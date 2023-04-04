using System;
using DSharpPlus.AsyncEvents;

namespace DSharpPlus.VoiceLink.EventArgs
{
    public sealed class VoiceLinkConnectionEventArgs : AsyncEventArgs
    {
        public VoiceLinkConnection Connection { get; init; }

        public VoiceLinkConnectionEventArgs(VoiceLinkConnection connection) => Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }
}
