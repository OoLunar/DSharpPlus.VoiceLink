namespace DSharpPlus.VoiceLink.Opus
{
    public enum OpusApplication
    {
        /// <summary>
        /// Gives the best quality at a given bitrate for voice signals. It enhances the input signal by high-pass filtering and emphasizing formants and harmonics. Optionally it includes in-band forward error correction to protect against packet loss. Use this mode for typical VoIP applications. Because of the enhancement, even at high bitrates the output may sound different from the input.
        /// </summary>
        Voip = 2048,

        /// <summary>
        /// Gives the best quality at a given bitrate for most non-voice signals like music. Use this mode for music and mixed (music/voice) content, broadcast, and applications requiring less than 15 ms of coding delay.
        /// </summary>
        Audio = 2049,

        /// <summary>
        /// Configures low-delay mode that disables the speech-optimized mode in exchange for slightly reduced delay. This mode can only be set on an newly initialized or freshly reset encoder because it changes the codec delay.
        /// </summary>
        RestrictedLowDelay = 2051
    }
}
