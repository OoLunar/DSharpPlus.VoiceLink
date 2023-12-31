using System;
using DSharpPlus.Entities;
using Newtonsoft.Json;

namespace DSharpPlus.VoiceLink.Commands
{
    // Explicitly annotated with Newtonsoft.Json attributes as required by DSharpPlus.DiscordClient.SendPayloadAsync<T>(GatewayOpCode, T);
    public record VoiceLinkStateCommand
    {
        [JsonProperty("guild_id")]
        public required ulong? GuildId { get; init; }

        [JsonProperty("channel_id")]
        public required ulong? ChannelId { get; init; }

        [JsonProperty("user_id")]
        public required ulong UserId { get; init; }

        [JsonProperty("member")]
        public required Optional<DiscordMember> Member { get; init; }

        [JsonProperty("session_id")]
        public required string SessionId { get; init; }

        [JsonProperty("deaf")]
        public required bool Deaf { get; init; }

        [JsonProperty("mute")]
        public required bool Mute { get; init; }

        [JsonProperty("self_deaf")]
        public required bool SelfDeaf { get; init; }

        [JsonProperty("self_mute")]
        public required bool SelfMute { get; init; }

        [JsonProperty("self_stream")]
        public required Optional<bool> SelfStream { get; init; }

        [JsonProperty("self_video")]
        public required bool SelfVideo { get; init; }

        [JsonProperty("suppress")]
        public required bool Suppress { get; init; }

        [JsonProperty("request_to_speak_timestamp")]
        public required DateTimeOffset? RequestToSpeakTimestamp { get; init; }
    }
}
