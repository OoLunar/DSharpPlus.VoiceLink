using System;
using System.IO;
using System.IO.Pipelines;
using DSharpPlus.Entities;
using DSharpPlus.VoiceLink.Enums;
using DSharpPlus.VoiceLink.Opus;

namespace DSharpPlus.VoiceLink
{
    public sealed record VoiceLinkUser
    {
        public VoiceLinkConnection Connection { get; init; }
        public uint Ssrc { get; init; }
        public DiscordMember Member { get; internal set; }
        public VoiceSpeakingIndicators VoiceIndication { get; internal set; } = VoiceSpeakingIndicators.None;
        public PipeReader AudioPipe => _audioPipe.Reader;
        public Stream AudioStream => _audioPipe.Reader.AsStream(true);

        internal Pipe _audioPipe { get; init; } = new();
        internal OpusDecoder _opusDecoder { get; init; } = OpusDecoder.Create(OpusSampleRate.Opus48000Hz, 2);
        internal ushort _lastSequence;

        public VoiceLinkUser(VoiceLinkConnection connection, uint ssrc, DiscordMember member, ushort sequence = 0)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Ssrc = ssrc;
            Member = member;
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
