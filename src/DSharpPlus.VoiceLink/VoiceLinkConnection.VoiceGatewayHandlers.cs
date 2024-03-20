using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using DSharpPlus.VoiceLink.Commands;
using DSharpPlus.VoiceLink.Enums;
using DSharpPlus.VoiceLink.EventArgs;
using DSharpPlus.VoiceLink.Payloads;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.VoiceLink
{
    public sealed partial class VoiceLinkConnection
    {
        private delegate ValueTask VoiceGatewayHandler(VoiceLinkConnection connection, ReadResult result);
        private static readonly FrozenDictionary<VoiceOpCode, VoiceGatewayHandler> _voiceGatewayHandlers;

        static VoiceLinkConnection()
        {
            Dictionary<VoiceOpCode, VoiceGatewayHandler> handlers = new()
            {
                [VoiceOpCode.Hello] = HelloAsync,
                [VoiceOpCode.Ready] = ReadyAsync,
                [VoiceOpCode.SessionDescription] = SessionDescriptionAsync,
                [VoiceOpCode.HeartbeatAck] = HeartbeatAckAsync,
                [VoiceOpCode.Resumed] = ResumedAsync,
                [VoiceOpCode.ClientConnected] = ClientConnectedAsync,
                [VoiceOpCode.ClientDisconnect] = ClientDisconnectAsync,
                [VoiceOpCode.Speaking] = SpeakingAsync
            };

            _voiceGatewayHandlers = handlers.ToFrozenDictionary();
        }

        private static async ValueTask HelloAsync(VoiceLinkConnection connection, ReadResult result)
        {
            // Start heartbeat
            connection._logger.LogDebug("Connection {GuildId}: Starting heartbeat...", connection.Guild.Id);
            _ = connection.SendHeartbeatAsync(connection._websocketPipe.Reader.Parse<VoiceGatewayDispatch<VoiceHelloPayload>>(result).Data);

            // Send Identify
            connection._logger.LogTrace("Connection {GuildId}: Sending identify...", connection.Guild.Id);
            await connection._webSocket.SendAsync(new VoiceGatewayDispatch()
            {
                OpCode = VoiceOpCode.Identify,
                Data = new VoiceIdentifyCommand()
                {
                    ServerId = connection.Guild.Id,
                    SessionId = connection._sessionId!,
                    Token = connection._voiceToken!,
                    UserId = connection.User.Id
                }
            }, connection._cancellationTokenSource.Token);
        }

        private static async ValueTask ReadyAsync(VoiceLinkConnection connection, ReadResult result)
        {
            VoiceReadyPayload voiceReadyPayload = connection._websocketPipe.Reader.Parse<VoiceGatewayDispatch<VoiceReadyPayload>>(result).Data;

            // Insert our SSRC code
            connection._logger.LogDebug("Connection {GuildId}: Bot's SSRC code is {Ssrc}.", connection.Guild.Id, voiceReadyPayload.Ssrc);
            connection._speakers.Add(voiceReadyPayload.Ssrc, new(connection, voiceReadyPayload.Ssrc, connection.Member, connection._audioDecoderFactory(connection.Extension.Configuration.ServiceProvider)));

            // Setup UDP while also doing ip discovery
            connection._logger.LogDebug("Connection {GuildId}: Setting up UDP, sending ip discovery...", connection.Guild.Id);
            byte[] ipDiscovery = new DiscordIpDiscoveryPacket(0x01, 70, voiceReadyPayload.Ssrc, string.Empty, default);
            await connection._udpClient.SendAsync(ipDiscovery, voiceReadyPayload.Ip, voiceReadyPayload.Port, connection._cancellationTokenSource.Token);

            // Receive IP Discovery Response
            UdpReceiveResult ipDiscoveryResponse = await connection._udpClient.ReceiveAsync(connection._cancellationTokenSource.Token);
            if (ipDiscoveryResponse.Buffer.Length != 74)
            {
                throw new InvalidOperationException("Received invalid IP Discovery Response.");
            }

            DiscordIpDiscoveryPacket reply = ipDiscoveryResponse.Buffer;
            connection._logger.LogDebug("Connection {GuildId}: Received ip discovery response: {Reply}", connection.Guild.Id, reply);
            connection._logger.LogTrace("Connection {GuildId}: Sending select protocol...", connection.Guild.Id);
            await connection._webSocket.SendAsync<VoiceGatewayDispatch>(new()
            {
                OpCode = VoiceOpCode.SelectProtocol,
                Data = new VoiceSelectProtocolCommand()
                {
                    Protocol = "udp",
                    Data = new VoiceSelectProtocolCommandData()
                    {
                        Address = reply.Address,
                        Port = reply.Port,
                        Mode = connection._voiceEncrypter.Name
                    }
                }
            }, connection._cancellationTokenSource.Token);
        }

        private static async ValueTask SessionDescriptionAsync(VoiceLinkConnection connection, ReadResult result)
        {
            VoiceSessionDescriptionPayload sessionDescriptionPayload = connection._websocketPipe.Reader.Parse<VoiceGatewayDispatch<VoiceSessionDescriptionPayload>>(result).Data;

            // The secret key uses IEnumerable because any other Array-based type will cause STJ to interpret the bytes as Base64.
            connection._secretKey = sessionDescriptionPayload.SecretKey.ToArray();

            // Start the UDP audio receiver loop
            _ = connection.ReceiveAudioLoopAsync();

            // Let the connection start sending audio data
            connection._readySemaphore.Release();

            // Invoke the event
            await connection.Extension._connectionCreated.InvokeAsync(connection.Extension, new VoiceLinkConnectionEventArgs(connection));
        }

        private static async ValueTask HeartbeatAckAsync(VoiceLinkConnection connection, ReadResult readResult)
        {
            long heartbeat = connection._websocketPipe.Reader.Parse<VoiceGatewayDispatch<long>>(readResult).Data;
            if (connection._heartbeatQueue.TryDequeue(out long unixTimestamp))
            {
                connection.WebsocketPing = TimeSpan.FromMilliseconds(heartbeat - unixTimestamp);
                return;
            }

            // This should never happen. If it does, we're in a bad state and should reconnect.
            connection._logger.LogError("Connection {GuildId}: Received unexpected heartbeat, disconnecting and reconnecting.", connection.Guild.Id);
            await connection.ReconnectAsync();
        }

        private static ValueTask ResumedAsync(VoiceLinkConnection connection, ReadResult _)
        {
            connection._logger.LogInformation("Connection {GuildId}: Resumed.", connection.Guild.Id);
            return default;
        }

        private static async ValueTask ClientConnectedAsync(VoiceLinkConnection connection, ReadResult result)
        {
            VoiceUserJoinPayload voiceClientConnectedPayload = connection._websocketPipe.Reader.Parse<VoiceGatewayDispatch<VoiceUserJoinPayload>>(result).Data;
            connection._logger.LogDebug("Connection {GuildId}: User {UserId} connected.", connection.Guild.Id, voiceClientConnectedPayload.UserId);
            await connection.Extension._userConnected.InvokeAsync(connection.Extension, new VoiceLinkUserEventArgs()
            {
                Connection = connection,
                Member = await connection.Guild.GetMemberAsync(voiceClientConnectedPayload.UserId)
            });
        }

        private static async ValueTask ClientDisconnectAsync(VoiceLinkConnection connection, ReadResult result)
        {
            VoiceUserLeavePayload voiceClientDisconnectedPayload = connection._websocketPipe.Reader.Parse<VoiceGatewayDispatch<VoiceUserLeavePayload>>(result).Data;
            connection._logger.LogDebug("Connection {GuildId}: User {UserId} disconnected.", connection.Guild.Id, voiceClientDisconnectedPayload.UserId);

            // We only receive a member's SSRC when they speak. This means they may not be within the speakers dictionary.
            // Since we're going to be receiving a lot more RTP packets than speaking payloads, we index by the SSRC.
            // This means we need to iterate through the speakers dictionary to find the user instead of just indexing.
            if (connection._speakers.FirstOrDefault(x => x.Value.Member.Id == voiceClientDisconnectedPayload.UserId) is KeyValuePair<uint, VoiceLinkUser> kvp)
            {
                connection._speakers.Remove(kvp.Key);
                kvp.Value._audioPipe.Writer.Complete();
            }

            await connection.Extension._userDisconnected.InvokeAsync(connection.Extension, new VoiceLinkUserEventArgs()
            {
                Connection = connection,
                Member = await connection.Guild.GetMemberAsync(voiceClientDisconnectedPayload.UserId)
            });
        }

        private static async ValueTask SpeakingAsync(VoiceLinkConnection connection, ReadResult result)
        {
            VoiceSpeakingPayload voiceSpeakingPayload = connection._websocketPipe.Reader.Parse<VoiceGatewayDispatch<VoiceSpeakingPayload>>(result).Data;
            connection._logger.LogTrace("Connection {GuildId}: User {UserId} is speaking.", connection.Guild.Id, voiceSpeakingPayload.UserId);

            // Sometimes we receive the voice data over the UDP connection before we receive the speaking payload.
            // To prevent packet loss, we create a pseudo user in the UDP receiver loop.
            // When we receive the speaking payload, we update the user's member object.
            if (!connection._speakers.TryGetValue(voiceSpeakingPayload.Ssrc, out VoiceLinkUser? voiceLinkUser))
            {
                voiceLinkUser = new(connection, voiceSpeakingPayload.Ssrc, await connection.Guild.GetMemberAsync(voiceSpeakingPayload.UserId), connection._audioDecoderFactory(connection.Extension.Configuration.ServiceProvider));
                connection._speakers.TryAdd(voiceSpeakingPayload.Ssrc, voiceLinkUser);
            }
            else
            {
                voiceLinkUser.Member = await connection.Guild.GetMemberAsync(voiceSpeakingPayload.UserId);
            }

            // Let the user know that someone is speaking. The user will always know who is speaking by this point.
            await connection.Extension._userSpeaking.InvokeAsync(connection.Extension, new VoiceLinkUserSpeakingEventArgs()
            {
                Connection = connection,
                Payload = voiceSpeakingPayload,
                VoiceUser = voiceLinkUser
            });
        }
    }
}
