using System;

namespace DSharpPlus.VoiceLink.AudioCodecs
{
    public delegate IAudioCodec AudioCodecFactory(IServiceProvider serviceProvider);
    public interface IAudioCodec
    {
        public int GetMaxBufferSize();
        public int Decode(bool hasPacketLoss, ReadOnlySpan<byte> input, Span<byte> output);
    }
}
