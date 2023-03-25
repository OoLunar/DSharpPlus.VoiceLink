using Newtonsoft.Json;

namespace DSharpPlus.VoiceLink.Payloads
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="HeartbeatInterval"></param>
    public sealed record VoiceHelloPayload([property: JsonProperty("heartbeat_interval")] int HeartbeatInterval) { }
}
