using Newtonsoft.Json;

namespace DSharpPlus.VoiceLink.Payloads
{
    public sealed record VoiceUserJoinPayload(
        [property: JsonProperty("user_id")] ulong UserId,
        [property: JsonProperty("audio_ssrc")] uint Ssrc
    );
}
