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

        public PipeReader AudioPipe => _voicePipe.Reader;
        public Stream AudioStream => _voicePipe.Reader.AsStream(true);
        internal Pipe _voicePipe { get; private set; } = new();

        public VoiceLinkUser(DiscordUser user, VoiceLinkConnection connection, uint ssrc)
        {
            User = user ?? throw new ArgumentNullException(nameof(user));
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Ssrc = ssrc;
        }

        internal void ResetSsrc(uint ssrc)
        {
            if (Ssrc == ssrc)
            {
                return;
            }

            Ssrc = ssrc;
            _voicePipe.Writer.Complete();
            _voicePipe = new();
        }
    }
}
