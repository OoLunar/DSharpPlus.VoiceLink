using DSharpPlus.VoiceLink.Enums;

namespace DSharpPlus.VoiceLink.Commands
{
    public sealed record VoiceSpeakingCommand
    {
        public required VoiceSpeakingIndicators Speaking { get; init; }
        public required int Delay { get; init; }
        public required uint Ssrc { get; init; }
        public required ulong UserId
        {
            get; init;
        }
    }
}
