using System;
using System.Security.Cryptography;
using DSharpPlus.VoiceLink.Rtp;
using DSharpPlus.VoiceLink.Sodium;

namespace DSharpPlus.VoiceLink.VoiceEncryptionCiphers
{
    public sealed record XSalsa20Poly1305SuffixEncryptionCipher : IVoiceEncryptionCipher
    {
        /// <inheritdoc/>
        public string Name => "xsalsa20_poly1305_suffix";
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
            RandomNumberGenerator.Fill(nonce);
            nonce.CopyTo(target[12..36]);

            // Encrypt the data
            return SodiumXSalsa20Poly1305.Encrypt(data, key, nonce, target[36..]) == 0;
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
            data[^24..].CopyTo(nonce);

            // Decrypt the data
            return SodiumXSalsa20Poly1305.Decrypt(data[12..^24], key, nonce, target) == 0;
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
            data[^24..].CopyTo(nonce);

            // Decrypt the data
            return SodiumXSalsa20Poly1305.Decrypt(data[8..^24], key, nonce, target) == 0;
        }
    }
}
