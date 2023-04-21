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
        public uint Ssrc { get; internal set; }
        public DiscordUser? User { get; internal set; }
        public VoiceSpeakingIndicators VoiceIndication { get; internal set; } = VoiceSpeakingIndicators.None;
        public PipeReader AudioPipe => _audioPipe.Reader;
        public Stream AudioStream => _audioPipe.Reader.AsStream(true);

        internal Pipe _audioPipe { get; private set; } = new();
        internal OpusDecoder _opusDecoder { get; set; } = OpusDecoder.Create(OpusSampleRate.Opus48000Hz, 2);
        internal ushort _lastSequence { get; set; }
        internal uint _lastTimestamp { get; set; }

        public VoiceLinkUser(VoiceLinkConnection connection, uint ssrc, DiscordUser? user = null)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Ssrc = ssrc;
            User = user;
        }
    }
}
