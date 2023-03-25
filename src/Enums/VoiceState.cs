using System;

namespace DSharpPlus.VoiceLink.Enums
{
    [Flags]
    public enum VoiceState
    {
        None = 0,
        Speaking = 1 << 0,
        UserMuted = 1 << 1,
        UserDeafened = 1 << 2,
        ServerMuted = 1 << 3,
        ServerDeafened = 1 << 4
    }
}
