using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DSharpPlus.VoiceLink.Payloads
{
    public sealed record VoiceReadyPayload(
        [property: JsonProperty("ssrc")] uint Ssrc,
        [property: JsonProperty("ip")] string Ip,
        [property: JsonProperty("port")] ushort Port,
        [property: JsonProperty("modes")] IReadOnlyList<string> Modes,
        [property: JsonProperty("heartbeat_interval"), Obsolete("HeartbeatInterval here is an erroneous field and should be ignored. The correct heartbeat_interval value comes from the Hello payload.")] int HeartbeatInterval
    );
}
