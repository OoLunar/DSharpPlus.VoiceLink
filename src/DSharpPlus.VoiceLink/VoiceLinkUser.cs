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
        private bool _sequenceInitialized;

        public VoiceLinkUser(VoiceLinkConnection connection, uint ssrc, DiscordMember member)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Ssrc = ssrc;
            Member = member;
        }

        public bool UpdateSequence(ushort sequence)
        {
            bool hasPacketLoss = unchecked(++_lastSequence) != sequence;
            if (hasPacketLoss)
            {
                _lastSequence = sequence;
                if (!_sequenceInitialized)
                {
                    _sequenceInitialized = true;
                    hasPacketLoss = false;
                }
            }

            return hasPacketLoss;
        }
    }
}
