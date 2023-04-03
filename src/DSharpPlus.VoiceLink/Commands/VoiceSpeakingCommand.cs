using DSharpPlus.VoiceLink.Enums;
using Newtonsoft.Json;

namespace DSharpPlus.VoiceLink.Commands
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="Speaking"></param>
    /// <param name="Delay"></param>
    /// <param name="SSRC"></param>
    public sealed record VoiceSpeakingCommand(
        [property: JsonProperty("speaking")] VoiceSpeakingIndicators Speaking,
        [property: JsonProperty("delay")] int Delay,
        [property: JsonProperty("ssrc")] uint SSRC)
    { }
}
