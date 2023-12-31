namespace DSharpPlus.VoiceLink.Commands
{
    public sealed record VoiceIdentifyCommand
    {
        public required ulong ServerId { get; init; }
        public required ulong UserId { get; init; }
        public required string SessionId { get; init; }
        public required string Token { get; init; }
    }
}
