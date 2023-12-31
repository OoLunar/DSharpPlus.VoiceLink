using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DSharpPlus.VoiceLink
{
    public static class ExtensionMethods
    {
        private static readonly Type _shardedLoggerFactoryType = typeof(DiscordClient).Assembly.GetType("DSharpPlus.ShardedLoggerFactory", true)!;

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

            configuration ??= new();
            ServiceDescriptor? currentLoggingImplementation = configuration.ServiceCollection.FirstOrDefault(service => service.ServiceType == typeof(ILoggerFactory));

            // There are 4 scenarios here:
            // - The user does not provide a logging implementation.
            // - The user provides a logging implementation only to the DiscordClient.
            // - The user provides a the default ShardedLoggerFactory implementation through the services
            // - The user provides a custom logging implementation through the services

            // No implementation provided
            if (currentLoggingImplementation is null)
            {
                // Check if the client has a valid logging implementation
                Type clientType = client.Logger.GetType();
                if (clientType != _shardedLoggerFactoryType)
                {
                    Type[] clientInterfaces = clientType.GetInterfaces();
                    if (clientInterfaces.Any(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(ILogger<>)))
                    {
                        configuration.ServiceCollection.AddSingleton(typeof(ILogger<>), client.Logger);
                    }

                    if (clientInterfaces.Contains(typeof(ILoggerFactory)))
                    {
                        configuration.ServiceCollection.AddSingleton(typeof(ILoggerFactory), client.Logger);
                    }
                }
                else
                {
                    Console.WriteLine($"No logging system set, using a {nameof(NullLoggerFactory)}. This is not recommended, please provide a logging system so you can see errors.");
                    configuration.ServiceCollection.AddSingleton<ILoggerFactory, NullLoggerFactory>().AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
                }
            }
            // Check if they provided the ShardedLoggerFactory explicitly to the services
            else if (currentLoggingImplementation.ServiceType == _shardedLoggerFactoryType)
            {
                Console.WriteLine($"ShardedLoggerFactory detected, using {nameof(NullLoggerFactory)} instead. VoiceLink is NOT compatible with the default logging system that DSharpPlus provides!");
                configuration.ServiceCollection
                    .RemoveAll<ILoggerFactory>().RemoveAll(typeof(ILogger<>)) // Remove the default logging implementation, if set
                    .AddSingleton<ILoggerFactory, NullLoggerFactory>().AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            }

            VoiceLinkExtension extension = new(configuration);
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

            _ = await shardedClient.InitializeShardsAsync();
            configuration ??= new();

            ServiceDescriptor? currentLoggingImplementation = configuration.ServiceCollection.FirstOrDefault(service => service.ServiceType == typeof(ILoggerFactory));
            if (currentLoggingImplementation is null)
            {
                Console.WriteLine($"No logging system set, using a {nameof(NullLoggerFactory)}. This is not recommended, please provide a logging system so you can see errors.");
                _ = configuration.ServiceCollection.AddSingleton<ILoggerFactory, NullLoggerFactory>().AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            }
            else if (currentLoggingImplementation.ServiceType == _shardedLoggerFactoryType && shardedClient.Logger.GetType() == _shardedLoggerFactoryType)
            {
                Console.WriteLine($"ShardedLoggerFactory detected, using {nameof(NullLoggerFactory)} instead. VoiceLink is NOT compatible with the default logging system that DSharpPlus provides!");
                _ = configuration.ServiceCollection
                    .RemoveAll<ILoggerFactory>().RemoveAll(typeof(ILogger<>)) // Remove the default logging implementation, if set
                    .AddSingleton<ILoggerFactory, NullLoggerFactory>().AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            }

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
        public static VoiceLinkExtension? GetVoiceLinkExtension(this DiscordClient client) => client is null
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
                VoiceLinkExtension? extension = shard.GetExtension<VoiceLinkExtension>();
                if (extension is not null)
                {
                    extensions[shard.ShardId] = extension;
                }
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
            return result.IsCanceled ? throw new OperationCanceledException("The reader was canceled.") : await reader.ParseAsync<T>(result);
        }

        internal static ValueTask<T> ParseAsync<T>(this PipeReader reader, ReadResult result)
        {
            try
            {
                if (result.IsCompleted)
                {
                    return ValueTask.FromResult(JsonSerializer.Deserialize<T>(reader.AsStream(), VoiceLinkExtension.DefaultJsonSerializerOptions)!);
                }
                else if (result.Buffer.IsSingleSegment)
                {
                    return ValueTask.FromResult(JsonSerializer.Deserialize<T>(result.Buffer.FirstSpan, VoiceLinkExtension.DefaultJsonSerializerOptions)!);
                }
                else
                {
                    return ValueTask.FromResult(JsonSerializer.Deserialize<T>(result.Buffer.ToArray(), VoiceLinkExtension.DefaultJsonSerializerOptions)!);
                }
            }
            finally
            {
                reader.AdvanceTo(result.Buffer.End);
            }
        }
    }
}
