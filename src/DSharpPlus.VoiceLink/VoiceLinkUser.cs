using System;
using System.IO;
using System.IO.Pipelines;
using DSharpPlus.Entities;
using DSharpPlus.VoiceLink.AudioDecoders;
using DSharpPlus.VoiceLink.Enums;

namespace DSharpPlus.VoiceLink
{
    public sealed record VoiceLinkUser
    {
        public VoiceLinkConnection Connection { get; init; }
        public uint Ssrc { get; init; }
        public DiscordMember Member { get; internal set; }
        public VoiceSpeakingIndicators VoiceIndication { get; internal set; } = VoiceSpeakingIndicators.None;
        public IAudioDecoder AudioDecoder { get; init; }
        public PipeReader AudioPipe => _audioPipe.Reader;
        public Stream AudioStream => _audioPipe.Reader.AsStream(true);

        internal Pipe _audioPipe { get; init; } = new();
        internal ushort _lastSequence;

        public VoiceLinkUser(VoiceLinkConnection connection, uint ssrc, DiscordMember member, IAudioDecoder audioDecoder, ushort sequence = 0)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Ssrc = ssrc;
            Member = member;
            AudioDecoder = audioDecoder ?? throw new ArgumentNullException(nameof(audioDecoder));
            _lastSequence = sequence;
        }

        internal bool UpdateSequence(ushort sequence)
        {
            bool hasPacketLoss = unchecked(++_lastSequence) != sequence;
            if (hasPacketLoss)
            {
                _lastSequence = sequence;
            }

            return hasPacketLoss;
        }
    }
}
