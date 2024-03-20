using System;
using DSharpPlus.VoiceLink.Opus;

namespace DSharpPlus.VoiceLink.AudioDecoders
{
    public class Pcm16BitAudioDecoder : IAudioDecoder
    {
        // 48 kHz
        private const int SAMPLE_RATE = 48000;

        // 20 milliseconds
        private const double FRAME_DURATION = 0.020;

        // 960 samples
        private const int FRAME_SIZE = (int)(SAMPLE_RATE * FRAME_DURATION);

        // Stereo audio + opus PCM units are 16 bits
        private const int BUFFER_SIZE = FRAME_SIZE * 2 * sizeof(short);

        /// <inheritdoc/>
        public int GetMaxBufferSize() => BUFFER_SIZE;

        private OpusDecoder _opusDecoder { get; init; } = OpusDecoder.Create(OpusSampleRate.Opus48000Hz, 2);

        /// <inheritdoc/>
        public int Decode(bool hasPacketLoss, ReadOnlySpan<byte> input, Span<byte> output)
        {
            _opusDecoder.Decode(input, output, FRAME_SIZE, hasPacketLoss);
            return BUFFER_SIZE;
        }
    }
}
