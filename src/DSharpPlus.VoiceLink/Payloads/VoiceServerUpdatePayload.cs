using Newtonsoft.Json;

namespace DSharpPlus.VoiceLink.Payloads
{
    /// <summary>
    /// Sent when a guild's voice server is updated. This is sent when initially connecting to voice, and when the current voice instance fails over to a new server.
    /// </summary>
    /// <param name="Token">The voice connection token.</param>
    /// <param name="GuildId">The guild this voice server update is for.</param>
    /// <param name="Endpoint">The voice server host. A null endpoint means that the voice server allocated has gone away and is trying to be reallocated. You should attempt to disconnect from the currently connected voice server, and not attempt to reconnect until a new voice server is allocated.</param>
    public sealed record DiscordVoiceServerUpdatePayload(
        [property: JsonProperty("token")] string Token,
        [property: JsonProperty("guild_id")] ulong GuildId,
        [property: JsonProperty("endpoint")] string? Endpoint
    );
}
