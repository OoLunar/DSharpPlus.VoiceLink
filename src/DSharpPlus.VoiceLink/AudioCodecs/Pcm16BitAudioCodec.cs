using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using DSharpPlus.VoiceLink.Opus;

namespace DSharpPlus.VoiceLink.AudioCodecs
{
    public class Pcm16BitAudioCodec : IAudioCodec
    {
        // 16 bits per sample
        private const int BYTES_PER_SAMPLE = 2;

        // 48 kHz
        private const int SAMPLE_RATE = 48000;

        // 20 milliseconds
        private const double FRAME_DURATION = 0.020;

        // 960 samples
        private const int FRAME_SIZE = (int)(SAMPLE_RATE * FRAME_DURATION);

        // 20 milliseconds of audio data, 1920 bytes
        private const int SINGLE_CHANNEL_BUFFER_SIZE = FRAME_SIZE * BYTES_PER_SAMPLE;

        public int Channels { get; init; }
        public int BufferSize { get; init; }
        private OpusEncoder _opusEncoder { get; init; }
        private OpusDecoder _opusDecoder { get; init; }

        public Pcm16BitAudioCodec(int channels = 2)
        {
            Channels = channels;
            BufferSize = SINGLE_CHANNEL_BUFFER_SIZE * Channels;
            _opusDecoder = OpusDecoder.Create(OpusSampleRate.Opus48000Hz, channels);
        }

        /// <inheritdoc/>
        public int GetMaxBufferSize() => BufferSize;

        /// <inheritdoc/>
        public int EncodeOpus(ReadOnlySequence<byte> input, Span<byte> output)
        {
            int sliceSize = Math.Min((int)input.Length, BufferSize);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(sliceSize);
            try
            {
                input.Slice(0, sliceSize).CopyTo(buffer);
                return _opusEncoder.Encode(Unsafe.As<byte[], short[]>(ref buffer), FRAME_SIZE, ref output);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <inheritdoc/>
        public int DecodeOpus(bool hasPacketLoss, ReadOnlySpan<byte> input, Span<byte> output)
        {
            _opusDecoder.Decode(input, output, FRAME_SIZE, hasPacketLoss);
            return output.Length;
        }
    }
}
