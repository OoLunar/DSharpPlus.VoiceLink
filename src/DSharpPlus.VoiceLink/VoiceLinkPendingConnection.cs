using System.Threading;
using DSharpPlus.EventArgs;

namespace DSharpPlus.VoiceLink
{
    internal class VoiceLinkPendingConnection
    {
        public SemaphoreSlim Semaphore { get; init; } = new(0, 2);
        public VoiceStateUpdateEventArgs? VoiceStateUpdateEventArgs { get; internal set; }
        public VoiceServerUpdateEventArgs? VoiceServerUpdateEventArgs { get; internal set; }
    }
}
