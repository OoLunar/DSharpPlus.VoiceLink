using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.Net.Serialization;
using DSharpPlus.Net.WebSocket;
using DSharpPlus.VoiceLink.Commands;
using DSharpPlus.VoiceLink.Enums;
using DSharpPlus.VoiceLink.Payloads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DSharpPlus.VoiceLink
{
    public sealed record VoiceLinkConnection
    {
        public VoiceLinkExtension Extension { get; init; }
        public DiscordChannel Channel { get; init; }
        public DiscordUser User { get; init; }
        public VoiceState VoiceState { get; internal set; }
        public ConnectionState ConnectionState { get; private set; }

        /// <summary>
        /// The ping is milliseconds between the client sending a heartbeat and receiving a heartbeat ack.
        /// </summary>
        public long Ping { get; private set; }

        public DiscordGuild Guild => Channel.Guild;
        public DiscordMember? Member => User as DiscordMember;
        public IReadOnlyDictionary<ulong, VoiceLinkUser> CurrentUsers => _currentUsers;
        public PipeWriter AudioPipe => _audioPipe.Writer;

        internal VoiceStateUpdateEventArgs? _voiceStateUpdateEventArgs { get; set; }
        internal VoiceServerUpdateEventArgs? _voiceServerUpdateEventArgs { get; set; }
        internal TaskCompletionSource _voiceStateUpdateTcs { get; set; } = new();
        internal IWebSocketClient? _webSocketClient { get; set; }

        private readonly Pipe _audioPipe = new();
        private readonly ConcurrentQueue<long> _heartbeatQueue = new();
        private readonly ILogger<VoiceLinkConnection> _logger;
        private readonly ConcurrentDictionary<ulong, VoiceLinkUser> _currentUsers = new();
        private Uri? _endpointUri { get; set; }
        private UdpClient? _udpClient { get; set; }

        public VoiceLinkConnection(VoiceLinkExtension extension, DiscordChannel channel, DiscordUser user, VoiceState voiceState)
        {
            Extension = extension;
            Channel = channel;
            User = user;
            VoiceState = voiceState;
            _logger = extension.Configuration.ServiceProvider.GetRequiredService<ILogger<VoiceLinkConnection>>();
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
            _webSocketClient.Disconnected += DisconnectedAsync;
            _webSocketClient.ExceptionThrown += ExceptionThrownAsync;
            _webSocketClient.MessageReceived += MessageReceivedAsync;

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

        private async Task ResumeAsync()
        {
            _logger.LogDebug("Connection {GuildId}: Attempting to resume...", Guild.Id);
            ConnectionState = ConnectionState.Resuming;
            await _webSocketClient!.ConnectAsync(_endpointUri);
            await _webSocketClient.SendMessageAsync(DiscordJson.SerializeObject(new VoiceGatewayDispatch(VoiceOpCode.Resume, new DiscordVoiceResumingCommand(_voiceServerUpdateEventArgs!.Guild.Id, _voiceStateUpdateEventArgs!.SessionId, _voiceServerUpdateEventArgs.VoiceToken))));
        }

        private Task DisconnectedAsync(IWebSocketClient webSocketClient, SocketCloseEventArgs eventArgs)
        {
            _logger.LogDebug("Connection {GuildId}: Disconnected from endpoint {Endpoint}. Error code {Code}: {Message}", Guild.Id, _endpointUri, eventArgs.CloseCode, eventArgs.CloseMessage);
            return Task.CompletedTask;
        }

        private Task ExceptionThrownAsync(IWebSocketClient webSocketClient, SocketErrorEventArgs eventArgs)
        {
            _logger.LogError(eventArgs.Exception, "Connection {GuildId}: Exception thrown", Guild.Id);
            return Task.CompletedTask;
        }

        private Task MessageReceivedAsync(IWebSocketClient webSocketClient, SocketMessageEventArgs eventArgs)
        {
            if (eventArgs is not SocketTextMessageEventArgs textMessageEventArgs)
            {
                throw new InvalidOperationException($"The {nameof(eventArgs)} argument is not a {nameof(SocketTextMessageEventArgs)}.");
            }

            VoiceGatewayDispatch? payload = JsonConvert.DeserializeObject<VoiceGatewayDispatch>(textMessageEventArgs.Message);
            if (payload is null)
            {
                _logger.LogWarning("Connection {GuildId}: Received null payload {Payload}", Guild.Id, textMessageEventArgs);
                return Task.CompletedTask;
            }

            _logger.LogTrace("Connection {GuildId}: Received payload {Payload}", Guild.Id, payload);
            switch (payload.OpCode)
            {
                case VoiceOpCode.Hello:
                    ConnectionState = ConnectionState.Identify;
                    return SendIdentifyAsync(((JObject)payload.Data!).ToDiscordObject<VoiceHelloPayload>());
                case VoiceOpCode.Ready:
                    ConnectionState = ConnectionState.SelectProtocol;
                    return SendSelectProtocolAsync(((JObject)payload.Data!).ToDiscordObject<VoiceReadyPayload>());
                case VoiceOpCode.SessionDescription:
                    ConnectionState = ConnectionState.Heartbeating;
                    _voiceStateUpdateTcs.SetResult();
                    return Task.CompletedTask;
                case VoiceOpCode.Resumed:
                    ConnectionState = ConnectionState.Heartbeating;
                    return Task.CompletedTask;
                case VoiceOpCode.HeartbeatAck:
                    return HandleHeartbeat((long)payload.Data!);
                case VoiceOpCode.Speaking:
                    return UserSpeakingAsync(((JObject)payload.Data!).ToDiscordObject<VoiceSpeakingCommand>());
                case VoiceOpCode.ClientConnected:
                    return ClientConnectedAsync(((JObject)payload.Data!).ToDiscordObject<VoiceUserJoinPayload>());
                case VoiceOpCode.ClientDisconnect:
                    return ClientDisconnectAsync((ulong)((JObject)payload.Data!)["user_id"]!);
                default:
                    _logger.LogWarning("Connection {GuildId}: Received unknown/unimplemented payload. Please update VoiceLink or open an issue about this! Payload: {Payload}", Guild.Id, payload);
                    return Task.CompletedTask;
            }
        }

        private async Task StartHeartbeatAsync(VoiceHelloPayload helloPayload)
        {
            _logger.LogDebug("Connection {GuildId}: Starting heartbeat with a {HeartbeatInterval:N0}ms timer.", Guild.Id, helloPayload.HeartbeatInterval);
            PeriodicTimer heartbeatTimer = new(TimeSpan.FromMilliseconds(helloPayload.HeartbeatInterval));
            while (await heartbeatTimer.WaitForNextTickAsync() && ConnectionState is not ConnectionState.None)
            {
                if (_heartbeatQueue.Count > Extension.Configuration.MaxHeartbeatQueueSize)
                {
                    _logger.LogError("Connection {GuildId}: Heartbeat queue is too large ({MaxHeartbeat}), disconnecting and reconnecting.", Guild.Id, Extension.Configuration.MaxHeartbeatQueueSize);
                    await ReconnectAsync();
                    return;
                }

                long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _heartbeatQueue.Enqueue(unixTimestamp);
                _logger.LogTrace("Connection {GuildId}: Sending heartbeat {UnixTimestamp}", Guild.Id, unixTimestamp);
                await _webSocketClient!.SendMessageAsync(DiscordJson.SerializeObject(new VoiceGatewayDispatch(VoiceOpCode.Heartbeat, unixTimestamp)));
            }
        }

        private Task HandleHeartbeat(long heartbeat)
        {
            if (_heartbeatQueue.TryDequeue(out long unixTimestamp))
            {
                if (heartbeat != unixTimestamp)
                {
                    _logger.LogError("Connection {GuildId}: Heartbeat mismatch, disconnecting and reconnecting.", Guild.Id);
                    return ReconnectAsync();
                }

                Ping = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - unixTimestamp;
            }
            else
            {
                _logger.LogError("Connection {GuildId}: Heartbeat queue is empty, disconnecting and reconnecting.", Guild.Id);
                return ReconnectAsync();
            }

            return Task.CompletedTask;
        }

        private Task SendIdentifyAsync(VoiceHelloPayload helloPayload)
        {
            _ = StartHeartbeatAsync(helloPayload);
            return _webSocketClient!.SendMessageAsync(DiscordJson.SerializeObject(new VoiceGatewayDispatch(VoiceOpCode.Identify, new VoiceIdentifyCommand(
                Channel.Guild.Id,
                User.Id,
                _voiceStateUpdateEventArgs!.SessionId,
                _voiceServerUpdateEventArgs!.VoiceToken
            ))));
        }

        private async Task SendSelectProtocolAsync(VoiceReadyPayload voiceReadyPayload)
        {
            // Ip discovery here
            _udpClient = new(voiceReadyPayload.Ip, voiceReadyPayload.Port);

            byte[] ipDiscovery = new DiscordIPDiscovery(0x01, 70, voiceReadyPayload.Ssrc, string.Empty, default);
            await _udpClient.SendAsync(ipDiscovery, ipDiscovery.Length);

            DiscordIPDiscovery reply = (await _udpClient.ReceiveAsync()).Buffer.AsSpan();
            _logger.LogDebug("Connection {GuildId}: Received IP discovery reply {Reply}", Guild.Id, reply);


            await _webSocketClient!.SendMessageAsync(DiscordJson.SerializeObject(new VoiceGatewayDispatch(VoiceOpCode.SelectProtocol, new VoiceSelectProtocolCommand(
                "udp",
                new VoiceSelectProtocolCommandData(
                    reply.Address,
                    reply.Port,
                    "xsalsa20_poly1305"
                )
            ))));
        }

        private async Task UserSpeakingAsync(VoiceSpeakingCommand voiceSpeakingCommand)
        {
            if (!_currentUsers.TryGetValue(voiceSpeakingCommand.UserId, out VoiceLinkUser? user))
            {
                user = new VoiceLinkUser(await Extension.Client.GetUserAsync(voiceSpeakingCommand.UserId), this, voiceSpeakingCommand.SSRC);
                _currentUsers.TryAdd(voiceSpeakingCommand.UserId, user);
            }

            user.VoiceIndication = voiceSpeakingCommand.Speaking;
            user.Ssrc = voiceSpeakingCommand.SSRC;

            // Fire and forget the event, as we don't need to wait for the event handlers to complete.
            // This also prevents the blocking of the voice gateway.
            _ = Extension._userSpeaking.InvokeAsync(Extension, new(this, voiceSpeakingCommand, user));
        }

        private async Task ClientConnectedAsync(VoiceUserJoinPayload userJoinPayload)
        {
            VoiceLinkUser voiceUser = new(await Extension.Client.GetUserAsync(userJoinPayload.UserId), this, userJoinPayload.Ssrc);
            _currentUsers.TryAdd(userJoinPayload.UserId, voiceUser);
            _ = Extension._userConnected.InvokeAsync(Extension, new(this, voiceUser));
        }

        private async Task ClientDisconnectAsync(ulong userId)
        {
            if (!_currentUsers.TryRemove(userId, out VoiceLinkUser? voiceUser))
            {
                voiceUser = new VoiceLinkUser(await Extension.Client.GetUserAsync(userId), this, 0);
            }

            _ = Extension._userDisconnected.InvokeAsync(Extension, new(this, voiceUser));
        }
    }
}
