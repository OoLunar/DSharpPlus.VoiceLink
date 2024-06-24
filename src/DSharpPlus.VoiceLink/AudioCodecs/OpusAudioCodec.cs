using System;
using System.Buffers;

namespace DSharpPlus.VoiceLink.AudioCodecs
{
    public class OpusAudioCodec : IAudioCodec
    {
        public const int CHANNELS = 2;
        public const int MAX_FRAME_SIZE = 5760;
        public const int MAX_BUFFER_SIZE = MAX_FRAME_SIZE * 2 * CHANNELS;
        public static readonly byte[] SilenceFrame = [0xF8, 0xFF, 0xFE];

        public int GetMaxBufferSize() => MAX_BUFFER_SIZE;
        public int DecodeOpus(bool hasPacketLoss, ReadOnlySpan<byte> input, Span<byte> output)
        {
            if (hasPacketLoss)
            {
                SilenceFrame.AsSpan().CopyTo(output);
                return SilenceFrame.Length;
            }

            input.CopyTo(output);
            return input.Length;
        }

        public int EncodeOpus(ReadOnlySequence<byte> input, Span<byte> output)
        {
            int frameSize = (int)Math.Min(input.Length, MAX_FRAME_SIZE);
            input.Slice(0, frameSize).CopyTo(output);
            return frameSize;
        }
    }
}
