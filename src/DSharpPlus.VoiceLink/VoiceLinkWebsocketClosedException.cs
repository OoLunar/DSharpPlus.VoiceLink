using System;

namespace DSharpPlus.VoiceLink
{
    public sealed class VoiceLinkWebsocketClosedException : Exception
    {
        public VoiceLinkWebsocketClosedException() { }
        public VoiceLinkWebsocketClosedException(string? message) : base(message) { }
        public VoiceLinkWebsocketClosedException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
