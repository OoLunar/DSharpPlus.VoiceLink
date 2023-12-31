using System;
using System.Buffers.Binary;

namespace DSharpPlus.VoiceLink.Rtp
{
    /// <summary>
    /// General helper methods for handling RTP headers.
    /// </summary>
    public static class RtpUtilities
    {
        public const byte VersionWithExtension = 0x90;
        public const byte Version = 0x80;
        public const byte PayloadType = 0x78;

        /// <summary>
        /// Determines if the given buffer contains a valid RTP header.
        /// </summary>
        /// <param name="source">The data to reference.</param>
        /// <returns>Whether the data contains a valid RTP header.</returns>
        public static bool IsRtpHeader(ReadOnlySpan<byte> source) => source.Length >= 12 && (source[0] == Version || source[0] == VersionWithExtension) && source[1] == PayloadType;

        /// <summary>
        /// Encodes a RTP header into the given buffer.
        /// </summary>
        /// <param name="sequence">The sequence for this audio frame.</param>
        /// <param name="timestamp">The timestamp of the audio frame.</param>
        /// <param name="ssrc">The srrc of the audio frame.</param>
        /// <param name="target">Which buffer to write to.</param>
        /// <exception cref="ArgumentException">The target buffer must have a minimum of 12 bytes for the RTP header to fit.</exception>
        public static void EncodeHeader(ushort sequence, uint timestamp, uint ssrc, Span<byte> target)
        {
            if (target.Length < 12)
            {
                throw new ArgumentException("The target buffer must have a minimum of 12 bytes for the RTP header to fit.", nameof(target));
            }

            target.Clear();
            target[0] = Version;
            target[1] = PayloadType;
            BinaryPrimitives.WriteUInt16BigEndian(target[2..4], sequence);
            BinaryPrimitives.WriteUInt32BigEndian(target[4..8], timestamp);
            BinaryPrimitives.WriteUInt32BigEndian(target[8..12], ssrc);
        }

        /// <summary>
        /// Attempts to decode the RTP header from the given buffer.
        /// </summary>
        /// <param name="source">The source data; Both the RTP header and the encrypted audio.</param>
        /// <param name="sequence">The sequence number provided from the RTP header.</param>
        /// <param name="timestamp">The timestamp found in the RTP header.</param>
        /// <param name="ssrc">The Ssrc grabbed from the RTP header.</param>
        /// <exception cref="ArgumentException">The source buffer must have a minimum of 12 bytes for it to be a RTP header or contains an unknown RTP header version or type.</exception>
        public static RtpHeader DecodeHeader(ReadOnlySpan<byte> source)
        {
            if (source.Length < 12)
            {
                throw new ArgumentException("The source buffer must have a minimum of 12 bytes for it to be a RTP header.", nameof(source));
            }
            else if (source[0] != Version && source[0] != VersionWithExtension)
            {
                throw new ArgumentException("The source buffer contains an unknown RTP header version.", nameof(source));
            }
            else if (source[1] != PayloadType)
            {
                throw new ArgumentException("The source buffer contains an unknown RTP header type.", nameof(source));
            }

            return new RtpHeader()
            {
                Sequence = BinaryPrimitives.ReadUInt16BigEndian(source[2..4]),
                Timestamp = BinaryPrimitives.ReadUInt32BigEndian(source[4..8]),
                Ssrc = BinaryPrimitives.ReadUInt32BigEndian(source[8..12])
            };
        }
    }
}
