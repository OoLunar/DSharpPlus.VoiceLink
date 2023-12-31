namespace DSharpPlus.VoiceLink.Commands
{
    /// <summary>
    /// Once we've fully discovered our external IP and UDP port, we can then tell the voice WebSocket what it is, and start receiving/sending data.
    /// </summary>
    public sealed record VoiceSelectProtocolCommand
    {
        public required string Protocol { get; init; }
        public required VoiceSelectProtocolCommandData Data { get; init; }
    }

    public sealed record VoiceSelectProtocolCommandData
    {
        public required string Address { get; init; }
        public required ushort Port { get; init; }
        public required string Mode { get; init; }
    }
}
