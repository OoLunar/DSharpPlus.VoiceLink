using System.Text.Json.Serialization;
using DSharpPlus.VoiceLink.Enums;

namespace DSharpPlus.VoiceLink
{
    public record VoiceGatewayDispatch
    {
        [JsonPropertyName("op")]
        public VoiceOpCode OpCode { get; init; }

        [JsonPropertyName("d")]
        public object? Data { get; init; }
    }

    public record VoiceGatewayDispatch<T> : VoiceGatewayDispatch
    {
        [JsonPropertyName("d")]
        public new T Data { get; init; } = default!;
    }
}
