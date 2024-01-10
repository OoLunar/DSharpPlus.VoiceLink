namespace DSharpPlus.VoiceLink.Rtp
{
    public readonly record struct RtpHeader
    {
        public byte FirstMetadata { get; init; }
        public byte SecondMetadata { get; init; }
        public ushort Sequence { get; init; }
        public uint Timestamp { get; init; }
        public uint Ssrc { get; init; }

        // The version is the first two bits of the first byte.
        public byte Version => (byte)(FirstMetadata & 0b11000000);

        // The padding bit is the third bit of the first byte.
        public bool HasPadding => (FirstMetadata & 0b00100000) != 0;

        // The extension bit is the fourth bit of the first byte.
        public bool HasExtension => (FirstMetadata & 0b00010000) != 0;

        // The CSRC count is the last four bits of the first byte.
        public byte CsrcCount => (byte)((FirstMetadata & 0b00001111) >> 4);

        // The marker bit is the first bit of the second byte.
        public bool HasMarker => (SecondMetadata & 0b10000000) != 0;

        // The payload type is the last seven bits of the second byte.
        public byte PayloadType => (byte)(SecondMetadata & 0b01111111);
    }
}
