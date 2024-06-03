using System;

namespace DSharpPlus.VoiceLink.AudioCodecs
{
    public class OpusAudioCodec : IAudioCodec
    {
        private const int CHANNELS = 2;
        private const int MAX_FRAME_SIZE = 5760;
        private const int MAX_BUFFER_SIZE = MAX_FRAME_SIZE * 2 * CHANNELS;

        public int GetMaxBufferSize() => MAX_BUFFER_SIZE;
        public int Decode(bool hasPacketLoss, ReadOnlySpan<byte> input, Span<byte> output)
        {
            input.CopyTo(output);
            return input.Length;
        }
    }
}
