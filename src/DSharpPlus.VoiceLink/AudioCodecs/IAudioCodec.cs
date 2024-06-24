using System;
using System.Buffers;

namespace DSharpPlus.VoiceLink.AudioCodecs
{
    public delegate IAudioCodec AudioCodecFactory(IServiceProvider serviceProvider);
    public interface IAudioCodec
    {
        public int GetMaxBufferSize();
        public int EncodeOpus(ReadOnlySequence<byte> input, Span<byte> output);
        public int DecodeOpus(bool hasPacketLoss, ReadOnlySpan<byte> input, Span<byte> output);
    }
}
