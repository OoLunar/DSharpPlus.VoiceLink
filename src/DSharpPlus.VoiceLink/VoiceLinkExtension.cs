using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.VoiceLink.Commands;
using DSharpPlus.VoiceLink.Enums;

namespace DSharpPlus.VoiceLink
{
    public sealed class VoiceLinkExtension : BaseExtension
    {
        public VoiceLinkConfiguration Configuration { get; init; }
        public IReadOnlyDictionary<ulong, VoiceLinkConnection> Connections => _connections;
        internal readonly ConcurrentDictionary<ulong, VoiceLinkConnection> _connections = new();

        public VoiceLinkExtension(VoiceLinkConfiguration configuration) => Configuration = configuration;

        protected override void Setup(DiscordClient client)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            else if (Client is not null)
            {
                throw new InvalidOperationException("This extension has already been registered to a client.");
            }

            Client = client;
            Client.VoiceStateUpdated += VoiceStateUpdateEventHandler;
            Client.VoiceServerUpdated += VoiceServerUpdateEventHandler;
        }

        public override void Dispose() => Parallel.ForEachAsync(_connections.Values, CancellationToken.None, async (connection, cancellationToken) => await connection.DisconnectAsync()).GetAwaiter().GetResult();

        public async Task<VoiceLinkConnection> ConnectAsync(DiscordChannel channel, VoiceState voiceState)
        {
            if (channel is null)
            {
                throw new ArgumentNullException(nameof(channel));
            }
            else if (channel.Type is not ChannelType.Voice or ChannelType.Stage)
            {
                throw new ArgumentException("Channel must be a voice or stage channel.", nameof(channel));
            }
            else if (channel.Guild is null)
            {
                throw new ArgumentException("Channel must be a guild channel.", nameof(channel));
            }

            Permissions botPermissions = channel.PermissionsFor(channel.Guild.CurrentMember);
            if (!botPermissions.HasPermission(Permissions.AccessChannels | Permissions.UseVoice))
            {
                throw new InvalidOperationException($"The bot must have the {Permissions.AccessChannels} and {Permissions.UseVoice} permissions to connect to a channel.");
            }
            else if (!botPermissions.HasPermission(Permissions.Speak) && !voiceState.HasFlag(VoiceState.UserMuted))
            {
                throw new InvalidOperationException($"The bot must have the {Permissions.Speak} permission to speak in a voice channel.");
            }
            else if (channel.UserLimit >= channel.Users.Count && !botPermissions.HasPermission(Permissions.ManageChannels))
            {
                throw new InvalidOperationException($"The voice channel is full and the bot must have the {Permissions.ManageChannels} permission to connect to override the channel user limit.");
            }

            VoiceLinkConnection connection = new(this, channel, Client.CurrentUser, voiceState);
            if (!_connections.TryAdd(channel.Guild.Id, connection))
            {
                throw new InvalidOperationException($"The bot is already connected to a voice channel in guild {channel.Guild.Id}. The bot may only be connected to one voice channel per guild.");
            }

#pragma warning disable CS0618 // Type or member is obsolete
            // Error: This method should not be used unless you know what you're doing. Instead, look towards the other explicitly implemented methods which come with client-side validation.
            // Justification: I know what I'm doing.
            await Client.SendPayloadAsync(GatewayOpCode.VoiceStateUpdate, new VoiceLinkStateCommand(
                channel.Guild is null ? Optional.FromNoValue<ulong>() : channel.Guild.Id,
                channel.Id,
                Client.CurrentUser.Id,
                Optional.FromNoValue<DiscordMember>(),
                string.Empty,
                voiceState.HasFlag(VoiceState.ServerDeafened),
                voiceState.HasFlag(VoiceState.ServerMuted),
                voiceState.HasFlag(VoiceState.UserDeafened),
                voiceState.HasFlag(VoiceState.UserMuted),
                Optional.FromNoValue<bool>(),
                false,
                false,
                null
            ));
#pragma warning restore CS0618 // Type or member is obsolete

            // From the Discord Docs (https://discord.com/developers/docs/topics/voice-connections#connecting-to-voice):
            // > If our request succeeded, the gateway will respond with two events—a Voice State Update event and a Voice Server Update event—meaning your
            // > library must properly wait for both events before continuing. The first will contain a new key, session_id, and the second will provide
            // > voice server information we can use to establish a new voice connection:
            // As such, we have pre-emptively created event handlers for both events, and wait for both to be received before continuing.
            //await connection.IdleUntilReadyAsync();
            return connection;
        }

        private Task VoiceStateUpdateEventHandler(DiscordClient client, VoiceStateUpdateEventArgs eventArgs)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            else if (eventArgs is null)
            {
                throw new ArgumentNullException(nameof(eventArgs));
            }
            else if (_connections.TryGetValue(eventArgs.Guild.Id, out VoiceLinkConnection? connection))
            {
                connection._voiceStateUpdateEventArgs = eventArgs;
                if (connection._voiceServerUpdateEventArgs is not null)
                {
                    return connection.ConnectAsync();
                }
            }

            return Task.CompletedTask;
        }

        private Task VoiceServerUpdateEventHandler(DiscordClient client, VoiceServerUpdateEventArgs eventArgs)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            else if (eventArgs is null)
            {
                throw new ArgumentNullException(nameof(eventArgs));
            }
            else if (_connections.TryGetValue(eventArgs.Guild.Id, out VoiceLinkConnection? connection))
            {
                connection._voiceServerUpdateEventArgs = eventArgs;
                if (connection._voiceStateUpdateEventArgs is not null)
                {
                    return connection.ConnectAsync();
                }
            }

            return Task.CompletedTask;
        }
    }
}
