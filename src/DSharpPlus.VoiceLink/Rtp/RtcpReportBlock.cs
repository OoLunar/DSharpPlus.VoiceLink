using System;
using System.Buffers.Binary;

namespace DSharpPlus.VoiceLink.Rtp
{
    public readonly record struct RtcpReportBlock
    {
        public ushort SynchronizationSource { get; }
        public ushort FractionLost { get; }
        public uint CumulativePacketsLost { get; }
        public uint ExtendedHighestSequenceNumberReceived { get; }
        public uint InterarrivalJitter { get; }
        public uint LastSenderReport { get; }
        public uint DelaySinceLastSenderReport { get; }

        public RtcpReportBlock(ReadOnlySpan<byte> data)
        {
            SynchronizationSource = BinaryPrimitives.ReadUInt16BigEndian(data);
            FractionLost = data[2];
            CumulativePacketsLost = BinaryPrimitives.ReadUInt32BigEndian(data[3..]);
            ExtendedHighestSequenceNumberReceived = BinaryPrimitives.ReadUInt32BigEndian(data[7..]);
            InterarrivalJitter = BinaryPrimitives.ReadUInt32BigEndian(data[11..]);
            LastSenderReport = BinaryPrimitives.ReadUInt32BigEndian(data[15..]);
            DelaySinceLastSenderReport = BinaryPrimitives.ReadUInt32BigEndian(data[19..]);
        }
    }
}
