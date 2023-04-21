using System;
using System.Net;
using DSharpPlus.Net.WebSocket;
using DSharpPlus.VoiceLink.VoiceEncrypters;
using Microsoft.Extensions.DependencyInjection;

namespace DSharpPlus.VoiceLink
{
    public sealed record VoiceLinkConfiguration
    {
        public IServiceCollection ServiceCollection { get; set; } = new ServiceCollection();
        internal IServiceProvider ServiceProvider => _serviceProvider ??= ServiceCollection.BuildServiceProvider();
        private IServiceProvider? _serviceProvider;

        public IWebProxy? Proxy { get; set; }
        public WebSocketClientFactoryDelegate WebSocketClientFactory
        {
            internal get => _webSocketClientFactory;
            set => _webSocketClientFactory = value is null ? throw new ArgumentNullException(nameof(value)) : value;
        }
        private WebSocketClientFactoryDelegate _webSocketClientFactory = WebSocketClient.CreateNew;

        public int MaxHeartbeatQueueSize { get; set; } = 5;
        public IVoiceEncrypter VoiceEncrypter { get; set; } = new XSalsa20Poly1305();
    }
}
