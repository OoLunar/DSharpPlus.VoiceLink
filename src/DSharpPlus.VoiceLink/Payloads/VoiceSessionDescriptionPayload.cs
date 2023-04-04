using System.Collections.Generic;
using Newtonsoft.Json;

namespace DSharpPlus.VoiceLink.Payloads
{
    /// <summary>
    /// Finally, the voice server will respond with a <see cref="Enums.DiscordVoiceOpCode.SessionDescription"/> that includes the <c>mode</c> and <c>secret_key</c>, a 32 byte array used for encrypting and sending voice data.
    /// </summary>
    /// <param name="Mode"></param>
    /// <param name="SecretKey"></param>
    public sealed record VoiceSessionDescriptionPayload(
        [property: JsonProperty("mode")] string Mode,
        [property: JsonProperty("secret_key")] IReadOnlyList<byte> SecretKey
    );
}
