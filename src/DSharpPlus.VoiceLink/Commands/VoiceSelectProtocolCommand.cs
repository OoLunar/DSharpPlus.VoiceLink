using Newtonsoft.Json;

namespace DSharpPlus.VoiceLink.Commands
{
    /// <summary>
    /// Once we've fully discovered our external IP and UDP port, we can then tell the voice WebSocket what it is, and start receiving/sending data.
    /// </summary>
    /// <param name="Protocol"></param>
    /// <param name="Data"></param>
    /// <returns></returns>
    public sealed record VoiceSelectProtocolCommand(
        [property: JsonProperty("protocol")] string Protocol,
        [property: JsonProperty("data")] VoiceSelectProtocolCommandData Data
    );

    /// <summary>
    ///
    /// </summary>
    /// <param name="Address"></param>
    /// <param name="Port"></param>
    /// <param name="Mode"></param>
    public sealed record VoiceSelectProtocolCommandData(
        [property: JsonProperty("address")] string Address,
        [property: JsonProperty("port")] ushort Port,
        [property: JsonProperty("mode")] string Mode
    );
}
