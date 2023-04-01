using System;

namespace DSharpPlus.VoiceLink.Opus
{
    public struct OpusDecoder : IDisposable
    {
        public static int GetSize(int channels) => OpusNativeMethods.DecoderGetSize(channels);

        public static unsafe OpusDecoder Create(OpusSampleRate sampleRate, int channels)
        {
            OpusDecoder* decoder = OpusNativeMethods.DecoderCreate(sampleRate, channels, out OpusErrorCode errorCode);
            return errorCode switch
            {
                OpusErrorCode.Ok => *decoder,
                OpusErrorCode.BadArg => throw new ArgumentException("Invalid argument passed to the decoder."),
                OpusErrorCode.AllocFail => throw new InvalidOperationException("Failed to allocate memory for the decoder."),
                OpusErrorCode.InternalError => throw new InvalidOperationException("An internal error occurred in the decoder."),
                _ => *decoder
            };
        }

        public unsafe OpusErrorCode Init(OpusSampleRate sampleRate, int channels)
        {
            fixed (OpusDecoder* pinned = &this)
            {
                return OpusNativeMethods.DecoderInit(pinned, sampleRate, channels);
            }
        }

        public unsafe int Decode(ReadOnlySpan<byte> data, ref Span<byte> pcm, int frameSize, bool decodeFec)
        {
            int decodedLength;
            fixed (OpusDecoder* pinned = &this)
            fixed (byte* dataPointer = data)
            fixed (byte* pcmPointer = pcm)
            {
                decodedLength = OpusNativeMethods.Decode(pinned, dataPointer, data.Length, pcmPointer, frameSize, decodeFec ? 1 : 0);
            }

            // Less than zero means an error occurred
            if (decodedLength < 0)
            {
                throw (OpusErrorCode)decodedLength switch
                {
                    OpusErrorCode.BadArg => new ArgumentException("Invalid argument passed to the decoder."),
                    OpusErrorCode.AllocFail => new InvalidOperationException("Failed to allocate memory for the decoder."),
                    OpusErrorCode.InternalError => new InvalidOperationException("An internal error occurred in the decoder."),
                    OpusErrorCode.BufferTooSmall => new InvalidOperationException("The buffer is too small to hold the decoded data."),
                    OpusErrorCode.InvalidPacket => new InvalidOperationException("The compressed data passed is corrupted or of an unsupported type."),
                    _ => new InvalidOperationException("An unknown error occurred in the decoder.")
                };
            }

            // Trim the data to the encoded length
            data = data[decodedLength..];
            return decodedLength;
        }

        public unsafe int DecodeFloat(ReadOnlySpan<byte> data, ref Span<byte> pcm, int frameSize, bool decodeFec)
        {
            int decodedLength;
            fixed (OpusDecoder* pinned = &this)
            fixed (byte* dataPointer = data)
            fixed (byte* pcmPointer = pcm)
            {
                decodedLength = OpusNativeMethods.DecodeFloat(pinned, dataPointer, data.Length, pcmPointer, frameSize, decodeFec ? 1 : 0);
            }

            // Less than zero means an error occurred
            if (decodedLength < 0)
            {
                throw (OpusErrorCode)decodedLength switch
                {
                    OpusErrorCode.BadArg => new ArgumentException("Invalid argument passed to the decoder."),
                    OpusErrorCode.AllocFail => new InvalidOperationException("Failed to allocate memory for the decoder."),
                    OpusErrorCode.InternalError => new InvalidOperationException("An internal error occurred in the decoder."),
                    OpusErrorCode.BufferTooSmall => new InvalidOperationException("The buffer is too small to hold the decoded data."),
                    OpusErrorCode.InvalidPacket => new InvalidOperationException("The compressed data passed is corrupted or of an unsupported type."),
                    _ => new InvalidOperationException("An unknown error occurred in the decoder.")
                };
            }

            // Trim the data to the encoded length
            data = data[decodedLength..];
            return decodedLength;
        }

        public unsafe void Control(OpusControlRequest control, int value)
        {
            OpusErrorCode errorCode;
            fixed (OpusDecoder* pinned = &this)
            {
                errorCode = OpusNativeMethods.DecoderControl(pinned, control, value);
            }

            switch (errorCode)
            {
                case OpusErrorCode.BadArg: throw new ArgumentException("Invalid argument passed to the decoder.");
                case OpusErrorCode.AllocFail: throw new InvalidOperationException("Failed to allocate memory for the decoder.");
                case OpusErrorCode.InternalError: throw new InvalidOperationException("An internal error occurred in the decoder.");
                case OpusErrorCode.InvalidPacket: throw new InvalidOperationException("The compressed data passed is corrupted or of an unsupported type.");
                case OpusErrorCode.Unimplemented: throw new NotImplementedException("The request number is valid but not implemented by this version of the library.");
                case OpusErrorCode.InvalidState: throw new InvalidOperationException("The decoder structure passed is invalid or already freed.");
            }
        }

        public unsafe void Destroy()
        {
            fixed (OpusDecoder* pinned = &this)
            {
                OpusNativeMethods.DecoderDestroy(pinned);
            }
        }

        public void Dispose() => Destroy();
    }
}
