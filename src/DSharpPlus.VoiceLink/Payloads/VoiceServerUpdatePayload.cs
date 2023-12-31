namespace DSharpPlus.VoiceLink.Payloads
{
    /// <summary>
    /// Sent when a guild's voice server is updated. This is sent when initially connecting to voice, and when the current voice instance fails over to a new server.
    /// </summary>
    public sealed record DiscordVoiceServerUpdatePayload
    {
        /// <summary>
        /// The voice connection token.
        /// </summary>
        public required string Token { get; init; }

        /// <summary>
        /// The guild this voice server update is for.
        /// </summary>
        public required ulong GuildId { get; init; }

        /// <summary>
        /// The voice server host. A null endpoint means that the voice server allocated has gone away and is trying to be reallocated. You should attempt to disconnect from the currently connected voice server, and not attempt to reconnect until a new voice server is allocated.
        /// </summary>
        public required string? Endpoint { get; init; }
    }
}
