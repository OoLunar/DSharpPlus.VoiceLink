using System;

namespace DSharpPlus.VoiceLink.AudioDecoders
{
    public delegate IAudioDecoder AudioDecoderFactory(IServiceProvider serviceProvider);
    public interface IAudioDecoder
    {
        public int GetMaxBufferSize();
        public int Decode(bool hasPacketLoss, ReadOnlySpan<byte> input, Span<byte> output);
    }
}
