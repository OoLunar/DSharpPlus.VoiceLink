using System;

namespace DSharpPlus.VoiceLink.Opus
{
    public readonly struct OpusDecoder(IntPtr state) : IDisposable
    {
        /// <inheritdoc cref="OpusNativeMethods.DecoderGetSize(int)"/>
        public static int GetSize(int channels) => OpusNativeMethods.DecoderGetSize(channels);

        /// <returns>A new decoder instance.</returns>
        /// <exception cref="OpusException">The Opus library has thrown an exception.</exception>
        /// <inheritdoc cref="OpusNativeMethods.DecoderCreate(OpusSampleRate, int, out OpusErrorCode*)"/>
        public static unsafe OpusDecoder Create(OpusSampleRate sampleRate, int channels)
        {
            IntPtr state = OpusNativeMethods.DecoderCreate(sampleRate, channels, out OpusErrorCode* errorCode);
            return (errorCode != default && *errorCode != OpusErrorCode.Ok)
                ? throw new OpusException(*errorCode)
                : new OpusDecoder(state);
        }

        /// <inheritdoc cref="OpusNativeMethods.DecoderInit(IntPtr, OpusSampleRate, int)"/>
        public void Init(OpusSampleRate sampleRate, int channels)
        {
            OpusErrorCode errorCode = OpusNativeMethods.DecoderInit(state, sampleRate, channels);
            if (errorCode != OpusErrorCode.Ok)
            {
                throw new OpusException(errorCode);
            }
        }

        /// <inheritdoc cref="OpusNativeMethods.Decode(IntPtr, byte*, int, byte*, int, int)"/>
        public unsafe int Decode(ReadOnlySpan<byte> data, Span<byte> pcm, int frameSize, bool decodeFec)
        {
            int decodedLength;
            fixed (byte* dataPointer = data)
            fixed (byte* pcmPointer = pcm)
            {
                decodedLength = OpusNativeMethods.Decode(
                    state,
                    dataPointer,
                    data.Length,
                    pcmPointer,
                    frameSize,
                    decodeFec ? 1 : 0
                );
            }

            // Less than zero means an error occurred
            if (decodedLength < 0)
            {
                throw new OpusException((OpusErrorCode)decodedLength);
            }

            // Multiplied by the sample size, which is size of short times the number of channels
            return decodedLength * sizeof(short) * 2;
        }

        /// <inheritdoc cref="OpusNativeMethods.DecodeFloat(IntPtr, byte*, int, byte*, int, int)"/>
        public unsafe int DecodeFloat(ReadOnlySpan<byte> data, Span<byte> pcm, int frameSize, bool decodeFec)
        {
            int decodedLength;
            fixed (byte* dataPointer = data)
            fixed (byte* pcmPointer = pcm)
            {
                decodedLength = OpusNativeMethods.DecodeFloat(state, dataPointer, data.Length, pcmPointer, frameSize, decodeFec ? 1 : 0);
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

        /// <inheritdoc cref="OpusNativeMethods.DecoderControl(IntPtr, OpusControlRequest, out int)"/>
        public void Control(OpusControlRequest control, out int value)
        {
            OpusErrorCode errorCode = OpusNativeMethods.DecoderControl(state, control, out value);

            if (errorCode != OpusErrorCode.Ok)
            {
                throw new OpusException(errorCode);
            }
        }

        /// <inheritdoc cref="OpusNativeMethods.DecoderDestroy(IntPtr)"/>
        public void Destroy() => OpusNativeMethods.DecoderDestroy(state);

        /// <summary>
        /// Gets the number of samples per channel of a packet.
        /// </summary>
        /// <exception cref="ArgumentException">Invalid argument passed to the decoder.</exception>
        /// <exception cref="InvalidOperationException">The compressed data passed is corrupted or of an unsupported type or an unknown error occured.</exception>
        /// <returns>The number of samples per channel of a packet.</returns>
        /// <inheritdoc cref="OpusNativeMethods.DecoderGetNbSamples(IntPtr, byte*, int)"/>
        public unsafe int GetSampleCount(ReadOnlySpan<byte> data)
        {
            int sampleCount;
            fixed (byte* dataPointer = data)
            {
                sampleCount = OpusNativeMethods.DecoderGetNbSamples(state, dataPointer, data.Length);
            }

            // Less than zero means an error occurred
            return sampleCount >= 0 ? sampleCount : throw new OpusException((OpusErrorCode)sampleCount);
        }

        public void Dispose() => Destroy();
    }
}
