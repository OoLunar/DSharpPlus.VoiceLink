using DSharpPlus.VoiceLink.Enums;
using Newtonsoft.Json;

namespace DSharpPlus.VoiceLink
{
    public sealed record VoiceGatewayDispatch([property: JsonProperty("op")] VoiceOpCode OpCode, [property: JsonProperty("d")] object? Data);
}
