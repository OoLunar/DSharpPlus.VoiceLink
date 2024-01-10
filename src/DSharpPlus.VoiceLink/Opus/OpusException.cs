using System;

namespace DSharpPlus.VoiceLink.Opus
{
    /// <summary>
    /// Thrown by any Opus method that could fail.
    /// </summary>
    public sealed class OpusException : Exception
    {
        /// <summary>
        /// The error code that any Opus method could have failed with.
        /// </summary>
        public OpusErrorCode ErrorCode { get; init; }

        public OpusException(OpusErrorCode errorCode) : base(GetErrorMessage(errorCode)) => ErrorCode = errorCode;
        public OpusException(OpusErrorCode errorCode, string message) : base(message) => ErrorCode = errorCode;
        public OpusException(OpusErrorCode errorCode, string message, Exception inner) : base(message, inner) => ErrorCode = errorCode;

        /// <summary>
        /// Matches an error code to a human-readable error message.
        /// </summary>
        /// <param name="errorCode">The error code to match.</param>
        /// <returns>A human-readable error message.</returns>
        public static string GetErrorMessage(OpusErrorCode errorCode) => errorCode switch
        {
            OpusErrorCode.Ok => "Error 0: No error.",
            OpusErrorCode.BadArg => "Error -1: One or more invalid/out of range arguments.",
            OpusErrorCode.BufferTooSmall => "Error -2: Not enough bytes allocated in the buffer.",
            OpusErrorCode.InternalError => "Error -3: An internal error was detected.",
            OpusErrorCode.InvalidPacket => "Error -4: The compressed data passed is corrupted.",
            OpusErrorCode.Unimplemented => "Error -5: Invalid/unsupported request number.",
            OpusErrorCode.InvalidState => "Error -6: An encoder or decoder structure is invalid or already freed.",
            OpusErrorCode.AllocFail => "Error -7: Memory allocation has failed.",
            _ => $"Error {(int)errorCode}: Unknown error."
        };
    }
}
