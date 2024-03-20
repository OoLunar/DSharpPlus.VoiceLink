using System;
using DSharpPlus.VoiceLink.AudioDecoders;
using DSharpPlus.VoiceLink.VoiceEncrypters;
using Microsoft.Extensions.DependencyInjection;

namespace DSharpPlus.VoiceLink
{
    public sealed record VoiceLinkConfiguration
    {
        public IServiceCollection ServiceCollection { get; set; } = new ServiceCollection();
        internal IServiceProvider ServiceProvider => _serviceProvider ??= ServiceCollection.BuildServiceProvider();
        private IServiceProvider? _serviceProvider;
        public int MaxHeartbeatQueueSize { get; set; } = 5;
        public IVoiceEncrypter VoiceEncrypter { get; set; } = new XSalsa20Poly1305();
        public AudioDecoderFactory AudioDecoderFactory { get; set; } = _ => new Pcm16BitAudioDecoder();
    }
}
