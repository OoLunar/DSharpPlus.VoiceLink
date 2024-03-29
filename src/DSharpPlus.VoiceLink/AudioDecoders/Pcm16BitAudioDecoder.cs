using System;
using DSharpPlus.VoiceLink.Opus;

namespace DSharpPlus.VoiceLink.AudioDecoders
{
    public class Pcm16BitAudioDecoder : IAudioDecoder
    {
        // 16 bits per sample
        private const int BYTES_PER_SAMPLE = 2;

        // 48 kHz
        private const int SAMPLE_RATE = 48000;

        // 20 milliseconds
        private const double FRAME_DURATION = 0.020;

        // 960 samples
        private const int FRAME_SIZE = (int)(SAMPLE_RATE * FRAME_DURATION);

        // 20 milliseconds of audio data, 3840 bytes
        private const int SINGLE_CHANNEL_BUFFER_SIZE = FRAME_SIZE * BYTES_PER_SAMPLE;

        public int Channels { get; init; }
        private OpusDecoder _opusDecoder { get; init; }

        public Pcm16BitAudioDecoder(int channels = 2)
        {
            Channels = channels;
            _opusDecoder = OpusDecoder.Create(OpusSampleRate.Opus48000Hz, channels);
        }

        /// <inheritdoc/>
        public int GetMaxBufferSize() => SINGLE_CHANNEL_BUFFER_SIZE * Channels;

        /// <inheritdoc/>
        public int Decode(bool hasPacketLoss, ReadOnlySpan<byte> input, Span<byte> output)
        {
            _opusDecoder.Decode(input, output, FRAME_SIZE, hasPacketLoss);
            return output.Length;
        }
    }
}
