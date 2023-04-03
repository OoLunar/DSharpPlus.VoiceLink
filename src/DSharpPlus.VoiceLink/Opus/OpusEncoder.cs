using System;

namespace DSharpPlus.VoiceLink.Opus
{
    public struct OpusEncoder : IDisposable
    {
        /// <inheritdoc cref="OpusNativeMethods.EncoderGetSize(int)"/>
        public static int GetSize(int channels) => OpusNativeMethods.EncoderGetSize(channels);

        /// <exception cref="OpusException">The Opus library has thrown an exception.</exception>
        /// <inheritdoc cref="OpusNativeMethods.EncoderInit(OpusEncoder*, OpusSampleRate, int, OpusApplication)"/>
        public static unsafe OpusEncoder Create(OpusSampleRate sampleRate, int channels, OpusApplication application)
        {
            OpusEncoder* encoder = OpusNativeMethods.EncoderCreate(sampleRate, channels, application, out OpusErrorCode* errorCode);
            return *errorCode != OpusErrorCode.Ok ? throw new OpusException(*errorCode) : *encoder;
        }

        /// <summary>
        /// Initializes a previously allocated encoder state.
        /// </summary>
        /// <inheritdoc cref="OpusNativeMethods.EncoderInit(OpusEncoder*, OpusSampleRate, int, OpusApplication)"/>
        public unsafe OpusErrorCode Init(OpusSampleRate sampleRate, int channels, OpusApplication application)
        {
            fixed (OpusEncoder* pinned = &this)
            {
                return OpusNativeMethods.EncoderInit(pinned, sampleRate, channels, application);
            }
        }

        /// <param name="data">The encoded data.</param>
        /// <returns>The length of the encoded packet (in bytes)</returns>
        /// <exception cref="OpusException">The Opus library has thrown an exception.</exception>
        /// <inheritdoc cref="OpusNativeMethods.Encode(OpusEncoder*, byte*, int, byte*, int)"/>
        public unsafe int Encode(ReadOnlySpan<byte> pcm, int frameSize, ref Span<byte> data)
        {
            int encodedLength;
            fixed (OpusEncoder* pinned = &this)
            fixed (byte* pcmPointer = pcm)
            fixed (byte* dataPointer = data)
            {
                encodedLength = OpusNativeMethods.Encode(pinned, pcmPointer, frameSize, dataPointer, data.Length);
            }

            // Less than zero means an error occurred
            if (encodedLength < 0)
            {
                throw new OpusException((OpusErrorCode)encodedLength);
            }

            // Trim the data to the encoded length
            data = data[..encodedLength];
            return encodedLength;
        }

        /// <param name="data">The encoded data.</param>
        /// <returns>The length of the encoded packet (in bytes)</returns>
        /// <exception cref="OpusException">The Opus library has thrown an exception.</exception>
        /// <inheritdoc cref="OpusNativeMethods.EncodeFloat(OpusEncoder*, byte*, int, byte*, int)"/>
        public unsafe int EncodeFloat(ReadOnlySpan<byte> pcm, int frameSize, ref Span<byte> data)
        {
            int encodedLength;
            fixed (OpusEncoder* pinned = &this)
            fixed (byte* pcmPointer = pcm)
            fixed (byte* dataPointer = data)
            {
                encodedLength = OpusNativeMethods.EncodeFloat(pinned, pcmPointer, frameSize, dataPointer, data.Length);
            }

            // Less than zero means an error occurred
            if (encodedLength < 0)
            {
                throw new OpusException((OpusErrorCode)encodedLength);
            }

            // Trim the data to the encoded length
            data = data[..encodedLength];
            return encodedLength;
        }

        /// <summary>
        /// Frees an <see cref="OpusEncoder"/> allocated by <see cref="Create(OpusSampleRate, int, OpusApplication)"/>.
        /// </summary>
        public unsafe void Destroy()
        {
            fixed (OpusEncoder* pinned = &this)
            {
                OpusNativeMethods.EncoderDestroy(pinned);
            }
        }

        /// <inheritdoc cref="OpusNativeMethods.EncoderControl(OpusEncoder*, int, int)"/>
        public unsafe OpusErrorCode Control(OpusControlRequest request, int value)
        {
            fixed (OpusEncoder* pinned = &this)
            {
                return OpusNativeMethods.EncoderControl(pinned, (int)request, value);
            }
        }

        public void Dispose() => Destroy();
    }
}
