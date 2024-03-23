using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.VoiceLink.AudioDecoders;
using DSharpPlus.VoiceLink.Commands;
using DSharpPlus.VoiceLink.Enums;
using DSharpPlus.VoiceLink.Payloads;
using DSharpPlus.VoiceLink.Rtp;
using DSharpPlus.VoiceLink.VoiceEncrypters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.VoiceLink
{
    public sealed partial class VoiceLinkConnection
    {
        public VoiceState VoiceState { get; init; }
        public VoiceLinkExtension Extension { get; init; }
        public DiscordChannel Channel { get; init; }
        public DiscordGuild Guild => Channel.Guild;
        public DiscordClient Client => Extension.Client;
        public DiscordUser User => Client.CurrentUser;
        public DiscordMember Member => Guild.CurrentMember;
        public IReadOnlyDictionary<uint, VoiceLinkUser> Speakers => _speakers;
        public TimeSpan HeartbeatPing { get; private set; }

        // Audio processing
        private ILogger<VoiceLinkConnection> _logger { get; init; }
        private CancellationTokenSource _cancellationTokenSource { get; init; } = new();
        private Dictionary<uint, VoiceLinkUser> _speakers { get; init; } = [];
        private IVoiceEncrypter _voiceEncrypter { get; init; }
        private AudioDecoderFactory _audioDecoderFactory { get; init; }
        private byte[] _secretKey { get; set; } = [];
        private Pipe _audioPipe { get; init; } = new();

        // Networking
        private readonly Pipe _websocketPipe = new();
        private readonly UdpClient _udpClient = new();
        private readonly ConcurrentQueue<long> _heartbeatQueue = new();
        private readonly SemaphoreSlim _readySemaphore = new(0, 1);
        private ClientWebSocket _webSocket = new();
        private Uri? _endpoint { get; set; }
        private string? _sessionId { get; set; }
        private string? _voiceToken { get; set; }

        public VoiceLinkConnection(VoiceLinkExtension extension, DiscordChannel channel, VoiceState voiceState)
        {
            VoiceState = voiceState;
            Extension = extension;
            Channel = channel;
            _logger = extension.Configuration.ServiceProvider.GetRequiredService<ILogger<VoiceLinkConnection>>();
            _voiceEncrypter = extension.Configuration.VoiceEncrypter;
            _audioDecoderFactory = extension.Configuration.AudioDecoderFactory;
        }

        public async ValueTask DisconnectAsync()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            await Client.SendPayloadAsync(GatewayOpCode.VoiceStateUpdate, new VoiceLinkStateCommand()
            {
                GuildId = Channel.Guild.Id,
                ChannelId = null,
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

            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", _cancellationTokenSource.Token);
            _webSocket.Abort();
            _webSocket.Dispose();
            _udpClient.Close();

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.TryReset();

            _websocketPipe.Reader.CancelPendingRead();
            _websocketPipe.Writer.CancelPendingFlush();
            _websocketPipe.Reader.Complete();
            _websocketPipe.Writer.Complete();
            _websocketPipe.Reset();

            _speakers.Clear();
            _heartbeatQueue.Clear();
            HeartbeatPing = TimeSpan.Zero;

            await Extension._connectionDestroyed.InvokeAsync(Extension, new(this));
            Extension._connections.TryRemove(Guild.Id, out _);
        }

        public async ValueTask ReconnectAsync()
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", _cancellationTokenSource.Token);
            _udpClient.Close();

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.TryReset();

            _websocketPipe.Reader.CancelPendingRead();
            _websocketPipe.Writer.CancelPendingFlush();
            _websocketPipe.Reader.Complete();
            _websocketPipe.Writer.Complete();
            _websocketPipe.Reset();

            _speakers.Clear();
            _heartbeatQueue.Clear();
            HeartbeatPing = TimeSpan.Zero;

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
            VoiceOpCode ParseVoiceOpCode(ReadResult readResult)
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

                _logger.LogError("Connection {GuildId}: Op code was not included within the payload. Did the payload structure change?", Guild.Id);
                _logger.LogError("Connection {GuildId}: Payload:\n{Payload}", Guild.Id, Encoding.UTF8.GetString(readResult.Buffer));
                throw new InvalidOperationException("Op code was not included within the payload.");
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
                    VoiceGatewayDispatch voiceGatewayDispatch = _websocketPipe.Reader.Parse<VoiceGatewayDispatch>(readResult);
                    _logger.LogWarning("Connection {GuildId}: Unknown voice op code {VoiceOpCode}: {VoiceGatewayDispatch}", Guild.Id, voiceGatewayDispatch.OpCode, voiceGatewayDispatch.Data);
                }
                catch (VoiceLinkWebsocketClosedException) when (_webSocket.State is WebSocketState.Connecting)
                {
                    // In theory this means that resuming failed. We should just restart the connection entirely.
                    _logger.LogWarning("Connection {GuildId}: Websocket closed, restarting the connection entirely...", Guild.Id);
                    await ReconnectAsync();
                    return;
                }
                catch (VoiceLinkWebsocketClosedException) when (_webSocket.State is WebSocketState.CloseReceived)
                {
                    _logger.LogDebug("Connection {GuildId}: Disconnected from voice channel, closing the connection...", Guild.Id);
                    await DisconnectAsync();
                    return;
                }
                catch (VoiceLinkWebsocketClosedException) when (_webSocket.State is WebSocketState.CloseSent or WebSocketState.Closed)
                {
                    // We requested to close the connection, so we should just return.
                    return;
                }
                catch (VoiceLinkWebsocketClosedException) when (_webSocket.State is not WebSocketState.Open)
                {
                    // Attempt to reconnect and resume. If that fails then restart the connection entirely.
                    _logger.LogWarning("Connection {GuildId}: Websocket closed, attempting to resume...", Guild.Id);
                    _webSocket.Abort();
                    _webSocket = new ClientWebSocket();
                    await _webSocket.ConnectAsync(_endpoint!, _cancellationTokenSource.Token);
                    await _webSocket.SendAsync(new DiscordVoiceResumingCommand()
                    {
                        ServerId = Guild.Id,
                        SessionId = _sessionId!,
                        Token = _voiceToken!
                    }, _cancellationTokenSource.Token);
                }
                catch (TaskCanceledException) when (_webSocket.State is WebSocketState.CloseSent)
                {
                    // We requested to close the connection, so we should just return.
                    return;
                }
                catch (Exception error)
                {
                    // We need to catch and log all errors here because this task method is not watched
                    _logger.LogError(error, "Connection {GuildId}: Unexpected exception on the voice gateway loop. Please report this as a bug!", Guild.Id);
                    throw;
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
                });
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
                if (RtpUtilities.IsRtpHeader(udpReceiveResult.Buffer))
                {
                    // When the VoiceLinkUser is not null, this means that voice data was
                    // successfully decrypted and decoded and is ready to be consumed by the dev.
                    VoiceLinkUser? voiceLinkUser = HandleRtpVoicePacket(udpReceiveResult.Buffer);
                    if (voiceLinkUser is not null)
                    {
                        await voiceLinkUser._audioPipe.Writer.FlushAsync(_cancellationTokenSource.Token);
                    }
                }
                else if (RtcpUtilities.IsRtcpReceiverReport(udpReceiveResult.Buffer))
                {
                    HandleRtcpReceiverReportPacket(udpReceiveResult.Buffer);
                }
                else
                {
                    _logger.LogWarning("Connection {GuildId}: Received an unknown packet with a length of {Length} bytes. It is not an RTP Header or a keep alive packet. Skipping.", Guild.Id, udpReceiveResult.Buffer.Length);
                    _logger.LogDebug("Connection {GuildId}: Packet: {Packet}", Guild.Id, udpReceiveResult.Buffer.Select(x => x.ToString("X2", CultureInfo.InvariantCulture)));
                }
            }
        }

        private void HandleRtcpReceiverReportPacket(byte[] buffer)
        {
            RtcpHeader header = RtcpUtilities.DecodeHeader(buffer);

            // Decrypt the audio
            int decryptedBufferSize = _voiceEncrypter.GetDecryptedSize(buffer.Length);

            // Allocate the buffer for the decrypted audio
            byte[] decryptedAudioArray = ArrayPool<byte>.Shared.Rent(decryptedBufferSize);

            // Trimmed to the decrypted buffer size. Separate variable so
            // we can return the complete array to the pool at full length.
            Span<byte> decryptedAudio = decryptedAudioArray.AsSpan(0, decryptedBufferSize);
            if (!_voiceEncrypter.TryDecryptReportPacket(header, buffer, _secretKey, decryptedAudio))
            {
                _logger.LogWarning("Connection {GuildId}: Failed to decrypt RTCP Receiver Report packet for {Ssrc}. Skipping.", Guild.Id, header.Ssrc);
                ArrayPool<byte>.Shared.Return(decryptedAudioArray);
                return;
            }

            RtcpReceiverReportPacket receiverReportPacket = new(header, decryptedAudio);
            _logger.LogTrace("Connection {GuildId}: Received RTCP Receiver Report packet: {ReceiverReportPacket}", Guild.Id, receiverReportPacket);
            ArrayPool<byte>.Shared.Return(decryptedAudioArray);
        }

        private VoiceLinkUser? HandleRtpVoicePacket(byte[] buffer)
        {
            RtpHeader rtpHeader = RtpUtilities.DecodeHeader(buffer);
            if (!_speakers.TryGetValue(rtpHeader.Ssrc, out VoiceLinkUser? voiceLinkUser))
            {
                // Create a new user if we don't have one
                // We're explicitly passing a null member, however the dev should never expect this to
                // be null as the speaking event should always fire once we receive both the user and the ssrc.
                // TL;DR, this is to ensure we never lose any audio data.
                voiceLinkUser = new(this, rtpHeader.Ssrc, null!, _audioDecoderFactory(Extension.Configuration.ServiceProvider), rtpHeader.Sequence);
                _speakers.Add(rtpHeader.Ssrc, voiceLinkUser);
            }

            // Decrypt the audio
            int decryptedBufferSize = _voiceEncrypter.GetDecryptedSize(buffer.Length);

            // Allocate the buffer for the decrypted audio
            byte[] decryptedAudioArray = ArrayPool<byte>.Shared.Rent(decryptedBufferSize);

            // Trimmed to the decrypted buffer size. Separate variable so
            // we can return the complete array to the pool at full length.
            Span<byte> decryptedAudio = decryptedAudioArray.AsSpan(0, decryptedBufferSize);
            if (!_voiceEncrypter.TryDecryptOpusPacket(voiceLinkUser, buffer, _secretKey, decryptedAudio))
            {
                _logger.LogWarning("Connection {GuildId}: Failed to decrypt audio from {Ssrc}. Skipping.", Guild.Id, rtpHeader.Ssrc);
                ArrayPool<byte>.Shared.Return(decryptedAudioArray);
                return null;
            }

            // Strip any RTP header extensions. See https://www.rfc-editor.org/rfc/rfc3550#section-5.3.1
            // Discord currently uses a generic profile marker of [0xbe, 0xde]. See
            // https://www.rfc-editor.org/rfc/rfc8285#section-4.2
            if (rtpHeader.HasExtension)
            {
                ushort extensionLength = RtpUtilities.GetHeaderExtensionLength(decryptedAudio);
                decryptedAudio = decryptedAudio[(4 + (4 * extensionLength))..];
            }

            // TODO: Handle FEC (Forward Error Correction) aka packet loss.
            // * https://tools.ietf.org/html/rfc5109
            bool hasPacketLoss = voiceLinkUser.UpdateSequence(rtpHeader.Sequence);

            // Decode the audio
            try
            {
                int maxBufferSize = voiceLinkUser.AudioDecoder.GetMaxBufferSize();

                // Allocate the buffer for the PCM data
                Span<byte> audioBuffer = voiceLinkUser._audioPipe.Writer.GetSpan(maxBufferSize);

                // Decode the Opus packet
                int writtenBytes = voiceLinkUser.AudioDecoder.Decode(hasPacketLoss, decryptedAudio, audioBuffer);

                // Write the audio to the pipe
                voiceLinkUser._audioPipe.Writer.Advance(writtenBytes);
            }
            catch (Exception error)
            {
                // TODO: Should this be a reason to terminate the connection?
                // definitely should if a few in a row fail, at the very least
                _logger.LogError(error, "Connection {GuildId}: Failed to decode opus audio from {Ssrc}. Skipping", Guild.Id, rtpHeader.Ssrc);
            }

            ArrayPool<byte>.Shared.Return(decryptedAudioArray);
            return voiceLinkUser;
        }
    }
}
