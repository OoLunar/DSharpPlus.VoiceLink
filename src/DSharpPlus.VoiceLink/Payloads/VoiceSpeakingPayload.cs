using DSharpPlus.VoiceLink.Enums;

namespace DSharpPlus.VoiceLink.Payloads
{
    public sealed record VoiceSpeakingPayload
    {
        public ulong UserId { get; init; }
        public required uint Ssrc { get; init; }
        public required VoiceSpeakingIndicators Speaking { get; init; }
        public required int Delay { get; init; }
    }
}
