using System;
using System.Runtime.Serialization;

namespace DSharpPlus.VoiceLink.Sodium
{
    [Serializable]
    public class SodiumException : Exception
    {
        public SodiumException() { }
        public SodiumException(string message) : base(message) { }
        public SodiumException(string message, Exception inner) : base(message, inner) { }
        protected SodiumException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
