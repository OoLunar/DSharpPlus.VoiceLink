namespace DSharpPlus.VoiceLink.Rtp
{
    public readonly struct RtpHeader
    {
        public ushort Sequence { get; init; }
        public uint Timestamp { get; init; }
        public uint Ssrc { get; init; }
    }
}
