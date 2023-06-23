using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DSharpPlus.VoiceLink.Opus;
using DSharpPlus.VoiceLink.VoiceEncrypters;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.VoiceLink
{
    public sealed partial record VoiceLinkConnection
    {
        public PipeWriter? AudioPipe => _audioPipe?.Writer;
        public IVoiceEncrypter VoiceEncrypter => Extension.Configuration.VoiceEncrypter;
        public VoiceLinkUser VoiceLinkUser { get; private set; }
        public OpusEncoder OpusEncoder { get; private set; } = OpusEncoder.Create(OpusSampleRate.Opus48000Hz, 2, OpusApplication.Audio);
        public OpusApplication OpusApplication
        {
            get => _opusApplication; private set
            {
                _opusApplication = value;
                OpusErrorCode errorCode = OpusEncoder.Control(OpusControlRequest.SetApplication, (int)value);
                if (errorCode != OpusErrorCode.Ok)
                {
                    throw new OpusException(errorCode);
                }
            }
        }

        private OpusApplication _opusApplication;
        private readonly Pipe? _audioPipe = new();
        private byte[] _secretKey { get; set; }
        private ushort _sequence { get; set; }
        private uint _timestamp { get; set; }

        // MaxPacketDuration: 120ms
        // SampleRate: 48000hz
        // (MaxPacketDuration / 1000) * SampleRate = 5760 samples
        private const int MaxPacketDuration = 5760;

        [SuppressMessage("Style", "IDE0047", Justification = "Apparently PEMDAS isn't well remembered.")]
        private static int CalclulateMaxOutputSize(int sampleRate, int maxPacketDuration, int channels) => (sampleRate / 1000) * maxPacketDuration * channels;

        private async Task SendVoicePacketAsync()
        {
            unsafe int EncodeOpusPacket(ReadOnlySpan<byte> pcm, int sampleDuration, Span<byte> target)
            {
                return OpusEncoder.Encode(MemoryMarshal.Cast<byte, short>(pcm), sampleDuration, ref target);
            }

            // Referenced from VoiceNext:
            // internal int CalculateMaximumFrameSize() => 120 * (this.SampleRate / 1000);
            // internal int SampleCountToSampleSize(int sampleCount) => sampleCount * this.ChannelCount * 2 /* sizeof(int16_t) */;
            // audioFormat.SampleCountToSampleSize(audioFormat.CalculateMaximumFrameSize())
            // (120 * (this.SampleRate / 1000)) * this.ChannelCount * 2
            // Because the Opus data must be sent as 48,000hz and in 2 channels (stereo) we can hardcode the variables with their proper values
            // (120 * (48000 / 1000)) * 2 * 2
            // 120 * 48 * 4
            // 120 * 192
            // 23040
            // Where did the 120 or the 1000 come from? No clue. Hopefully they can stay hardcoded though.
            // The 1000 is the sample rate, which is the amount of samples per second. 48000 / 1000 = 48.
            // The 120 is the frame size, which is the amount of samples per frame. 48000 / 400 = 120.
            const int maximumOpusSize = 23040;

            // The buffer we use for the Opus data.
            Memory<byte> opusPacket = new(new byte[maximumOpusSize]);

            // The buffer we use for the Rtp header and the *encrypted* Opus data.
            // The switch case is us factoring in the nonce size, which differs per encryption mode.
            Memory<byte> completePacket = new(new byte[VoiceEncrypter.GetEncryptedSize(maximumOpusSize + 12)]);

            ReadResult result = default;
            while (!result.IsCompleted && _audioPipe is not null)
            {
                result = await _audioPipe.Reader.ReadAsync();

                // Encode Opus to the opusPacket buffer.
                EncodeOpusPacket(result.Buffer.IsSingleSegment ? result.Buffer.FirstSpan : result.Buffer.ToArray(), 120, opusPacket.Span);

                // Encode the RTP Header
                RtpUtilities.EncodeHeader(_sequence, _timestamp, VoiceLinkUser.Ssrc, completePacket.Span);

                // Attempt to encrypt the Opus data.
                if (!VoiceEncrypter.Encrypt(VoiceLinkUser, opusPacket.Span, _secretKey, completePacket.Span))
                {
                    _logger.LogError("Connection {GuildId}: Failed to encrypt Opus data.", Guild.Id);
                    return;
                }

                // Increment the sequence and timestamp.
                // Unchecked to prevent stack overflow exceptions, since we intend to wrap around.
                _sequence = unchecked((ushort)(_sequence + 1));
                _timestamp = unchecked(_timestamp + 120);

                // Trim the unused packet data and send the trimmed packet to the voice gateway.
                Memory<byte> trimmedPacket = completePacket.Trim(byte.MinValue);
                _ = await _udpClient!.SendAsync(trimmedPacket.ToArray(), trimmedPacket.Length);

                // Advance the pipe.
                _audioPipe.Reader.AdvanceTo(result.Buffer.End);
            }
        }

        private unsafe void ProcessPacket(UdpReceiveResult result)
        {
            if (!RtpUtilities.IsRtpHeader(result.Buffer))
            {
                // The source buffer contains an unknown RTP payload type.
                // According to RFC 3550 (The RTP specification), if the payload type is unknown then the packet should be discarded.
                return;
            }

            // Decode the RTP header.
            RtpUtilities.DecodeHeader(result.Buffer, out ushort sequence, out uint timestamp, out uint ssrc);

            // Grab the user associated with the ssrc.
            if (!_currentUsers.TryGetValue(ssrc, out VoiceLinkUser? voiceLinkUser))
            {
                voiceLinkUser = new VoiceLinkUser(this, ssrc);
                _currentUsers.TryAdd(ssrc, voiceLinkUser);
            }

            // Check if there has been any packet loss.
            ushort packetLossCount = unchecked((ushort)(sequence - voiceLinkUser._lastSequence));
            if (packetLossCount > 0)
            {
                _logger.LogWarning("Connection {GuildId}: User {UserId}, packet loss detected. Total packets lost: {PacketLossCount}.", Guild.Id, voiceLinkUser.User?.Id, packetLossCount);

                //OpusDecoder decoder = voiceLinkUser._opusDecoder;
                //voiceLinkUser._opusDecoder.Control(OpusControlRequest.GetLastPacketDuration, out int lastPacketDuration);
                //while (packetLossCount-- > 0)
                //{
                //    int decodedLength;
                //    Span<short> fecPCM = new(new short[SampleCountToSampleSize(lastPacketDuration)]);
                //    fixed (short* fecPCMPointer = fecPCM)
                //    {
                //        decodedLength = OpusNativeMethods.Decode((OpusDecoder*)Unsafe.AsPointer(ref decoder), null, 0, fecPCMPointer, lastPacketDuration, 1);
                //    }
                //
                //    if (decodedLength < 0)
                //    {
                //        _logger.LogError("Connection {GuildId}: User {UserId}, failed to decode FEC packet: Error {ErrorCode}, {ErrorMessage}", Guild.Id, voiceLinkUser.User.Id, decodedLength, OpusException.GetErrorMessage((OpusErrorCode)decodedLength));
                //        continue;
                //    }
                //
                //    // Write the... fec... packet to the audio pipe.
                //    // What does FEC stand for again?
                //    // Forward Error Correction?
                //    // No idea what I'm supposed to do with that.
                //    voiceLinkUser._lastSequence = unchecked(voiceLinkUser._lastSequence += 1);
                //    voiceLinkUser._audioPipe.Writer.Write(MemoryMarshal.Cast<short, byte>(fecPCM));
                //    voiceLinkUser._audioPipe.Writer.Advance(decodedLength);
                //}
            }

            // Decrypt the voice packet.
            Span<byte> opusAudio = new byte[VoiceEncrypter.GetDecryptedSize(result.Buffer.Length)];
            if (!VoiceEncrypter.Decrypt(voiceLinkUser, result.Buffer.AsSpan(0, opusAudio.Length), _secretKey, opusAudio))
            {
                _logger.LogError("Connection {GuildId}: User {UserId}, failed to decrypt packet.", Guild.Id, voiceLinkUser.User?.Id);
                return;
            }

            //Decode the Opus packet.
            Span<short> pcmAudio = new(new short[opusAudio.Length / 2]);
            int errorCode = voiceLinkUser._opusDecoder.Decode(opusAudio, ref pcmAudio, 120, false);
            if (errorCode != 0)
            {
                _logger.LogError("Connection {GuildId}: User {UserId}, failed to decode packet: Error {ErrorCode}, {ErrorMessage}", Guild.Id, voiceLinkUser.User?.Id, errorCode, OpusException.GetErrorMessage((OpusErrorCode)errorCode));
                return;
            }

            // Write the PCM audio to the pipe.
            voiceLinkUser._lastTimestamp = timestamp;
            voiceLinkUser._lastSequence = sequence;
            voiceLinkUser._audioPipe.Writer.Write(MemoryMarshal.Cast<short, byte>(pcmAudio));
            voiceLinkUser._audioPipe.Writer.Advance(pcmAudio.Length);
        }
    }
}
