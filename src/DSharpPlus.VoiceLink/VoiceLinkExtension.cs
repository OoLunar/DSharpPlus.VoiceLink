using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.AsyncEvents;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.VoiceLink.Commands;
using DSharpPlus.VoiceLink.Enums;
using DSharpPlus.VoiceLink.EventArgs;
using DSharpPlus.VoiceLink.Sodium;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.VoiceLink
{
    public sealed class VoiceLinkExtension : BaseExtension
    {
        internal static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public VoiceLinkConfiguration Configuration { get; init; }
        public IReadOnlyDictionary<ulong, VoiceLinkConnection> Connections => _connections;
        internal readonly ConcurrentDictionary<ulong, VoiceLinkConnection> _connections = new();
        internal readonly ConcurrentDictionary<ulong, VoiceLinkPendingConnection> _pendingConnections = new();
        private readonly ILogger<VoiceLinkExtension> _logger;

        public event AsyncEventHandler<VoiceLinkExtension, VoiceLinkConnectionEventArgs> ConnectionCreated { add => _connectionCreated.Register(value); remove => _connectionCreated.Unregister(value); }
        internal readonly AsyncEvent<VoiceLinkExtension, VoiceLinkConnectionEventArgs> _connectionCreated = new("VOICELINK_CONNECTION_CREATED", EverythingWentWrongErrorHandler);

        public event AsyncEventHandler<VoiceLinkExtension, VoiceLinkConnectionEventArgs> ConnectionDestroyed { add => _connectionDestroyed.Register(value); remove => _connectionDestroyed.Unregister(value); }
        internal readonly AsyncEvent<VoiceLinkExtension, VoiceLinkConnectionEventArgs> _connectionDestroyed = new("VOICELINK_CONNECTION_DESTROYED", EverythingWentWrongErrorHandler);

        public event AsyncEventHandler<VoiceLinkExtension, VoiceLinkUserEventArgs> UserConnected { add => _userConnected.Register(value); remove => _userConnected.Unregister(value); }
        internal readonly AsyncEvent<VoiceLinkExtension, VoiceLinkUserEventArgs> _userConnected = new("VOICELINK_USER_CONNECTED", EverythingWentWrongErrorHandler);

        public event AsyncEventHandler<VoiceLinkExtension, VoiceLinkUserSpeakingEventArgs> UserSpeaking { add => _userSpeaking.Register(value); remove => _userSpeaking.Unregister(value); }
        internal readonly AsyncEvent<VoiceLinkExtension, VoiceLinkUserSpeakingEventArgs> _userSpeaking = new("VOICELINK_USER_SPEAKING", EverythingWentWrongErrorHandler);

        public event AsyncEventHandler<VoiceLinkExtension, VoiceLinkUserEventArgs> UserDisconnected { add => _userDisconnected.Register(value); remove => _userDisconnected.Unregister(value); }
        internal readonly AsyncEvent<VoiceLinkExtension, VoiceLinkUserEventArgs> _userDisconnected = new("VOICELINK_USER_DISCONNECTED", EverythingWentWrongErrorHandler);

        public VoiceLinkExtension(VoiceLinkConfiguration configuration)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = configuration.ServiceProvider.GetRequiredService<ILogger<VoiceLinkExtension>>();
            if (SodiumNativeMethods.Init() == -1)
            {
                throw new SodiumException("Sodium initialization failed. Is it installed?");
            }
        }

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
            Client.VoiceStateUpdated += VoiceStateUpdateEventHandlerAsync;
            Client.VoiceServerUpdated += VoiceServerUpdateEventHandlerAsync;
        }

        public override void Dispose() { }

        public async Task<VoiceLinkConnection> ConnectAsync(DiscordChannel channel, VoiceState voiceState, CancellationToken cancellationToken = default)
        {
            if (channel is null)
            {
                throw new ArgumentNullException(nameof(channel));
            }
            else if (channel.Type is not DiscordChannelType.Voice or DiscordChannelType.Stage)
            {
                throw new ArgumentException("Channel must be a voice or stage channel.", nameof(channel));
            }
            else if (channel.Guild is null)
            {
                throw new ArgumentException("Channel must be a guild channel.", nameof(channel));
            }
            else if (voiceState.HasFlag(VoiceState.Speaking))
            {
                throw new ArgumentException("The voice state cannot be speaking when connecting.", nameof(voiceState));
            }
            else if (_connections.TryGetValue(channel.Guild.Id, out _))
            {
                throw new InvalidOperationException($"The bot is already connected to a voice channel in guild {channel.Guild.Id}. The bot may only be connected to one voice channel per guild.");
            }

            DiscordPermissions botPermissions = channel.PermissionsFor(channel.Guild.CurrentMember);
            if (!botPermissions.HasPermission(DiscordPermissions.AccessChannels | DiscordPermissions.UseVoice))
            {
                throw new InvalidOperationException($"The bot must have the {DiscordPermissions.AccessChannels} and {DiscordPermissions.UseVoice} permissions to connect to a channel.");
            }
            else if (!botPermissions.HasPermission(DiscordPermissions.Speak) && !voiceState.HasFlag(VoiceState.UserMuted))
            {
                throw new InvalidOperationException($"The bot must have the {DiscordPermissions.Speak} permission to speak in a voice channel.");
            }
            else if (channel.UserLimit >= channel.Users.Count && !botPermissions.HasPermission(DiscordPermissions.ManageChannels))
            {
                throw new InvalidOperationException($"The voice channel is full and the bot must have the {DiscordPermissions.ManageChannels} permission to connect to override the channel user limit.");
            }

            _logger.LogDebug("Connecting to voice channel {ChannelId} in guild {GuildId}.", channel.Id, channel.Guild.Id);
            VoiceLinkPendingConnection pendingConnection = new();
            if (!_pendingConnections.TryAdd(channel.Guild.Id, pendingConnection))
            {
                throw new InvalidOperationException($"The bot is already connecting to a voice channel in guild {channel.Guild.Id}. The bot may only be connected to one voice channel per guild.");
            }

#pragma warning disable CS0618 // Type or member is obsolete
            // Error: This method should not be used unless you know what you're doing. Instead, look towards the other explicitly implemented methods which come with client-side validation.
            // Justification: I know what I'm doing.
            await Client.SendPayloadAsync(GatewayOpCode.VoiceStateUpdate, new VoiceLinkStateCommand()
            {
                GuildId = channel.Guild.Id,
                ChannelId = channel.Id,
                UserId = Client.CurrentUser.Id,
                Member = Optional.FromNoValue<DiscordMember>(),
                SessionId = string.Empty,
                Deaf = voiceState.HasFlag(VoiceState.ServerDeafened),
                Mute = voiceState.HasFlag(VoiceState.ServerMuted),
                SelfDeaf = voiceState.HasFlag(VoiceState.UserDeafened),
                SelfMute = voiceState.HasFlag(VoiceState.UserMuted),
                SelfStream = Optional.FromNoValue<bool>(),
                SelfVideo = false,
                Suppress = false,
                RequestToSpeakTimestamp = null
            });
#pragma warning restore CS0618 // Type or member is obsolete

            // From the Discord Docs (https://discord.com/developers/docs/topics/voice-connections#connecting-to-voice):
            // > If our request succeeded, the gateway will respond with two events—a Voice State Update event and a Voice Server Update event—meaning your
            // > library must properly wait for both events before continuing. The first will contain a new key, session_id, and the second will provide
            // > voice server information we can use to establish a new voice connection:
            // As such, we have pre-emptively created event handlers for both events, and wait for both to be received before continuing.

            // We're waiting for both the voice state and voice server update events to be received.
            await pendingConnection.Semaphore.WaitAsync(cancellationToken);
            await pendingConnection.Semaphore.WaitAsync(cancellationToken);
            _pendingConnections.TryRemove(channel.Guild.Id, out _);
            _logger.LogDebug("Received voice state and voice server update events for guild {GuildId}.", channel.Guild.Id);

            VoiceLinkConnection connection = new(this, channel, voiceState);
            _connections[channel.Guild.Id] = connection;
            await connection.InitializeAsync(pendingConnection.VoiceStateUpdateEventArgs!, pendingConnection.VoiceServerUpdateEventArgs!, cancellationToken);
            return connection;
        }

        private Task VoiceStateUpdateEventHandlerAsync(DiscordClient client, VoiceStateUpdateEventArgs eventArgs)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            else if (eventArgs is null)
            {
                throw new ArgumentNullException(nameof(eventArgs));
            }
            else if (_pendingConnections.TryGetValue(eventArgs.Guild.Id, out VoiceLinkPendingConnection? pendingConnection))
            {
                pendingConnection.VoiceStateUpdateEventArgs = eventArgs;
                pendingConnection.Semaphore.Release();
            }

            return Task.CompletedTask;
        }

        private Task VoiceServerUpdateEventHandlerAsync(DiscordClient client, VoiceServerUpdateEventArgs eventArgs)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            else if (eventArgs is null)
            {
                throw new ArgumentNullException(nameof(eventArgs));
            }
            else if (_pendingConnections.TryGetValue(eventArgs.Guild.Id, out VoiceLinkPendingConnection? pendingConnection))
            {
                pendingConnection.VoiceServerUpdateEventArgs = eventArgs;
                pendingConnection.Semaphore.Release();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// The event handler used to log all unhandled exceptions, usually from when <see cref="_commandErrored"/> itself errors.
        /// </summary>
        /// <param name="asyncEvent">The event that errored.</param>
        /// <param name="error">The error that occurred.</param>
        /// <param name="handler">The handler/method that errored.</param>
        /// <param name="sender">The extension.</param>
        /// <param name="eventArgs">The event arguments passed to <paramref name="handler"/>.</param>
        private static void EverythingWentWrongErrorHandler<TArgs>(AsyncEvent<VoiceLinkExtension, TArgs> asyncEvent, Exception error, AsyncEventHandler<VoiceLinkExtension, TArgs> handler, VoiceLinkExtension sender, TArgs eventArgs) where TArgs : AsyncEventArgs => sender._logger.LogError(error, "Event handler '{Method}' for event {AsyncEvent} threw an unhandled exception.", handler.Method, asyncEvent.Name);
    }
}
