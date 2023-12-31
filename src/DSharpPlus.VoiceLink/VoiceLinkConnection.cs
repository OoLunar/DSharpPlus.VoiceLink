using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.VoiceLink.Commands;
using DSharpPlus.VoiceLink.Enums;
using DSharpPlus.VoiceLink.EventArgs;
using DSharpPlus.VoiceLink.Payloads;
using DSharpPlus.VoiceLink.Rtp;
using DSharpPlus.VoiceLink.VoiceEncrypters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.VoiceLink
{
    public sealed class VoiceLinkConnection(VoiceLinkExtension extension, DiscordChannel channel, VoiceState voiceState)
    {
        public VoiceState VoiceState { get; init; } = voiceState;
        public VoiceLinkExtension Extension { get; init; } = extension;
        public DiscordChannel Channel { get; init; } = channel;
        public DiscordGuild Guild => Channel.Guild;
        public DiscordClient Client => Extension.Client;
        public DiscordUser User => Client.CurrentUser;
        public DiscordMember Member => Guild.CurrentMember;
        public IReadOnlyDictionary<uint, VoiceLinkUser> Speakers => _speakers;
        public TimeSpan WebsocketPing { get; private set; }
        public TimeSpan UdpPing { get; private set; }

        // Audio processing
        private ILogger<VoiceLinkConnection> _logger { get; init; } = extension.Configuration.ServiceProvider.GetRequiredService<ILogger<VoiceLinkConnection>>();
        private CancellationTokenSource _cancellationTokenSource { get; init; } = new();
        private Dictionary<uint, VoiceLinkUser> _speakers { get; init; } = [];
        private IVoiceEncrypter _voiceEncrypter { get; init; } = extension.Configuration.VoiceEncrypter;
        private byte[] _secretKey { get; set; } = [];
        private Pipe _audioPipe { get; init; } = new();

        // Networking
        private ClientWebSocket _webSocket { get; init; } = new();
        private Pipe _websocketPipe { get; init; } = new();
        private UdpClient _udpClient { get; init; } = new();
        private ConcurrentQueue<long> _heartbeatQueue { get; init; } = new();
        private Uri? _endpoint { get; set; }
        private string? _sessionId { get; set; }
        private string? _voiceToken { get; set; }
        private SemaphoreSlim _readySemaphore { get; init; } = new(0, 1);

        public async ValueTask ReconnectAsync()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.TryReset();

            _webSocket.Dispose();
            _webSocket.Abort();

            _websocketPipe.Reader.CancelPendingRead();
            _websocketPipe.Writer.CancelPendingFlush();
            _websocketPipe.Reader.Complete();
            _websocketPipe.Writer.Complete();
            _websocketPipe.Reset();

            _udpClient.Close();
            _speakers.Clear();
            _heartbeatQueue.Clear();
            WebsocketPing = TimeSpan.Zero;

            Extension._connections.TryRemove(Guild.Id, out _);
            VoiceLinkPendingConnection pendingConnection = new();
            Extension._pendingConnections.TryAdd(Guild.Id, pendingConnection);

#pragma warning disable CS0618 // Type or member is obsolete
            await Client.SendPayloadAsync(GatewayOpCode.VoiceStateUpdate, new VoiceLinkStateCommand()
            {
                GuildId = Channel.Guild.Id,
                ChannelId = Channel.Id,
                UserId = Client.CurrentUser.Id,
                Member = Optional.FromNoValue<DiscordMember>(),
                SessionId = string.Empty,
                Deaf = VoiceState.HasFlag(VoiceState.ServerDeafened),
                Mute = VoiceState.HasFlag(VoiceState.ServerMuted),
                SelfDeaf = VoiceState.HasFlag(VoiceState.UserDeafened),
                SelfMute = VoiceState.HasFlag(VoiceState.UserMuted),
                SelfStream = Optional.FromNoValue<bool>(),
                SelfVideo = false,
                Suppress = false,
                RequestToSpeakTimestamp = null
            });
#pragma warning restore CS0618 // Type or member is obsolete

            await pendingConnection.Semaphore.WaitAsync();
            await pendingConnection.Semaphore.WaitAsync();
            Extension._pendingConnections.TryRemove(Channel.Guild.Id, out _);

            await InitializeAsync(pendingConnection.VoiceStateUpdateEventArgs!, pendingConnection.VoiceServerUpdateEventArgs!, _cancellationTokenSource.Token);
        }

        public async ValueTask InitializeAsync(VoiceStateUpdateEventArgs voiceStateUpdateEventArgs, VoiceServerUpdateEventArgs voiceServerUpdateEventArgs, CancellationToken cancellationToken = default)
        {
            // Setup endpoint
            if (voiceServerUpdateEventArgs.Endpoint is null)
            {
                throw new InvalidOperationException($"The {nameof(voiceServerUpdateEventArgs.Endpoint)} argument is null. A null endpoint means that the voice server allocated has gone away and is trying to be reallocated. You should attempt to disconnect from the currently connected voice server, and not attempt to reconnect until a new voice server is allocated.");
            }

            _sessionId = voiceStateUpdateEventArgs.SessionId;
            _voiceToken = voiceServerUpdateEventArgs.VoiceToken;

            // Resolve if the endpoint is a ip address or a hostname
            string? endpointHost;
            int endpointPort;
            int endpointIndex = voiceServerUpdateEventArgs.Endpoint.LastIndexOf(':');
            if (endpointIndex != -1)
            {
                endpointHost = voiceServerUpdateEventArgs.Endpoint[..endpointIndex];
                endpointPort = int.Parse(voiceServerUpdateEventArgs.Endpoint[(endpointIndex + 1)..], CultureInfo.InvariantCulture);
            }
            else
            {
                endpointHost = voiceServerUpdateEventArgs.Endpoint;
                endpointPort = 443;
            }

            _endpoint = new UriBuilder()
            {
                Scheme = "wss",
                Host = endpointHost,
                Port = endpointPort,
                Query = $"v=4&encoding=json"
            }.Uri;

            _logger.LogDebug("Connection {GuildId}: Connecting to {Endpoint}", Guild.Id, _endpoint);
            await _webSocket.ConnectAsync(_endpoint, cancellationToken);
            _logger.LogDebug("Connection {GuildId}: Connected to {Endpoint}", Guild.Id, _endpoint);

            // Start receiving events
            _logger.LogDebug("Connection {GuildId}: Starting voice gateway loop", Guild.Id);
            _ = StartVoiceGatewayAsync();

            // Wait until we can start sending data.
            await _readySemaphore.WaitAsync(cancellationToken);
        }

        private async Task StartVoiceGatewayAsync()
        {
            static VoiceOpCode ParseVoiceOpCode(ReadResult readResult)
            {
                Utf8JsonReader utf8JsonReader = new(readResult.Buffer);
                while (utf8JsonReader.Read())
                {
                    if (utf8JsonReader.TokenType != JsonTokenType.PropertyName || utf8JsonReader.GetString() != "op")
                    {
                        continue;
                    }

                    utf8JsonReader.Read();
                    return (VoiceOpCode)utf8JsonReader.GetInt32();
                }

                throw new InvalidOperationException("Could not find op code.");
            }

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await _webSocket.ReadAsync(_websocketPipe.Writer, _cancellationTokenSource.Token);
                    ReadResult readResult = await _websocketPipe.Reader.ReadAsync(_cancellationTokenSource.Token);
                    VoiceOpCode voiceOpCode = ParseVoiceOpCode(readResult);
                    _logger.LogTrace("Connection {GuildId}: Received {VoiceOpCode}", Guild.Id, voiceOpCode);

                    // TODO: Maybe dictionary of delegates?
                    // Dictionary<VoiceOpCode, Func<object, Task>> handlers = new();
                    // Might be able to make use of JsonTypeInfo so we can have a concrete type for the data.
                    switch (voiceOpCode)
                    {
                        case VoiceOpCode.Hello:
                            // Start heartbeat
                            _logger.LogDebug("Connection {GuildId}: Starting heartbeat", Guild.Id);
                            _ = SendHeartbeatAsync((await _websocketPipe.Reader.ParseAsync<VoiceGatewayDispatch<VoiceHelloPayload>>(readResult)).Data);

                            // Send Identify
                            _logger.LogTrace("Connection {GuildId}: Sending identify", Guild.Id);
                            await _webSocket.SendAsync(new VoiceGatewayDispatch()
                            {
                                OpCode = VoiceOpCode.Identify,
                                Data = new VoiceIdentifyCommand()
                                {
                                    ServerId = Guild.Id,
                                    SessionId = _sessionId!,
                                    Token = _voiceToken!,
                                    UserId = User.Id
                                }
                            }, _cancellationTokenSource.Token);
                            break;
                        case VoiceOpCode.Ready:
                            VoiceReadyPayload voiceReadyPayload = (await _websocketPipe.Reader.ParseAsync<VoiceGatewayDispatch<VoiceReadyPayload>>(readResult)).Data;

                            // Insert our SSRC code
                            _logger.LogDebug("Connection {GuildId}: Bot's SSRC code is {Ssrc}", Guild.Id, voiceReadyPayload.Ssrc);
                            _speakers.Add(voiceReadyPayload.Ssrc, new(this, voiceReadyPayload.Ssrc, Member));

                            // Setup UDP while also doing ip discovery
                            _logger.LogDebug("Connection {GuildId}: Setting up UDP, sending ip discovery", Guild.Id);
                            byte[] ipDiscovery = new DiscordIPDiscovery(0x01, 70, voiceReadyPayload.Ssrc, string.Empty, default);
                            _udpClient.Connect(voiceReadyPayload.Ip, voiceReadyPayload.Port);
                            await _udpClient.SendAsync(ipDiscovery, _cancellationTokenSource.Token);

                            // Receive IP Discovery Response
                            UdpReceiveResult result = await _udpClient.ReceiveAsync(_cancellationTokenSource.Token);
                            if (result.Buffer.Length != 74)
                            {
                                throw new InvalidOperationException("Received invalid IP Discovery Response.");
                            }

                            DiscordIPDiscovery reply = result.Buffer;
                            _logger.LogDebug("Connection {GuildId}: Received ip discovery response {Reply}", Guild.Id, reply);
                            _logger.LogTrace("Connection {GuildId}: Sending select protocol", Guild.Id);
                            await _webSocket.SendAsync<VoiceGatewayDispatch>(new()
                            {
                                OpCode = VoiceOpCode.SelectProtocol,
                                Data = new VoiceSelectProtocolCommand()
                                {
                                    Protocol = "udp",
                                    Data = new VoiceSelectProtocolCommandData()
                                    {
                                        Address = reply.Address,
                                        Port = reply.Port,
                                        Mode = _voiceEncrypter.Name
                                    }
                                }
                            }, _cancellationTokenSource.Token);
                            break;
                        case VoiceOpCode.SessionDescription:
                            VoiceSessionDescriptionPayload sessionDescriptionPayload = (await _websocketPipe.Reader.ParseAsync<VoiceGatewayDispatch<VoiceSessionDescriptionPayload>>(readResult)).Data;
                            _secretKey = sessionDescriptionPayload.SecretKey.ToArray();
                            _ = ReceiveAudioLoopAsync();
                            _readySemaphore.Release();
                            await Extension._connectionCreated.InvokeAsync(Extension, new VoiceLinkConnectionEventArgs(this));
                            break;
                        case VoiceOpCode.HeartbeatAck:
                            long heartbeat = (await _websocketPipe.Reader.ParseAsync<VoiceGatewayDispatch<long>>(readResult)).Data;
                            if (_heartbeatQueue.TryDequeue(out long unixTimestamp))
                            {
                                WebsocketPing = TimeSpan.FromMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - unixTimestamp);
                            }
                            else
                            {
                                _logger.LogError("Connection {GuildId}: Received unexpected heartbeat, disconnecting and reconnecting.", Guild.Id);
                                await ReconnectAsync();
                            }
                            break;
                        case VoiceOpCode.Resumed:
                            _logger.LogInformation("Connection {GuildId}: Resumed", Guild.Id);
                            break;
                        case VoiceOpCode.ClientConnected:
                            VoiceUserJoinPayload voiceClientConnectedPayload = (await _websocketPipe.Reader.ParseAsync<VoiceGatewayDispatch<VoiceUserJoinPayload>>(readResult)).Data;
                            _logger.LogDebug("Connection {GuildId}: User {UserId} connected", Guild.Id, voiceClientConnectedPayload.UserId);
                            await Extension._userConnected.InvokeAsync(Extension, new VoiceLinkUserEventArgs()
                            {
                                Connection = this,
                                Member = await Guild.GetMemberAsync(voiceClientConnectedPayload.UserId)
                            });
                            break;
                        case VoiceOpCode.ClientDisconnect:
                            VoiceUserLeavePayload voiceClientDisconnectedPayload = (await _websocketPipe.Reader.ParseAsync<VoiceGatewayDispatch<VoiceUserLeavePayload>>(readResult)).Data;
                            _logger.LogDebug("Connection {GuildId}: User {UserId} disconnected", Guild.Id, voiceClientDisconnectedPayload.UserId);
                            if (_speakers.FirstOrDefault(x => x.Value.Member.Id == voiceClientDisconnectedPayload.UserId) is KeyValuePair<uint, VoiceLinkUser> kvp)
                            {
                                _speakers.Remove(kvp.Key);
                            }

                            await Extension._userDisconnected.InvokeAsync(Extension, new VoiceLinkUserEventArgs()
                            {
                                Connection = this,
                                Member = await Guild.GetMemberAsync(voiceClientDisconnectedPayload.UserId)
                            });
                            break;
                        case VoiceOpCode.Speaking:
                            VoiceSpeakingPayload voiceSpeakingPayload = (await _websocketPipe.Reader.ParseAsync<VoiceGatewayDispatch<VoiceSpeakingPayload>>(readResult)).Data;
                            _logger.LogTrace("Connection {GuildId}: User {UserId} is speaking", Guild.Id, voiceSpeakingPayload.UserId);
                            _speakers.Add(voiceSpeakingPayload.Ssrc, new(this, voiceSpeakingPayload.Ssrc, await Guild.GetMemberAsync(voiceSpeakingPayload.UserId)));
                            await Extension._userSpeaking.InvokeAsync(Extension, new VoiceLinkUserSpeakingEventArgs()
                            {
                                Connection = this,
                                Payload = voiceSpeakingPayload,
                                VoiceUser = _speakers[voiceSpeakingPayload.Ssrc]
                            });
                            break;
                        default:
                            VoiceGatewayDispatch voiceGatewayDispatch = await _websocketPipe.Reader.ParseAsync<VoiceGatewayDispatch>(readResult);
                            _logger.LogWarning("Connection {GuildId}: Unknown voice op code {VoiceOpCode}: {VoiceGatewayDispatch}", Guild.Id, voiceGatewayDispatch.OpCode, voiceGatewayDispatch.Data);
                            break;
                    }
                }
                catch (VoiceLinkWebsocketClosedException) when (_webSocket.State is WebSocketState.Connecting)
                {
                    // In theory this means that resuming failed. We should just restart the connection entirely.
                    _logger.LogWarning("Connection {GuildId}: Websocket closed, restarting the connection entirely.", Guild.Id);
                    await ReconnectAsync();
                    return;
                }
                catch (VoiceLinkWebsocketClosedException) when (_webSocket.State is not WebSocketState.Open)
                {
                    // Attempt to reconnect and resume. If that fails then restart the connection entirely.
                    _logger.LogWarning("Connection {GuildId}: Websocket closed, attempting to resume.", Guild.Id);
                    await _webSocket.ConnectAsync(_endpoint!, _cancellationTokenSource.Token);
                    await _webSocket.SendAsync(new DiscordVoiceResumingCommand()
                    {
                        ServerId = Guild.Id,
                        SessionId = _sessionId!,
                        Token = _voiceToken!
                    }, _cancellationTokenSource.Token);
                }
            }
        }

        private async Task SendHeartbeatAsync(VoiceHelloPayload voiceHelloPayload)
        {
            PeriodicTimer heartbeatTimer = new(TimeSpan.FromMilliseconds(voiceHelloPayload.HeartbeatInterval));
            while (await heartbeatTimer.WaitForNextTickAsync(_cancellationTokenSource.Token))
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

                await _webSocket.SendAsync(new VoiceGatewayDispatch()
                {
                    OpCode = VoiceOpCode.Heartbeat,
                    Data = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }, default);
            }
        }

        private async Task ReceiveAudioLoopAsync()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                // HEY. YOU. THE PERSON WHO'S TOUCHING THIS CODE.
                // This is a hotpath. Any modifications to this code should always
                // lead to equal or better performance. If you're not sure, don't touch it.
                UdpReceiveResult udpReceiveResult = await _udpClient.ReceiveAsync(_cancellationTokenSource.Token);
                if (udpReceiveResult.Buffer.Length == 8)
                {
                    // Keep alive packet
                    UdpPing = TimeSpan.FromMilliseconds(Unsafe.As<byte, long>(ref udpReceiveResult.Buffer[0]) - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                    // TODO: Maybe we should send a keep alive packet back?
                    continue;
                }

                // RTP Header
                if (!RtpUtilities.IsRtpHeader(udpReceiveResult.Buffer))
                {
                    _logger.LogWarning("Connection {GuildId}: Received an unknown packet with a length of {Length} bytes. It is not an RTP Header or a keep alive packet. Skipping.", Guild.Id, udpReceiveResult.Buffer.Length);
                    continue;
                }

                RtpHeader rtpHeader = RtpUtilities.DecodeHeader(udpReceiveResult.Buffer);
                if (!_speakers.TryGetValue(rtpHeader.Ssrc, out VoiceLinkUser? voiceLinkUser))
                {
                    _logger.LogWarning("Connection {GuildId}: Received audio from unknown user {Ssrc}, skipping.", Guild.Id, rtpHeader.Ssrc);
                    continue;
                }

                // Decrypt the audio
                byte[] decryptedAudio = ArrayPool<byte>.Shared.Rent(_voiceEncrypter.GetDecryptedSize(udpReceiveResult.Buffer.Length));
                if (!_voiceEncrypter.Decrypt(voiceLinkUser, udpReceiveResult.Buffer, _secretKey, decryptedAudio.AsSpan()))
                {
                    _logger.LogWarning("Connection {GuildId}: Failed to decrypt audio from {Ssrc}, skipping.", Guild.Id, rtpHeader.Ssrc);
                    continue;
                }

                // TODO: Handle FEC (Forward Error Correction) aka packet loss.
                // * https://tools.ietf.org/html/rfc5109

                // Decode the audio
                DecodeOpusAudio(decryptedAudio.AsSpan().TrimEnd((byte)'\0'), voiceLinkUser);
                ArrayPool<byte>.Shared.Return(decryptedAudio);
                await voiceLinkUser._audioPipe.Writer.FlushAsync(_cancellationTokenSource.Token);

                static void DecodeOpusAudio(ReadOnlySpan<byte> opusPacket, VoiceLinkUser voiceLinkUser)
                {
                    // Calculate the frame size and buffer size
                    int sampleRate = 48000; // 48 kHz
                    double frameDuration = 0.020; // 20 milliseconds
                    int frameSize = (int)(sampleRate * frameDuration); // 960 samples
                    int bufferSize = frameSize * 2; // Stereo audio

                    // Allocate the buffer for the PCM data
                    Span<short> pcmBuffer = new short[bufferSize];

                    // Decode the Opus packet
                    voiceLinkUser._opusDecoder.Decode(opusPacket, ref pcmBuffer, frameSize, false);

                    // Write the audio to the pipe
                    Span<byte> audioBuffer = voiceLinkUser._audioPipe.Writer.GetSpan(bufferSize * sizeof(short));
                    pcmBuffer.CopyTo(MemoryMarshal.Cast<byte, short>(audioBuffer));
                    voiceLinkUser._audioPipe.Writer.Advance(bufferSize * sizeof(short));
                }
            }
        }
    }
}
