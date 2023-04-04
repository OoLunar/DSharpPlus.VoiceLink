using System;
using System.IO;
using System.IO.Pipelines;
using DSharpPlus.Entities;
using DSharpPlus.VoiceLink.Enums;

namespace DSharpPlus.VoiceLink
{
    public sealed record VoiceLinkUser
    {
        public DiscordUser User { get; init; }
        public VoiceLinkConnection Connection { get; init; }
        public uint Ssrc { get; internal set; }
        public VoiceSpeakingIndicators VoiceIndication { get; internal set; } = VoiceSpeakingIndicators.None;
        internal Pipe VoicePipe { get; init; } = new();

        public PipeReader AudioPipe => VoicePipe.Reader;
        public Stream AudioStream => VoicePipe.Reader.AsStream(true);

        public VoiceLinkUser(DiscordUser user, VoiceLinkConnection connection, uint ssrc)
        {
            User = user ?? throw new ArgumentNullException(nameof(user));
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Ssrc = ssrc;
        }
    }
}
