using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.Net.Serialization;
using DSharpPlus.Net.WebSocket;
using DSharpPlus.VoiceLink.Commands;
using DSharpPlus.VoiceLink.Enums;
using DSharpPlus.VoiceLink.Opus;
using DSharpPlus.VoiceLink.Payloads;
using DSharpPlus.VoiceLink.Sodium;
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
        public EncryptionMode EncryptionMode { get; private set; }
        public OpusEncoder OpusEncoder { get; private set; } = OpusEncoder.Create(OpusSampleRate.Opus48000Hz, 2, OpusApplication.Audio);
        public OpusApplication OpusApplication
        {
            get => _opusApplication; private set
            {
                _opusApplication = value;
                OpusErrorCode errorCode = OpusEncoder.Control(OpusControlRequest.SetApplication, (int)value);
                if (errorCode != OpusErrorCode.Ok)
                {
                    throw new OpusException(errorCode);
                }
            }
        }

        private OpusApplication _opusApplication;

        /// <summary>
        /// The ping is milliseconds between the client sending a heartbeat and receiving a heartbeat ack.
        /// </summary>
        public long Ping { get; private set; }

        public DiscordGuild Guild => Channel.Guild;
        public DiscordMember? Member => User as DiscordMember;
        public IReadOnlyDictionary<ulong, VoiceLinkUser> CurrentUsers => _currentUsers;
        public PipeWriter? AudioPipe => _audioPipe?.Writer;

        internal VoiceStateUpdateEventArgs? _voiceStateUpdateEventArgs { get; set; }
        internal VoiceServerUpdateEventArgs? _voiceServerUpdateEventArgs { get; set; }
        internal TaskCompletionSource _voiceStateUpdateTcs { get; set; } = new();
        internal IWebSocketClient? _webSocketClient { get; set; }

        private readonly Pipe? _audioPipe = new();
        private readonly ConcurrentQueue<long> _heartbeatQueue = new();
        private readonly ILogger<VoiceLinkConnection> _logger;
        private readonly ConcurrentDictionary<ulong, VoiceLinkUser> _currentUsers = new();
        private Uri? _endpointUri { get; set; }
        private UdpClient? _udpClient { get; set; }

        private byte[] _secretKey { get; set; }
        private ushort _sequence { get; set; }
        private uint _timestamp { get; set; }
        private uint _ssrc { get; set; }
        private int _incrementalNumber { get; set; }

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

        public async Task StartSpeakingAsync()
        {
            await _webSocketClient!.SendMessageAsync(DiscordJson.SerializeObject(new VoiceGatewayDispatch(VoiceOpCode.Speaking, new VoiceSpeakingCommand(VoiceSpeakingIndicators.Microphone, 0, _ssrc, Extension.Client.CurrentUser.Id))));
            await SendVoicePacketAsync();
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
                    EncryptionMode = EncryptionMode.XSalsa20Poly1305;
                    _secretKey = ((JObject)payload.Data!)["secret_key"]!.ToObject<byte[]>()!;
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

        [SuppressMessage("Style", "IDE0047", Justification = "Apparently PEMDAS isn't well remembered.")]
        private async Task SendVoicePacketAsync()
        {
            unsafe int EncodeOpusPacket(ReadOnlySpan<byte> pcm, int sampleDuration, Span<byte> target) => OpusEncoder.Encode(pcm, sampleDuration, ref target);

            ReadOnlySpan<byte> GetIncrementalInteger()
            {
                byte[] array = BitConverter.GetBytes(_incrementalNumber);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(array);
                }

                _incrementalNumber = unchecked(_incrementalNumber + 1);
                return array;
            }

            // Referenced from VoiceNext:
            // internal int CalculateMaximumFrameSize() => 120 * (this.SampleRate / 1000);
            // internal int SampleCountToSampleSize(int sampleCount) => sampleCount * this.ChannelCount * 2 /* sizeof(int16_t) */;
            // audioFormat.SampleCountToSampleSize(audioFormat.CalculateMaximumFrameSize())
            // (120 * (this.SampleRate / 1000)) * this.ChannelCount * 2
            // Because the Opus data must be sent as 48,000hz and in 2 channels (stereo) we can hardcode the variables with their proper values
            // (120 * (48000 / 1000)) * 2 * 2
            // 120 * 48 * 4
            // 120 * 192
            // 23040
            // Where did the 120 or the 1000 come from? No clue. Hopefully they can stay hardcoded though.
            const int maximumOpusSize = 23040;
            // Additionally add 12 to 23040 as the Rtp header (12 bytes long) will always be included in the packet.
            const int maximumPacketSize = maximumOpusSize + 12;

            // The buffer we use for the Opus data.
            Memory<byte> opusPacket = new(new byte[maximumOpusSize]);

            // The buffer we use for the Rtp header and the *encrypted* Opus data.
            // The switch case is us factoring in the nonce size, which differs per encryption mode.
            Memory<byte> completePacket = new(new byte[EncryptionMode switch
            {
                // The Rtp header is the nonce.
                EncryptionMode.XSalsa20Poly1305 => maximumPacketSize,

                // A 32bit int (4 bytes) incremented per... packet(?)
                EncryptionMode.XSalsa20Poly1305Lite => maximumPacketSize + 4,

                // 24 (securely) randomly generated bytes
                EncryptionMode.XSalsa20Poly1305Suffix => maximumPacketSize + SodiumXSalsa20Poly1305.NonceSize,

                // What the fuck happened here, Discord sending us undocumented encryption types? They would never.
                _ => throw new NotImplementedException($"Unknown encryption mode selected: {EncryptionMode}")
            }]);

            ReadResult result = default;
            while (!result.IsCompleted && _audioPipe is not null)
            {
                result = await _audioPipe.Reader.ReadAsync();
                if (result.IsCanceled || result.Buffer.IsEmpty)
                {
                    await Task.Delay(50);
                    continue;
                }

                foreach (ReadOnlyMemory<byte> buffer in result.Buffer)
                {
                    /*
                        - `frameSize = buffer.Length * 4`
                        - `OpusEncoder.Encode` expects `framesize` as an `int`
                        This means `buffer.Length` will always need to be less than 1/4 of int.MaxValue
                    */
                    const int maxBufferLength = int.MaxValue / 4;
                    int currentBufferPosition = 0;
                    int nextBufferPosition = Math.Min(buffer.Length, maxBufferLength);
                    while (currentBufferPosition != buffer.Length)
                    {
                        // Encode Opus to the opusPacket buffer.
                        EncodeOpusPacket(buffer.Slice(currentBufferPosition, nextBufferPosition).Span, nextBufferPosition * 4, opusPacket.Span[12..]);

                        // Encode the RTP header to the packet.
                        RtpUtilities.EncodeHeader(_sequence, _timestamp, _ssrc, completePacket.Span[..12]);

                        // Encode Opus via Sodium and append it to the packet.
                        SodiumXSalsa20Poly1305.Encrypt(opusPacket.Span, new Span<byte>(_secretKey), EncryptionMode switch
                        {
                            EncryptionMode.XSalsa20Poly1305 => completePacket.Span[..12],
                            EncryptionMode.XSalsa20Poly1305Lite => GetIncrementalInteger(),
                            EncryptionMode.XSalsa20Poly1305Suffix => RandomNumberGenerator.GetBytes(24),
                            _ => throw new NotImplementedException($"Unknown encryption mode selected: {EncryptionMode}")
                        }, completePacket.Span[12..]);

                        // Send the packet to the voice gateway.
                        await _udpClient!.SendAsync(completePacket.ToArray(), completePacket.Length);

                        // Increment the sequence and timestamp.
                        _sequence = unchecked((ushort)(_sequence + 1));
                        _timestamp = unchecked((uint)(_timestamp + (nextBufferPosition * 4)));

                        // Increment the buffer position and calculate the next buffer position.
                        currentBufferPosition += nextBufferPosition;
                        nextBufferPosition = Math.Min(buffer.Length - currentBufferPosition, maxBufferLength);
                    }
                }
            }
        }
    }
}
