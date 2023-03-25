using Newtonsoft.Json;

namespace DSharpPlus.VoiceLink.Commands
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="ServerId"></param>
    /// <param name="SessionId"></param>
    /// <param name="Token"></param>
    public sealed record DiscordVoiceResumingCommand(
        [property: JsonProperty("server_id")] ulong ServerId,
        [property: JsonProperty("session_id")] string SessionId,
        [property: JsonProperty("token")] string Token)
    { }
}
