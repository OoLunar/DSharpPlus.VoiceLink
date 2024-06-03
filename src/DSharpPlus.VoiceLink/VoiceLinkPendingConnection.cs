using System.Threading;
using DSharpPlus.EventArgs;

namespace DSharpPlus.VoiceLink
{
    internal class VoiceLinkPendingConnection
    {
        public SemaphoreSlim Semaphore { get; init; } = new(0, 2);
        public VoiceStateUpdatedEventArgs? VoiceStateUpdateEventArgs { get; internal set; }
        public VoiceServerUpdatedEventArgs? VoiceServerUpdateEventArgs { get; internal set; }
    }
}
