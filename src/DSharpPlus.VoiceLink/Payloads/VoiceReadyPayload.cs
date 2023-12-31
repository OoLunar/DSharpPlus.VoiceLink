using System.Collections.Generic;

namespace DSharpPlus.VoiceLink.Payloads
{
    public sealed record VoiceReadyPayload
    {
        public required uint Ssrc { get; init; }
        public required string Ip { get; init; }
        public required ushort Port { get; init; }
        public required IReadOnlyList<string> Modes { get; init; }
    }
}
