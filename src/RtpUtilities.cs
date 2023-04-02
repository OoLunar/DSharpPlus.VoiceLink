using System;
using System.Buffers.Binary;
using DSharpPlus.VoiceLink.Enums;

namespace DSharpPlus.VoiceLink
{
    /// <summary>
    /// General helper methods for handling RTP headers.
    /// </summary>
    public static class RtpUtilities
    {
        public const byte VersionFlags = 0x80;
        public const byte PayloadType = 0x78;

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

            target[0] = VersionFlags;
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
        public static void DecodeHeader(ReadOnlySpan<byte> source, out ushort sequence, out uint timestamp, out uint ssrc)
        {
            if (source.Length < 12)
            {
                throw new ArgumentException("The source buffer must have a minimum of 12 bytes for it to be a RTP header.", nameof(source));
            }
            else if (source[0] != VersionFlags)
            {
                throw new ArgumentException("The source buffer contains an unknown RTP header version.", nameof(source));
            }
            else if (source[1] != PayloadType)
            {
                throw new ArgumentException("The source buffer contains an unknown RTP payload type.", nameof(source));
            }

            sequence = BinaryPrimitives.ReadUInt16BigEndian(source[2..4]);
            timestamp = BinaryPrimitives.ReadUInt32BigEndian(source[4..8]);
            ssrc = BinaryPrimitives.ReadUInt32BigEndian(source[8..12]);
        }

        /// <summary>
        /// Determines if the given buffer contains a valid RTP header.
        /// </summary>
        /// <param name="source">The data to reference.</param>
        /// <returns>Whether the data contains a valid RTP header.</returns>
        public static bool IsRtpHeader(ReadOnlySpan<byte> source) => source.Length >= 12 && source[0] == VersionFlags && source[1] == PayloadType;

        /// <summary>
        /// Calculates the size of the RTP packet based on the encrypted length and the encryption mode. The encryption mode determines the size of the nonce and appends it to the encrypted length.
        /// </summary>
        /// <param name="encryptedLength">The length of the encrypted audio.</param>
        /// <param name="encryptionMode">The encryption mode to be used.</param>
        /// <returns>The total size of the packet, that is <paramref name="encryptedLength"/> + the nonce size of <paramref name="encryptionMode"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="encryptionMode"/> is not a valid <see cref="EncryptionMode"/>.</exception>
        public static int CalculatePacketSize(int encryptedLength, EncryptionMode encryptionMode) => encryptionMode switch
        {
            EncryptionMode.XSalsa20Poly1305 => encryptedLength + 12,
            EncryptionMode.XSalsa20Poly1305Suffix => encryptedLength + 16,
            EncryptionMode.XSalsa20Poly1305Lite => encryptedLength + 4,
            _ => throw new ArgumentOutOfRangeException(nameof(encryptionMode), encryptionMode, null)
        };

        /// <summary>
        /// Removes the RTP header from the packet, returning the data.
        /// </summary>
        /// <param name="source">The complete packet to reference.</param>
        /// <param name="data">The encrypted audio.</param>
        /// <param name="encryptionMode">The encryption mode used when encrypting the data.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="encryptionMode"/> is not a valid <see cref="EncryptionMode"/>.</exception>
        public static void SlicePacketData(ReadOnlySpan<byte> source, out ReadOnlySpan<byte> data, EncryptionMode encryptionMode) => data = encryptionMode switch
        {
            EncryptionMode.XSalsa20Poly1305 => source[12..],
            EncryptionMode.XSalsa20Poly1305Suffix => source[12..^4],
            EncryptionMode.XSalsa20Poly1305Lite => source[12..^12],
            _ => throw new ArgumentOutOfRangeException(nameof(encryptionMode), encryptionMode, null)
        };
    }
}
