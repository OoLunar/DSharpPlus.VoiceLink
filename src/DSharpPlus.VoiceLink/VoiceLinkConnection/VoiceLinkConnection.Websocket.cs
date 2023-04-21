using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.EventArgs;
using DSharpPlus.Net.Serialization;
using DSharpPlus.Net.WebSocket;
using DSharpPlus.VoiceLink.Commands;
using DSharpPlus.VoiceLink.Enums;
using DSharpPlus.VoiceLink.Payloads;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DSharpPlus.VoiceLink
{
    public sealed partial record VoiceLinkConnection
    {
        /// <summary>
        /// The ping is milliseconds between the client sending a heartbeat and receiving a heartbeat ack.
        /// </summary>
        public long Ping { get; private set; }

        internal VoiceStateUpdateEventArgs? _voiceStateUpdateEventArgs { get; set; }
        internal VoiceServerUpdateEventArgs? _voiceServerUpdateEventArgs { get; set; }
        internal TaskCompletionSource _voiceStateUpdateTcs { get; set; } = new();
        internal IWebSocketClient? _webSocketClient { get; set; }

        private readonly ConcurrentQueue<long> _heartbeatQueue = new();
        private Uri? _endpointUri { get; set; }
        private UdpClient? _udpClient { get; set; }

        private Task WebsocketDisconnectedAsync(IWebSocketClient webSocketClient, SocketCloseEventArgs eventArgs)
        {
            _logger.LogDebug("Connection {GuildId}: Disconnected from endpoint {Endpoint}. Error code {Code}: {Message}", Guild.Id, _endpointUri, eventArgs.CloseCode, eventArgs.CloseMessage);
            return Task.CompletedTask;
        }

        private Task WebsocketExceptionThrownAsync(IWebSocketClient webSocketClient, SocketErrorEventArgs eventArgs)
        {
            _logger.LogError(eventArgs.Exception, "Connection {GuildId}: Exception thrown", Guild.Id);
            return Task.CompletedTask;
        }

        private Task WebsocketMessageReceivedAsync(IWebSocketClient webSocketClient, SocketMessageEventArgs eventArgs)
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
                    return DiscordSendIdentifyAsync(((JObject)payload.Data!).ToDiscordObject<VoiceHelloPayload>());
                case VoiceOpCode.Ready:
                    ConnectionState = ConnectionState.SelectProtocol;
                    return DiscordSendSelectProtocolAsync(((JObject)payload.Data!).ToDiscordObject<VoiceReadyPayload>());
                case VoiceOpCode.SessionDescription:
                    ConnectionState = ConnectionState.Heartbeating;
                    _secretKey = ((JObject)payload.Data!)["secret_key"]!.ToObject<byte[]>()!;
                    _ = Task.Run(async () =>
                    {
                        while (true)
                        {
                            if (_udpClient!.Available != 0)
                            {
                                UdpReceiveResult result = await _udpClient!.ReceiveAsync();
                                ProcessPacket(result);
                            }
                            else
                            {
                                await Task.Delay(50);
                            }
                        }
                    });
                    _voiceStateUpdateTcs.SetResult();
                    return Task.CompletedTask;
                case VoiceOpCode.Resumed:
                    ConnectionState = ConnectionState.Heartbeating;
                    return Task.CompletedTask;
                case VoiceOpCode.HeartbeatAck:
                    return DiscordHandleHeartbeat((long)payload.Data!);
                case VoiceOpCode.Speaking:
                    return DiscordUserSpeakingAsync(((JObject)payload.Data!).ToDiscordObject<VoiceSpeakingCommand>());
                case VoiceOpCode.ClientConnected:
                    return ClientConnectedAsync((ulong)((JObject)payload.Data!)["user_id"]!);
                case VoiceOpCode.ClientDisconnect:
                    return ClientDisconnectAsync((ulong)((JObject)payload.Data!)["user_id"]!);
                default:
                    _logger.LogWarning("Connection {GuildId}: Received unknown/unimplemented payload. Please update VoiceLink or open an issue about this! Payload: {Payload}", Guild.Id, payload);
                    return Task.CompletedTask;
            }
        }

        private async Task DiscordStartHeartbeatAsync(VoiceHelloPayload helloPayload)
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

        private Task DiscordHandleHeartbeat(long heartbeat)
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

        private Task DiscordSendIdentifyAsync(VoiceHelloPayload helloPayload)
        {
            _ = DiscordStartHeartbeatAsync(helloPayload);
            return _webSocketClient!.SendMessageAsync(DiscordJson.SerializeObject(new VoiceGatewayDispatch(VoiceOpCode.Identify, new VoiceIdentifyCommand(
                Channel.Guild.Id,
                User.Id,
                _voiceStateUpdateEventArgs!.SessionId,
                _voiceServerUpdateEventArgs!.VoiceToken
            ))));
        }

        private async Task DiscordSendSelectProtocolAsync(VoiceReadyPayload voiceReadyPayload)
        {
            // We've received our ssrc, let's put it into the _currentUsers dictionary
            VoiceLinkUser = new VoiceLinkUser(this, voiceReadyPayload.Ssrc, Guild.CurrentMember);
            _currentUsers.TryAdd(voiceReadyPayload.Ssrc, VoiceLinkUser);

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
                    VoiceEncrypter.Name
                )
            ))));
        }

        private async Task DiscordResumeAsync()
        {
            _logger.LogDebug("Connection {GuildId}: Attempting to resume...", Guild.Id);
            ConnectionState = ConnectionState.Resuming;
            await _webSocketClient!.ConnectAsync(_endpointUri);
            await _webSocketClient.SendMessageAsync(DiscordJson.SerializeObject(new VoiceGatewayDispatch(VoiceOpCode.Resume, new DiscordVoiceResumingCommand(_voiceServerUpdateEventArgs!.Guild.Id, _voiceStateUpdateEventArgs!.SessionId, _voiceServerUpdateEventArgs.VoiceToken))));
        }

        private async Task DiscordUserSpeakingAsync(VoiceSpeakingCommand voiceSpeakingCommand)
        {
            if (!_currentUsers.TryGetValue(voiceSpeakingCommand.SSRC, out VoiceLinkUser? user))
            {
                user = new VoiceLinkUser(this, voiceSpeakingCommand.SSRC, await Extension.Client.GetUserAsync(voiceSpeakingCommand.UserId));
                _currentUsers.TryAdd(voiceSpeakingCommand.SSRC, user);
            }

            user.VoiceIndication = voiceSpeakingCommand.Speaking;

            // Fire and forget the event, as we don't need to wait for the event handlers to complete.
            // This also prevents the blocking of the voice gateway.
            _ = Extension._userSpeaking.InvokeAsync(Extension, new(this, voiceSpeakingCommand, user));
        }

        private async Task ClientConnectedAsync(ulong userId)
        {
            VoiceLinkUser voiceUser = new(this, 0, await Extension.Client.GetUserAsync(userId));
            _ = Extension._userConnected.InvokeAsync(Extension, new(this, voiceUser));
        }

        private async Task ClientDisconnectAsync(ulong userId)
        {
            VoiceLinkUser voiceUser = _currentUsers.Values.FirstOrDefault(voiceUser => voiceUser.User?.Id == userId) ?? new VoiceLinkUser(this, 0, await Extension.Client.GetUserAsync(userId));
            await voiceUser._audioPipe.Writer.CompleteAsync();
            _ = Extension._userDisconnected.InvokeAsync(Extension, new(this, voiceUser));
        }
    }
}
