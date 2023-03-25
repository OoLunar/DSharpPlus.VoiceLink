using System;
using DSharpPlus.Entities;
using Newtonsoft.Json;

namespace DSharpPlus.VoiceLink.Commands
{
    public sealed record VoiceLinkStateCommand(
        [property: JsonProperty("guild_id")] Optional<ulong> GuildId,
        [property: JsonProperty("channel_id")] ulong? ChannelId,
        [property: JsonProperty("user_id")] ulong UserId,
        [property: JsonProperty("member")] Optional<DiscordMember> Member,
        [property: JsonProperty("session_id")] string SessionId,
        [property: JsonProperty("deaf")] bool Deaf,
        [property: JsonProperty("mute")] bool Mute,
        [property: JsonProperty("self_deaf")] bool SelfDeaf,
        [property: JsonProperty("self_mute")] bool SelfMute,
        [property: JsonProperty("self_stream")] Optional<bool> SelfStream,
        [property: JsonProperty("self_video")] bool SelfVideo,
        [property: JsonProperty("suppress")] bool Suppress,
        [property: JsonProperty("request_to_speak_timestamp")] DateTimeOffset? RequestToSpeakTimestamp
    );
}
