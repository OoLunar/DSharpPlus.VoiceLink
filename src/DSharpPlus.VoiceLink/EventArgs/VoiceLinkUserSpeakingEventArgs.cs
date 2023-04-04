using System;
using DSharpPlus.AsyncEvents;
using DSharpPlus.Entities;
using DSharpPlus.VoiceLink.Commands;
using DSharpPlus.VoiceLink.Enums;

namespace DSharpPlus.VoiceLink.EventArgs
{
    public sealed class VoiceLinkUserSpeakingEventArgs : AsyncEventArgs
    {
        public VoiceLinkConnection Connection { get; init; }
        public VoiceSpeakingCommand Command { get; init; }
        public VoiceLinkUser VoiceUser { get; init; }

        public DiscordUser User => VoiceUser.User;
        public DiscordGuild Guild => Connection.Guild;
        public DiscordChannel Channel => Connection.Channel;
        public DiscordMember Member => (DiscordMember)VoiceUser.User;
        public bool IsSpeaking => VoiceUser.VoiceIndication.HasFlag(VoiceSpeakingIndicators.Microphone);

        public VoiceLinkUserSpeakingEventArgs(VoiceLinkConnection connection, VoiceSpeakingCommand command, VoiceLinkUser voiceUser)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            VoiceUser = voiceUser ?? throw new ArgumentNullException(nameof(voiceUser));
        }
    }
}
