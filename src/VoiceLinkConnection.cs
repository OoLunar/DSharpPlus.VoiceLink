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

        public Task ConnectAsync()
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
            _webSocketClient.Connected += ConnectedAsync;
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
            return _webSocketClient.ConnectAsync(endpointUri);
        }

        public async Task DisconnectAsync()
        {
            _logger.LogDebug("Connection {GuildId}: Disconnecting", Guild.Id);
            ConnectionState = ConnectionState.None;
            _voiceServerUpdateEventArgs = null;
            _voiceStateUpdateEventArgs = null;
            _voiceStateUpdateTcs = new();
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

        public async Task ResumeAsync()
        {
            _logger.LogDebug("Connection {GuildId}: Attempting to resume...", Guild.Id);
            ConnectionState = ConnectionState.Resuming;
            await _webSocketClient!.ConnectAsync(_endpointUri);
            await _webSocketClient.SendMessageAsync(DiscordJson.SerializeObject(new VoiceGatewayDispatch(VoiceOpCode.Resume, new DiscordVoiceResumingCommand(_voiceServerUpdateEventArgs!.Guild.Id, _voiceStateUpdateEventArgs!.SessionId, _voiceServerUpdateEventArgs.VoiceToken))));
        }

        public Task ConnectedAsync(IWebSocketClient webSocketClient, SocketEventArgs eventArgs)
        {
            _logger.LogDebug("Connection {GuildId}: Connected to endpoint {Endpoint}", Guild.Id, _endpointUri);
            return Task.CompletedTask;
        }

        public Task DisconnectedAsync(IWebSocketClient webSocketClient, SocketCloseEventArgs eventArgs)
        {
            _logger.LogDebug("Connection {GuildId}: Disconnected from endpoint {Endpoint}. Error code {Code}: {Message}", Guild.Id, _endpointUri, eventArgs.CloseCode, eventArgs.CloseMessage);
            return Task.CompletedTask;
        }

        public Task ExceptionThrownAsync(IWebSocketClient webSocketClient, SocketErrorEventArgs eventArgs)
        {
            _logger.LogError(eventArgs.Exception, "Connection {GuildId}: Exception thrown", Guild.Id);
            return Task.CompletedTask;
        }

        public Task MessageReceivedAsync(IWebSocketClient webSocketClient, SocketMessageEventArgs eventArgs)
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
                case VoiceOpCode.Hello when ConnectionState is ConnectionState.None:
                    ConnectionState = ConnectionState.Identify;
                    return SendIdentifyAsync(((JObject)payload.Data!).ToDiscordObject<VoiceHelloPayload>());
                case VoiceOpCode.Ready when ConnectionState is ConnectionState.Identify:
                    ConnectionState = ConnectionState.SelectProtocol;
                    return SendSelectProtocolAsync(((JObject)payload.Data!).ToDiscordObject<VoiceReadyPayload>());
                case VoiceOpCode.SessionDescription when ConnectionState is ConnectionState.SelectProtocol:
                    ConnectionState = ConnectionState.Heartbeating;
                    _voiceStateUpdateTcs.SetResult();
                    break;
                case VoiceOpCode.Resumed when ConnectionState is ConnectionState.Resuming:
                    ConnectionState = ConnectionState.Heartbeating;
                    break;
                case VoiceOpCode.HeartbeatAck when ConnectionState is ConnectionState.Heartbeating:
                    return HandleHeartbeat((long)payload.Data!);
            }

            return Task.CompletedTask;
        }

        public void StartHeartbeat(VoiceHelloPayload helloPayload) => Task.Run(async () =>
        {
            _logger.LogDebug("Connection {GuildId}: Starting heartbeat with a {HeartbeatInterval:N0}ms timer.", Guild.Id, helloPayload.HeartbeatInterval);
            PeriodicTimer heartbeatTimer = new(TimeSpan.FromMilliseconds(helloPayload.HeartbeatInterval));
            while (await heartbeatTimer.WaitForNextTickAsync() && ConnectionState is not ConnectionState.None)
            {
                if (_heartbeatQueue.Count > Extension.Configuration.MaxHeartbeatQueueSize)
                {
                    _logger.LogError("Connection {GuildId}: Heartbeat queue is too large ({MaxHeartbeat}), disconnecting and reconnecting.", Guild.Id, Extension.Configuration.MaxHeartbeatQueueSize);
                    await DisconnectAsync();
                    return;
                }

                long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _heartbeatQueue.Enqueue(unixTimestamp);
                _logger.LogTrace("Connection {GuildId}: Sending heartbeat {UnixTimestamp}", Guild.Id, unixTimestamp);
                await _webSocketClient!.SendMessageAsync(DiscordJson.SerializeObject(new VoiceGatewayDispatch(VoiceOpCode.Heartbeat, unixTimestamp)));
            }
        });

        public Task HandleHeartbeat(long heartbeat)
        {
            if (_heartbeatQueue.TryDequeue(out long unixTimestamp))
            {
                _logger.LogTrace("Connection {GuildId}: Received heartbeat {UnixTimestamp}", Guild.Id, unixTimestamp);
                if (heartbeat != unixTimestamp)
                {
                    _logger.LogError("Connection {GuildId}: Heartbeat mismatch, disconnecting and reconnecting.", Guild.Id);
                    return DisconnectAsync();
                }

                Ping = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - unixTimestamp;
            }
            else
            {
                _logger.LogError("Connection {GuildId}: Heartbeat queue is empty, disconnecting and reconnecting.", Guild.Id);
                return DisconnectAsync();
            }

            return Task.CompletedTask;
        }

        public Task SendIdentifyAsync(VoiceHelloPayload helloPayload)
        {
            StartHeartbeat(helloPayload);
            return _webSocketClient!.SendMessageAsync(DiscordJson.SerializeObject(new VoiceGatewayDispatch(VoiceOpCode.Identify, new VoiceIdentifyCommand(
                Channel.Guild.Id,
                User.Id,
                _voiceStateUpdateEventArgs!.SessionId,
                _voiceServerUpdateEventArgs!.VoiceToken
            ))));
        }

        public async Task SendSelectProtocolAsync(VoiceReadyPayload voiceReadyPayload)
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
    }
}
