using System;

namespace DSharpPlus.VoiceLink.Opus
{
    public struct OpusDecoder : IDisposable
    {
        /// <inheritdoc cref="OpusNativeMethods.DecoderGetSize(int)"/>
        public static int GetSize(int channels) => OpusNativeMethods.DecoderGetSize(channels);

        /// <returns>A new decoder instance.</returns>
        /// <exception cref="OpusException">The Opus library has thrown an exception.</exception>
        /// <inheritdoc cref="OpusNativeMethods.DecoderCreate(OpusSampleRate, int, out OpusErrorCode*)"/>
        public static unsafe OpusDecoder Create(OpusSampleRate sampleRate, int channels)
        {
            OpusDecoder* decoder = OpusNativeMethods.DecoderCreate(sampleRate, channels, out OpusErrorCode* errorCode);
            return *errorCode != OpusErrorCode.Ok ? throw new OpusException(*errorCode) : *decoder;
        }

        /// <returns></returns>
        /// <inheritdoc cref="OpusNativeMethods.DecoderInit(OpusDecoder*, OpusSampleRate, int)"/>
        public unsafe void Init(OpusSampleRate sampleRate, int channels)
        {
            OpusErrorCode errorCode;
            fixed (OpusDecoder* pinned = &this)
            {
                errorCode = OpusNativeMethods.DecoderInit(pinned, sampleRate, channels);
            }

            if (errorCode != OpusErrorCode.Ok)
            {
                throw new OpusException(errorCode);
            }
        }

        /// <inheritdoc cref="OpusNativeMethods.Decode(OpusDecoder*, byte*, int, short*, int, int)"/>
        public unsafe int Decode(ReadOnlySpan<byte> data, ref Span<short> pcm, int frameSize, bool decodeFec)
        {
            int decodedLength;
            fixed (OpusDecoder* pinned = &this)
            fixed (byte* dataPointer = data)
            fixed (short* pcmPointer = pcm)
            {
                decodedLength = OpusNativeMethods.Decode(pinned, dataPointer, data.Length, pcmPointer, frameSize, decodeFec ? 1 : 0);
            }

            // Less than zero means an error occurred
            if (decodedLength < 0)
            {
                throw new OpusException((OpusErrorCode)decodedLength);
            }

            // Trim the data to the encoded length
            data = data[decodedLength..];
            return decodedLength;
        }

        /// <inheritdoc cref="OpusNativeMethods.DecodeFloat(OpusDecoder*, byte*, int, float*, int, int)"/>
        public unsafe int DecodeFloat(ReadOnlySpan<byte> data, ref Span<float> pcm, int frameSize, bool decodeFec)
        {
            int decodedLength;
            fixed (OpusDecoder* pinned = &this)
            fixed (byte* dataPointer = data)
            fixed (float* pcmPointer = pcm)
            {
                decodedLength = OpusNativeMethods.DecodeFloat(pinned, dataPointer, data.Length, pcmPointer, frameSize, decodeFec ? 1 : 0);
            }

            // Less than zero means an error occurred
            if (decodedLength < 0)
            {
                throw new OpusException((OpusErrorCode)decodedLength);
            }

            // Trim the data to the encoded length
            data = data[decodedLength..];
            return decodedLength;
        }

        /// <inheritdoc cref="OpusNativeMethods.DecoderControl(OpusDecoder*, OpusControlRequest, int)"/>
        public unsafe void Control(OpusControlRequest control, int value)
        {
            OpusErrorCode errorCode;
            fixed (OpusDecoder* pinned = &this)
            {
                errorCode = OpusNativeMethods.DecoderControl(pinned, control, value);
            }

            if (errorCode != OpusErrorCode.Ok)
            {
                throw new OpusException(errorCode);
            }
        }

        /// <inheritdoc cref="OpusNativeMethods.DecoderDestroy(OpusDecoder*)"/>
        public unsafe void Destroy()
        {
            fixed (OpusDecoder* pinned = &this)
            {
                OpusNativeMethods.DecoderDestroy(pinned);
            }
        }

        /// <summary>
        /// Gets the number of samples per channel of a packet.
        /// </summary>
        /// <exception cref="ArgumentException">Invalid argument passed to the decoder.</exception>
        /// <exception cref="InvalidOperationException">The compressed data passed is corrupted or of an unsupported type or an unknown error occured.</exception>
        /// <returns>The number of samples per channel of a packet.</returns>
        /// <inheritdoc cref="OpusNativeMethods.DecoderGetNbSamples(OpusDecoder*, byte*, int)"/>
        public unsafe int GetSampleCount(ReadOnlySpan<byte> data)
        {
            int sampleCount;
            fixed (OpusDecoder* pinned = &this)
            fixed (byte* dataPointer = data)
            {
                sampleCount = OpusNativeMethods.DecoderGetNbSamples(pinned, dataPointer, data.Length);
            }

            // Less than zero means an error occurred
            return sampleCount > 0 ? sampleCount : throw new OpusException((OpusErrorCode)sampleCount);
        }

        public void Dispose() => Destroy();
    }
}
