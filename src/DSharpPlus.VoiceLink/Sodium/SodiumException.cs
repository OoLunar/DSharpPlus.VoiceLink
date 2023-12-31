using System;

namespace DSharpPlus.VoiceLink.Sodium
{
    public class SodiumException : Exception
    {
        public SodiumException() { }
        public SodiumException(string message) : base(message) { }
        public SodiumException(string message, Exception inner) : base(message, inner) { }
    }
}
