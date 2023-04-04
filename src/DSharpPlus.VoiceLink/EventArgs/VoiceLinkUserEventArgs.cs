using System;
using DSharpPlus.AsyncEvents;
using DSharpPlus.Entities;

namespace DSharpPlus.VoiceLink.EventArgs
{
    public sealed class VoiceLinkUserEventArgs : AsyncEventArgs
    {
        public VoiceLinkConnection Connection { get; init; }
        public VoiceLinkUser VoiceUser { get; init; }

        public bool IsInCache => VoiceUser.Ssrc == 0;
        public DiscordUser User => VoiceUser.User;
        public DiscordGuild Guild => Connection.Guild;
        public DiscordChannel Channel => Connection.Channel;
        public DiscordMember Member => (DiscordMember)VoiceUser.User;

        public VoiceLinkUserEventArgs(VoiceLinkConnection connection, VoiceLinkUser voiceUser)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            VoiceUser = voiceUser ?? throw new ArgumentNullException(nameof(voiceUser));
        }
    }
}
