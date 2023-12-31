namespace DSharpPlus.VoiceLink.Commands
{
    public sealed record DiscordVoiceResumingCommand
    {
        public required ulong ServerId { get; init; }
        public required string SessionId { get; init; }
        public required string Token { get; init; }
    }
}
