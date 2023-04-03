namespace DSharpPlus.VoiceLink.Enums
{
    public enum ConnectionState
    {
        None,
        Identify,
        SelectProtocol,
        Heartbeating,
        Resuming,
        Reconnecting
    }
}
