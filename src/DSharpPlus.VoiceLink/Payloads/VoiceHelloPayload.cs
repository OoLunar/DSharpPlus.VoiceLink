namespace DSharpPlus.VoiceLink.Payloads
{
    public sealed record VoiceHelloPayload
    {
        public required double HeartbeatInterval { get; init; }
    }
}
