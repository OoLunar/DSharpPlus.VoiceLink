using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.VoiceLink.Commands;
using DSharpPlus.VoiceLink.Enums;
using DSharpPlus.VoiceLink.Payloads;
using DSharpPlus.VoiceLink.Rtp;
using DSharpPlus.VoiceLink.Sodium;
using DSharpPlus.VoiceLink.VoiceEncrypters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.VoiceLink
{
    public sealed partial class VoiceLinkConnection(VoiceLinkExtension extension, DiscordChannel channel, VoiceState voiceState)
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

            _logger.LogDebug("Connection {GuildId}: Connecting to {Endpoint}...", Guild.Id, _endpoint);
            await _webSocket.ConnectAsync(_endpoint, cancellationToken);
            _logger.LogDebug("Connection {GuildId}: Connected to {Endpoint}.", Guild.Id, _endpoint);

            // Start receiving events
            _logger.LogDebug("Connection {GuildId}: Starting voice gateway loop...", Guild.Id);
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
                    if (utf8JsonReader.TokenType != JsonTokenType.PropertyName || utf8JsonReader.GetString() != "op" || !utf8JsonReader.Read())
                    {
                        continue;
                    }

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
                    _logger.LogTrace("Connection {GuildId}: Received {VoiceOpCode}.", Guild.Id, voiceOpCode);
                    if (_voiceGatewayHandlers.TryGetValue(voiceOpCode, out VoiceGatewayHandler? handler))
                    {
                        await handler(this, readResult);
                        continue;
                    }

                    // Unknown voice op code, likely undocumented.
                    VoiceGatewayDispatch voiceGatewayDispatch = await _websocketPipe.Reader.ParseAsync<VoiceGatewayDispatch>(readResult);
                    _logger.LogWarning("Connection {GuildId}: Unknown voice op code {VoiceOpCode}: {VoiceGatewayDispatch}", Guild.Id, voiceGatewayDispatch.OpCode, voiceGatewayDispatch.Data);
                }
                catch (VoiceLinkWebsocketClosedException) when (_webSocket.State is WebSocketState.Connecting)
                {
                    // In theory this means that resuming failed. We should just restart the connection entirely.
                    _logger.LogWarning("Connection {GuildId}: Websocket closed, restarting the connection entirely...", Guild.Id);
                    await ReconnectAsync();
                    return;
                }
                catch (VoiceLinkWebsocketClosedException) when (_webSocket.State is not WebSocketState.Open)
                {
                    // Attempt to reconnect and resume. If that fails then restart the connection entirely.
                    _logger.LogWarning("Connection {GuildId}: Websocket closed, attempting to resume...", Guild.Id);
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
                    _logger.LogError("Connection {GuildId}: Heartbeat queue is too large ({MaxHeartbeat}), disconnecting and reconnecting...", Guild.Id, Extension.Configuration.MaxHeartbeatQueueSize);
                    await ReconnectAsync();
                    return;
                }

                long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _heartbeatQueue.Enqueue(unixTimestamp);
                _logger.LogTrace("Connection {GuildId}: Sending heartbeat {UnixTimestamp}...", Guild.Id, unixTimestamp);

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

                // RTP Header. Additionally packets with a length of 48 bytes are spammed to us, however we don't know what they are.
                // We suspect them to be RTCP receiver reports, however we cannot find a way to decode them.
                if (!RtpUtilities.IsRtpHeader(udpReceiveResult.Buffer))
                {
                    if (!RtcpUtilities.IsRtcpReceiverReport(udpReceiveResult.Buffer))
                    {
                        _logger.LogWarning("Connection {GuildId}: Received an unknown packet with a length of {Length} bytes. It is not an RTP Header or a keep alive packet. Skipping.", Guild.Id, udpReceiveResult.Buffer.Length);
                        continue;
                    }

                    RtcpHeader header = RtcpUtilities.DecodeHeader(udpReceiveResult.Buffer);
                    Memory<byte> data = udpReceiveResult.Buffer;
                    Memory<byte> nonce = ArrayPool<byte>.Shared.Rent(24);
                    data[^4..].CopyTo(nonce);

                    Memory<byte> payload = data[8..^4];
                    Memory<byte> result = ArrayPool<byte>.Shared.Rent(udpReceiveResult.Buffer.Length - SodiumXSalsa20Poly1305.MacSize);
                    if (SodiumXSalsa20Poly1305.Decrypt(payload.Span, _secretKey, nonce.Span, result.Span) != 0)
                    {
                        //_logger.LogWarning("Connection {GuildId}: Failed to decrypt rtcp receiver report packet, skipping.", Guild.Id);
                        continue;
                    }

                    RtcpReceiverReportPacket receiverReportPacket = new(header, payload.Span);
                    _logger.LogTrace("Connection {GuildId}: Received RTCP receiver report packet: {ReceiverReportPacket}", Guild.Id, receiverReportPacket);
                    ArrayPool<byte>.Shared.Return(nonce.ToArray());
                    ArrayPool<byte>.Shared.Return(result.ToArray());
                    continue;
                }

                RtpHeader rtpHeader = RtpUtilities.DecodeHeader(udpReceiveResult.Buffer);
                if (rtpHeader.PayloadType != 120)
                {
                    _logger.LogWarning("Connection {GuildId}: Received an unknown packet with a payload type of {PayloadType}. Skipping.", Guild.Id, rtpHeader.PayloadType);
                    continue;
                }

                if (rtpHeader.HasMarker)
                {
                    // All clients send a marker bit when they first connect. For now we're just going to ignore this.
                    continue;
                }

                if (!_speakers.TryGetValue(rtpHeader.Ssrc, out VoiceLinkUser? voiceLinkUser))
                {
                    // Create a new user if we don't have one
                    // We're explicitly passing a null member, however the dev should never expect this to
                    // be null as the speaking event should always fire once we receive both the user and the ssrc.
                    // TL;DR, this is to ensure we never lose any audio data.
                    voiceLinkUser = new(this, rtpHeader.Ssrc, null!);
                    _speakers.Add(rtpHeader.Ssrc, voiceLinkUser);
                }

                // Decrypt the audio
                int decryptedBufferSize = _voiceEncrypter.GetDecryptedSize(udpReceiveResult.Buffer.Length);
                byte[] decryptedAudioArr = ArrayPool<byte>.Shared.Rent(decryptedBufferSize);
                Memory<byte> decryptedAudio = decryptedAudioArr.AsMemory(0, decryptedBufferSize);

                if (!_voiceEncrypter.TryDecryptOpusPacket(voiceLinkUser, udpReceiveResult.Buffer, _secretKey, decryptedAudio.Span))
                {
                    _logger.LogWarning("Connection {GuildId}: Failed to decrypt audio from {Ssrc}, skipping.", Guild.Id, rtpHeader.Ssrc);
                    ArrayPool<byte>.Shared.Return(decryptedAudioArr);
                    continue;
                }

                // Strip any RTP header extensions. See https://www.rfc-editor.org/rfc/rfc3550#section-5.3.1
                // Discord currently uses a generic profile marker of [0xbe, 0xde], see
                // https://www.rfc-editor.org/rfc/rfc8285#section-4.2
                if (rtpHeader.HasExtension)
                {
                    ushort extensionLength = RtpUtilities.GetHeaderExtensionLength(decryptedAudio.Span);
                    decryptedAudio = decryptedAudio[(4 + 4 * extensionLength)..];
                }

                // TODO: Handle FEC (Forward Error Correction) aka packet loss.
                // * https://tools.ietf.org/html/rfc5109
                bool hasDataLoss = voiceLinkUser.UpdateSequence(rtpHeader.Sequence);

                // Decode the audio
                DecodeOpusAudio(decryptedAudio, voiceLinkUser, hasDataLoss);
                ArrayPool<byte>.Shared.Return(decryptedAudio);
                await voiceLinkUser._audioPipe.Writer.FlushAsync(_cancellationTokenSource.Token);

                static void DecodeOpusAudio(ReadOnlySpan<byte> opusPacket, VoiceLinkUser voiceLinkUser, bool hasPacketLoss = false)
                {
                    // Calculate the frame size and buffer size
                    const int sampleRate = 48000; // 48 kHz
                    const double frameDuration = 0.020; // 20 milliseconds
                    const int frameSize = (int)(sampleRate * frameDuration); // 960 samples
                    const int bufferSize = frameSize * 2 * sizeof(short); // Stereo audio + opus PCM units are 16 bits

                    // Allocate the buffer for the PCM data
                    Span<byte> audioBuffer = voiceLinkUser._audioPipe.Writer.GetSpan(bufferSize);

                    // Decode the Opus packet
                    voiceLinkUser._opusDecoder.Decode(opusPacket, audioBuffer, frameSize, hasPacketLoss);

                    // Write the audio to the pipe
                    voiceLinkUser._audioPipe.Writer.Advance(bufferSize);
                }
            }
        }
    }
}
