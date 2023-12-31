using System.Collections.Generic;

namespace DSharpPlus.VoiceLink.Payloads
{
    /// <summary>
    /// Finally, the voice server will respond with a <see cref="Enums.DiscordVoiceOpCode.SessionDescription"/> that includes the <c>mode</c> and <c>secret_key</c>, a 32 byte array used for encrypting and sending voice data.
    /// </summary>
    /// <param name="Mode"></param>
    /// <param name="SecretKey"></param>
    public sealed record VoiceSessionDescriptionPayload
    {
        public required string Mode { get; init; }

        // This cannot be a byte[] because it will attempt to read the property as a base64 string.
        public required IEnumerable<byte> SecretKey { get; init; }
    }
}
