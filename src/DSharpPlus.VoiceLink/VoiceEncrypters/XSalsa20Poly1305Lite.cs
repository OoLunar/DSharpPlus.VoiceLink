using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using DSharpPlus.VoiceLink.Enums;
using DSharpPlus.VoiceLink.Rtp;
using DSharpPlus.VoiceLink.Sodium;

namespace DSharpPlus.VoiceLink.VoiceEncrypters
{
    public sealed record XSalsa20Poly1305Lite : IVoiceEncrypter
    {
        /// <inheritdoc/>
        public string Name { get; init; } = "xsalsa20_poly1305_lite";

        /// <inheritdoc/>
        public int NonceSize { get; init; } = 4;

        /// <inheritdoc/>
        public EncryptionMode EncryptionMode { get; init; } = EncryptionMode.XSalsa20Poly1305Lite;

        /// <inheritdoc/>
        public ConcurrentDictionary<uint, uint> NonceCounter { get; } = new();

        public int GetEncryptedSize(int length) => length + SodiumXSalsa20Poly1305.MacSize;
        public int GetDecryptedSize(int length) => length - SodiumXSalsa20Poly1305.MacSize;

        public bool TryEncryptOpusPacket(VoiceLinkUser voiceLinkUser, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> target)
        {
            if (data.Length < SodiumXSalsa20Poly1305.MacSize)
            {
                throw new ArgumentException($"The data must have a minimum size of {SodiumXSalsa20Poly1305.MacSize} bytes.", nameof(data));
            }
            else if (key.Length < SodiumXSalsa20Poly1305.KeySize)
            {
                throw new ArgumentException($"The secret key must have a minimum size of {SodiumXSalsa20Poly1305.KeySize} bytes.", nameof(key));
            }
            else if (target.Length < GetEncryptedSize(data.Length))
            {
                throw new ArgumentException($"Target buffer must have a minimum size of {GetEncryptedSize(data.Length)} bytes.", nameof(target));
            }

            // Grab the nonce
            Span<byte> nonce = stackalloc byte[SodiumXSalsa20Poly1305.NonceSize];
            BinaryPrimitives.WriteUInt32LittleEndian(nonce, NonceCounter.AddOrUpdate(voiceLinkUser.Ssrc, 0, (_, v) => v + 1));
            nonce[..4].CopyTo(target[12..16]);

            // Encrypt the data
            return SodiumXSalsa20Poly1305.Encrypt(data, key, nonce, target[16..]) == 0;
        }

        public bool TryDecryptOpusPacket(VoiceLinkUser voiceLinkUser, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> target)
        {
            if (data.Length < SodiumXSalsa20Poly1305.MacSize)
            {
                throw new ArgumentException($"The data must have a minimum size of {SodiumXSalsa20Poly1305.MacSize} bytes.", nameof(data));
            }
            else if (key.Length < SodiumXSalsa20Poly1305.KeySize)
            {
                throw new ArgumentException($"The secret key must have a minimum size of {SodiumXSalsa20Poly1305.KeySize} bytes.", nameof(key));
            }
            else if (target.Length < GetDecryptedSize(data.Length))
            {
                throw new ArgumentException($"Target buffer must have a minimum size of {GetDecryptedSize(data.Length)} bytes.", nameof(target));
            }

            // Grab the nonce
            Span<byte> nonce = stackalloc byte[SodiumXSalsa20Poly1305.NonceSize];
            data[^4..].CopyTo(nonce);

            // Decrypt the data
            return SodiumXSalsa20Poly1305.Decrypt(data[12..^4], key, nonce, target) == 0;
        }

        public bool TryDecryptReportPacket(RtcpHeader header, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> target)
        {
            if (data.Length < SodiumXSalsa20Poly1305.MacSize)
            {
                throw new ArgumentException($"The data must have a minimum size of {SodiumXSalsa20Poly1305.MacSize} bytes.", nameof(data));
            }
            else if (key.Length < SodiumXSalsa20Poly1305.KeySize)
            {
                throw new ArgumentException($"The secret key must have a minimum size of {SodiumXSalsa20Poly1305.KeySize} bytes.", nameof(key));
            }
            else if (target.Length < GetDecryptedSize(data.Length))
            {
                throw new ArgumentException($"Target buffer must have a minimum size of {GetDecryptedSize(data.Length)} bytes.", nameof(target));
            }

            // Grab the nonce
            Span<byte> nonce = stackalloc byte[SodiumXSalsa20Poly1305.NonceSize];
            data[^4..].CopyTo(nonce);

            // Decrypt the data
            return SodiumXSalsa20Poly1305.Decrypt(data[8..^4], key, nonce, target) == 0;
        }
    }
}
