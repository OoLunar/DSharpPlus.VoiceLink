using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DSharpPlus.VoiceLink
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// Registers the extension with the <see cref="DiscordClient"/>.
        /// </summary>
        /// <param name="client">The client to register the extension with.</param>
        /// <param name="configuration">The configuration to use for the extension.</param>
        public static VoiceLinkExtension UseVoiceLink(this DiscordClient client, VoiceLinkConfiguration? configuration = null)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            else if (client.GetExtension<VoiceLinkExtension>() is not null)
            {
                throw new InvalidOperationException("The VoiceLink extension is already initialized.");
            }
            else if (!client.Intents.HasIntent(DiscordIntents.GuildVoiceStates))
            {
                throw new InvalidOperationException("The VoiceLink extension requires the GuildVoiceStates intent.");
            }

            VoiceLinkExtension extension = new(configuration ?? new());
            client.AddExtension(extension);
            return extension;
        }

        /// <summary>
        /// Registers the extension with all the shards on the <see cref="DiscordShardedClient"/>.
        /// </summary>
        /// <param name="shardedClient">The client to register the extension with.</param>
        /// <param name="configuration">The configuration to use for the extension.</param>
        public static async Task<IReadOnlyDictionary<int, VoiceLinkExtension>> UseVoiceLinkAsync(this DiscordShardedClient shardedClient, VoiceLinkConfiguration? configuration = null)
        {
            ArgumentNullException.ThrowIfNull(shardedClient);
            configuration ??= new();

            // Figure out how many shards we have
            _ = await shardedClient.InitializeShardsAsync();

            // Register the extension on each shard
            Dictionary<int, VoiceLinkExtension> extensions = [];
            foreach (DiscordClient shard in shardedClient.ShardClients.Values)
            {
                extensions[shard.ShardId] = shard.GetExtension<VoiceLinkExtension>() ?? shard.UseVoiceLink(configuration);
            }

            return extensions.AsReadOnly();
        }

        /// <summary>
        /// Retrieves the <see cref="VoiceLinkExtension"/> from the <see cref="DiscordClient"/>.
        /// </summary>
        /// <param name="client">The client to retrieve the extension from.</param>
        public static VoiceLinkExtension GetVoiceLinkExtension(this DiscordClient client) => client is null
            ? throw new ArgumentNullException(nameof(client))
            : client.GetExtension<VoiceLinkExtension>();

        /// <summary>
        /// Retrieves the <see cref="VoiceLinkExtension"/> from all of the shards on <see cref="DiscordShardedClient"/>.
        /// </summary>
        /// <param name="shardedClient">The client to retrieve the extension from.</param>
        public static IReadOnlyDictionary<int, VoiceLinkExtension> GetVoiceLinkExtensions(this DiscordShardedClient shardedClient)
        {
            ArgumentNullException.ThrowIfNull(shardedClient);

            Dictionary<int, VoiceLinkExtension> extensions = [];
            foreach (DiscordClient shard in shardedClient.ShardClients.Values)
            {
                extensions[shard.ShardId] = shard.GetExtension<VoiceLinkExtension>();
            }

            return extensions.AsReadOnly();
        }

        internal static async ValueTask SendAsync<T>(this ClientWebSocket webSocket, T data, CancellationToken cancellationToken = default) => await webSocket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(data, VoiceLinkExtension.DefaultJsonSerializerOptions), WebSocketMessageType.Text, true, cancellationToken);

        internal static async ValueTask ReadAsync(this ClientWebSocket websocket, PipeWriter pipeWriter, CancellationToken cancellationToken = default)
        {
            ValueWebSocketReceiveResult result;
            do
            {
                Memory<byte> memory = pipeWriter.GetMemory(1024);
                result = await websocket.ReceiveAsync(memory, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new VoiceLinkWebsocketClosedException("WebSocket received close message.");
                }
                else if (result.MessageType != WebSocketMessageType.Text)
                {
                    throw new InvalidOperationException("WebSocket received unexpected and unknown binary data.");
                }

                pipeWriter.Advance(result.Count);
            } while (!result.EndOfMessage);
            await pipeWriter.FlushAsync(cancellationToken);
        }

        internal static async ValueTask<T> ParseAsync<T>(this PipeReader reader, CancellationToken cancellationToken = default)
        {
            ReadResult result = await reader.ReadAsync(cancellationToken);
            return result.IsCanceled ? throw new OperationCanceledException("The reader was canceled.") : reader.Parse<T>(result);
        }

        internal static T Parse<T>(this PipeReader reader, ReadResult result)
        {
            Utf8JsonReader jsonReader = new(result.Buffer);
            T value = JsonSerializer.Deserialize<T>(ref jsonReader, VoiceLinkExtension.DefaultJsonSerializerOptions)!;
            reader.AdvanceTo(result.Buffer.GetPosition(jsonReader.BytesConsumed));
            return value;
        }
    }
}
