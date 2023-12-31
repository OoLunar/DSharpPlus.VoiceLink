namespace DSharpPlus.VoiceLink.Payloads
{
    public sealed record VoiceUserLeavePayload
    {
        public required ulong UserId { get; init; }
    }
}
