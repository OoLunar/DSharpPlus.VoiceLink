using System;
using System.Runtime.Serialization;

namespace DSharpPlus.VoiceLink.Opus
{
    /// <summary>
    /// Thrown by any Opus method that could fail.
    /// </summary>
    [Serializable]
    public sealed class OpusException : Exception
    {
        /// <summary>
        /// The error code that any Opus method could have failed with.
        /// </summary>
        public OpusErrorCode ErrorCode { get; init; }

        public OpusException(OpusErrorCode errorCode) : base(GetErrorMessage(errorCode)) => ErrorCode = errorCode;
        public OpusException(OpusErrorCode errorCode, string message) : base(message) => ErrorCode = errorCode;
        public OpusException(OpusErrorCode errorCode, string message, Exception inner) : base(message, inner) => ErrorCode = errorCode;
        private OpusException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public static string GetErrorMessage(OpusErrorCode errorCode) => errorCode switch
        {
            OpusErrorCode.Ok => "No error.",
            OpusErrorCode.BadArg => "One or more invalid/out of range arguments.",
            OpusErrorCode.BufferTooSmall => "Not enough bytes allocated in the buffer.",
            OpusErrorCode.InternalError => "An internal error was detected.",
            OpusErrorCode.InvalidPacket => "The compressed data passed is corrupted.",
            OpusErrorCode.Unimplemented => "Invalid/unsupported request number.",
            OpusErrorCode.InvalidState => "An encoder or decoder structure is invalid or already freed.",
            OpusErrorCode.AllocFail => "Memory allocation has failed.",
            _ => "Unknown error."
        };
    }
}
