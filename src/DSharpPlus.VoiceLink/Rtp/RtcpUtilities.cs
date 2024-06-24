using System;

namespace DSharpPlus.VoiceLink.Rtp
{
    public static class RtcpUtilities
    {
        /// <summary>
        /// Determines if the given buffer contains a valid RTCP header.
        /// </summary>
        /// <param name="source">The data to reference.</param>
        /// <returns>Whether the data contains a valid RTCP header.</returns>
        public static bool HasRtcpReceiverReport(ReadOnlySpan<byte> source) => source.Length >= 8 && source[1] == 201;

        public static RtcpHeader DecodeHeader(ReadOnlySpan<byte> source)
        {
            if (source.Length < 8)
            {
                throw new ArgumentException("The source buffer must have a minimum of 8 bytes for it to be a RTCP header.", nameof(source));
            }
            else if (source[1] != 201)
            {
                throw new ArgumentException("The source buffer must contain a RTCP receiver report.", nameof(source));
            }

            return new RtcpHeader(source);
        }
    }
}
