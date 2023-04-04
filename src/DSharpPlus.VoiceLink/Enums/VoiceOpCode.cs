namespace DSharpPlus.VoiceLink.Enums
{
    public enum VoiceOpCode
    {
        Identify = 0,
        SelectProtocol = 1,
        Ready = 2,
        Heartbeat = 3,
        SessionDescription = 4,
        Speaking = 5,
        HeartbeatAck = 6,
        Resume = 7,
        Hello = 8,
        Resumed = 9,
        ClientConnected = 12,
        ClientDisconnect = 13
    }
}
