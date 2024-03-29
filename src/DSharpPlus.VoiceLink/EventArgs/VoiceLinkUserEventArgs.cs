using DSharpPlus.AsyncEvents;
using DSharpPlus.Entities;

namespace DSharpPlus.VoiceLink.EventArgs
{
    public sealed class VoiceLinkUserEventArgs : AsyncEventArgs
    {
        public required VoiceLinkConnection Connection { get; init; }
        public required DiscordMember Member { get; init; }
        public required VoiceLinkUser? VoiceUser { get; init; }
    }
}
