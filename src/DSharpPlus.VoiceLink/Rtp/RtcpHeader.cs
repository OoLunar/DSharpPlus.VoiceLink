using System;
using System.Buffers.Binary;

namespace DSharpPlus.VoiceLink.Rtp
{
    public readonly record struct RtcpHeader
    {
        /// <summary>
        /// Gets the version of the RTCP header.
        /// </summary>
        public int Version { get; init; }

        /// <summary>
        /// Gets whether the RTCP header has padding.
        /// </summary>
        public int Padding { get; init; }

        /// <summary>
        /// Gets the report count of the RTCP header.
        /// </summary>
        public int ReportCount { get; init; }

        /// <summary>
        /// Gets the packet type of the RTCP header.
        /// </summary>
        public int PacketType { get; init; }

        /// <summary>
        /// Gets the length of the RTCP header.
        /// </summary>
        public int Length { get; init; }

        /// <summary>
        /// Gets the SSRC of the RTCP header.
        /// </summary>
        public uint Ssrc { get; init; }

        public RtcpHeader(ReadOnlySpan<byte> data)
        {
            if (data.Length < 8)
            {
                throw new ArgumentException("The source buffer must have a minimum of 8 bytes for it to be a RTCP header.", nameof(data));
            }
            else if (data[1] != 201)
            {
                throw new ArgumentException("The source buffer must contain a RTCP receiver report.", nameof(data));
            }

            Version = data[0] >> 6;
            Padding = (data[0] >> 5) & 0b00000001;
            ReportCount = data[0] & 0b00011111;
            PacketType = data[1];
            Length = BinaryPrimitives.ReadUInt16BigEndian(data[2..4]);
            Ssrc = BinaryPrimitives.ReadUInt32BigEndian(data[4..8]);
        }
    }
}
