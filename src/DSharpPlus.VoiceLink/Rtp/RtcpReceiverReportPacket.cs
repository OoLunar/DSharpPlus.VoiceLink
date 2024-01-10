using System;
using System.Collections.Generic;

namespace DSharpPlus.VoiceLink.Rtp
{
    public readonly record struct RtcpReceiverReportPacket
    {
        /// <summary>
        /// Gets the header of the RTCP packet.
        /// </summary>
        public RtcpHeader Header { get; init; }

        /// <summary>
        /// Gets the report blocks of the RTCP packet.
        /// </summary>
        public IReadOnlyList<RtcpReportBlock> ReportBlocks { get; init; }

        public RtcpReceiverReportPacket(RtcpHeader header, ReadOnlySpan<byte> data)
        {
            List<RtcpReportBlock> reportBlocks = new(header.ReportCount);
            for (int i = 0; i < header.ReportCount; i++)
            {
                reportBlocks.Add(new RtcpReportBlock(data));
                data = data[24..];
            }

            Header = header;
            ReportBlocks = reportBlocks;
        }
    }
}
