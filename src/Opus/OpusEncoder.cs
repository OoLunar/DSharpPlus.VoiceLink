using System;

namespace DSharpPlus.VoiceLink.Opus
{
    public struct OpusEncoder : IDisposable
    {
        /// <inheritdoc cref="OpusNativeMethods.EncoderGetSize(int)"/>
        public static int GetSize(int channels) => OpusNativeMethods.EncoderGetSize(channels);

        /// <inheritdoc cref="OpusNativeMethods.EncoderInit(OpusEncoder*, OpusSampleRate, int, OpusApplication)"/>
        /// <exception cref="ArgumentException">Invalid argument passed to the encoder.</exception>
        /// <exception cref="InvalidOperationException">Failed to allocate memory for the encoder or an internal error occurred in the encoder.</exception>
        public static unsafe OpusEncoder Create(OpusSampleRate sampleRate, int channels, OpusApplication application)
        {
            OpusEncoder* encoder = OpusNativeMethods.EncoderCreate(sampleRate, channels, application, out OpusErrorCode* errorCode);
            return *errorCode switch
            {
                OpusErrorCode.Ok => *encoder,
                OpusErrorCode.BadArg => throw new ArgumentException("Invalid argument passed to the encoder."),
                OpusErrorCode.AllocFail => throw new InvalidOperationException("Failed to allocate memory for the encoder."),
                OpusErrorCode.InternalError => throw new InvalidOperationException("An internal error occurred in the encoder."),
                _ => *encoder
            };
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
                throw (OpusErrorCode)encodedLength switch
                {
                    OpusErrorCode.BadArg => new ArgumentException("Invalid argument passed to the encoder."),
                    OpusErrorCode.AllocFail => new InvalidOperationException("Failed to allocate memory for the encoder."),
                    OpusErrorCode.InternalError => new InvalidOperationException("An internal error occurred in the encoder."),
                    OpusErrorCode.BufferTooSmall => new InvalidOperationException("The buffer is too small to hold the encoded data."),
                    OpusErrorCode.InvalidPacket => new InvalidOperationException("The compressed data passed is corrupted or of an unsupported type."),
                    OpusErrorCode.Unimplemented => new NotImplementedException("The encoder does not implement the requested feature."),
                    OpusErrorCode.InvalidState => new InvalidOperationException("The encoder is in an invalid state."),
                    _ => new InvalidOperationException($"An unknown error occurred while encoding the PCM data: {(OpusErrorCode)encodedLength}"),
                };
            }

            // Trim the data to the encoded length
            data = data[..encodedLength];
            return encodedLength;
        }

        /// <param name="data">The encoded data.</param>
        /// <returns>The length of the encoded packet (in bytes)</returns>
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
                throw (OpusErrorCode)encodedLength switch
                {
                    OpusErrorCode.BadArg => new ArgumentException("Invalid argument passed to the encoder."),
                    OpusErrorCode.AllocFail => new InvalidOperationException("Failed to allocate memory for the encoder."),
                    OpusErrorCode.InternalError => new InvalidOperationException("An internal error occurred in the encoder."),
                    OpusErrorCode.BufferTooSmall => new InvalidOperationException("The buffer is too small to hold the encoded data."),
                    OpusErrorCode.InvalidPacket => new InvalidOperationException("The compressed data passed is corrupted or of an unsupported type."),
                    OpusErrorCode.Unimplemented => new NotImplementedException("The encoder does not implement the requested feature."),
                    OpusErrorCode.InvalidState => new InvalidOperationException("The encoder is in an invalid state."),
                    _ => new InvalidOperationException($"An unknown error occurred while encoding the PCM data: {(OpusErrorCode)encodedLength}"),
                };
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
