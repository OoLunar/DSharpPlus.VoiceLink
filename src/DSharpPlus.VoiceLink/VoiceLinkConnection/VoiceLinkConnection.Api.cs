using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.Net.Serialization;
using DSharpPlus.VoiceLink.Commands;
using DSharpPlus.VoiceLink.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.VoiceLink
{
    public sealed partial record VoiceLinkConnection
    {
        public VoiceLinkExtension Extension { get; init; }
        public DiscordChannel Channel { get; init; }
        public DiscordUser User { get; init; }
        public VoiceState VoiceState { get; internal set; }
        public ConnectionState ConnectionState { get; private set; }
        public DiscordGuild Guild => Channel.Guild;
        public DiscordMember? Member => User as DiscordMember;
        public IReadOnlyDictionary<uint, VoiceLinkUser> CurrentUsers => _currentUsers;

        private readonly ConcurrentDictionary<uint, VoiceLinkUser> _currentUsers = new();
        private readonly ILogger<VoiceLinkConnection> _logger;

        public VoiceLinkConnection(VoiceLinkExtension extension, DiscordChannel channel, DiscordUser user, VoiceState voiceState)
        {
            Extension = extension;
            Channel = channel;
            User = user;
            VoiceState = voiceState;
            _logger = extension.Configuration.ServiceProvider.GetRequiredService<ILogger<VoiceLinkConnection>>();
            _secretKey = new byte[32];
        }

        public Task IdleUntilReadyAsync() => _voiceStateUpdateTcs.Task;

        public async Task ConnectAsync()
        {
            // Setup endpoint
            if (_voiceServerUpdateEventArgs?.Endpoint is null)
            {
                throw new InvalidOperationException($"The {nameof(_voiceServerUpdateEventArgs.Endpoint)} argument is null. A null endpoint means that the voice server allocated has gone away and is trying to be reallocated. You should attempt to disconnect from the currently connected voice server, and not attempt to reconnect until a new voice server is allocated.");
            }

            // Resolve if the endpoint is a ip address or a hostname
            string? endpointHost;
            int endpointPort;
            int endpointIndex = _voiceServerUpdateEventArgs.Endpoint.LastIndexOf(':');
            if (endpointIndex != -1)
            {
                endpointHost = _voiceServerUpdateEventArgs.Endpoint[..endpointIndex];
                endpointPort = int.Parse(_voiceServerUpdateEventArgs.Endpoint[(endpointIndex + 1)..], NumberStyles.Number, CultureInfo.InvariantCulture);
            }
            else
            {
                endpointHost = _voiceServerUpdateEventArgs.Endpoint;
                endpointPort = 443;
            }

            // Connect to endpoint
            _webSocketClient = Extension.Configuration.WebSocketClientFactory(Extension.Configuration.Proxy);
            _webSocketClient.Disconnected += WebsocketDisconnectedAsync;
            _webSocketClient.ExceptionThrown += WebsocketExceptionThrownAsync;
            _webSocketClient.MessageReceived += WebsocketMessageReceivedAsync;

            Uri endpointUri = new UriBuilder()
            {
                Scheme = "wss",
                Host = endpointHost,
                Port = endpointPort,
                Query = $"v=4&encoding=json"
            }.Uri;

            _logger.LogDebug("Connection {GuildId}: Connecting to {EndpointUri}", Guild.Id, endpointUri);
            await _webSocketClient.ConnectAsync(endpointUri);
            _logger.LogDebug("Connection {GuildId}: Connected to {EndpointUri}", Guild.Id, endpointUri);
        }

        public async Task DisconnectAsync()
        {
            _logger.LogDebug("Connection {GuildId}: Disconnecting", Guild.Id);
            ConnectionState = ConnectionState.None;
            _voiceServerUpdateEventArgs = null;
            _voiceStateUpdateEventArgs = null;
            _voiceStateUpdateTcs = new();
            _heartbeatQueue.Clear();
            Extension._connections.TryRemove(Channel.Id, out _);

            if (_webSocketClient is not null)
            {
                await _webSocketClient.DisconnectAsync();
            }

#pragma warning disable CS0618 // Type or member is obsolete
            await Extension.Client.SendPayloadAsync(GatewayOpCode.VoiceStateUpdate, new VoiceLinkStateCommand(
                Guild is null ? Optional.FromNoValue<ulong>() : Guild.Id,
                null,
                User.Id,
                Optional.FromNoValue<DiscordMember>(),
                _voiceStateUpdateEventArgs?.SessionId ?? string.Empty,
                VoiceState.HasFlag(VoiceState.ServerDeafened),
                VoiceState.HasFlag(VoiceState.ServerMuted),
                VoiceState.HasFlag(VoiceState.UserDeafened),
                VoiceState.HasFlag(VoiceState.UserMuted),
                Optional.FromNoValue<bool>(),
                false,
                false,
                null
            ));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public async Task ReconnectAsync()
        {
            _logger.LogDebug("Connection {GuildId}: Reconnecting", Guild.Id);
            await DisconnectAsync();
            Extension._connections.AddOrUpdate(Guild.Id, this, (key, value) => this);

#pragma warning disable CS0618 // Type or member is obsolete
            // Error: This method should not be used unless you know what you're doing. Instead, look towards the other explicitly implemented methods which come with client-side validation.
            // Justification: I know what I'm doing.
            await Extension.Client.SendPayloadAsync(GatewayOpCode.VoiceStateUpdate, new VoiceLinkStateCommand(
                Guild is null ? Optional.FromNoValue<ulong>() : Guild.Id,
                Channel.Id,
                Extension.Client.CurrentUser.Id,
                Optional.FromNoValue<DiscordMember>(),
                string.Empty,
                VoiceState.HasFlag(VoiceState.ServerDeafened),
                VoiceState.HasFlag(VoiceState.ServerMuted),
                VoiceState.HasFlag(VoiceState.UserDeafened),
                VoiceState.HasFlag(VoiceState.UserMuted),
                Optional.FromNoValue<bool>(),
                false,
                false,
                null
            ));
#pragma warning restore CS0618 // Type or member is obsolete

            await IdleUntilReadyAsync();
        }

        public async Task StartSpeakingAsync()
        {
            await _webSocketClient!.SendMessageAsync(DiscordJson.SerializeObject(new VoiceGatewayDispatch(VoiceOpCode.Speaking, new VoiceSpeakingCommand(VoiceSpeakingIndicators.Microphone, 0, _ssrc, Extension.Client.CurrentUser.Id))));
            await SendVoicePacketAsync();
        }
    }
}
