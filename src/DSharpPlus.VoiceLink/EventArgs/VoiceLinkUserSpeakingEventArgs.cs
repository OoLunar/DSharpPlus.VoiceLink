using DSharpPlus.AsyncEvents;
using DSharpPlus.Entities;
using DSharpPlus.VoiceLink.Enums;
using DSharpPlus.VoiceLink.Payloads;

namespace DSharpPlus.VoiceLink.EventArgs
{
    public sealed class VoiceLinkUserSpeakingEventArgs : AsyncEventArgs
    {
        public required VoiceLinkConnection Connection { get; init; }
        public required VoiceSpeakingPayload Payload { get; init; }
        public required VoiceLinkUser VoiceUser { get; init; }

        public DiscordUser User => VoiceUser.Member;
        public DiscordGuild Guild => Connection.Guild;
        public DiscordChannel Channel => Connection.Channel;
        public DiscordMember Member => VoiceUser.Member;
        public bool IsSpeaking => VoiceUser.VoiceIndication.HasFlag(VoiceSpeakingIndicators.Microphone);
    }
}
