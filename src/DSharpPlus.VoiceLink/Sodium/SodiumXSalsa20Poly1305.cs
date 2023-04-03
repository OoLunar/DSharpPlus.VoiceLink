using System;

namespace DSharpPlus.VoiceLink.Sodium
{
    public static class SodiumXSalsa20Poly1305
    {
        public static readonly int KeySize = SodiumNativeMethods.SecretBoxXSalsa20Poly1305KeyBytes();
        public static readonly int NonceSize = SodiumNativeMethods.SecretBoxXSalsa20Poly1305NonceBytes();
        public static readonly int MacSize = SodiumNativeMethods.SecretBoxXSalsa20Poly1305MacBytes();

        public static unsafe int Encrypt(ReadOnlySpan<byte> source, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, Span<byte> target)
        {
            fixed (byte* sourcePointer = source)
            fixed (byte* keyPointer = key)
            fixed (byte* noncePointer = nonce)
            fixed (byte* targetPointer = target)
            {
                return SodiumNativeMethods.SecretBoxEasy(targetPointer, sourcePointer, (ulong)source.Length, noncePointer, keyPointer);
            }
        }

        public static unsafe int Decrypt(ReadOnlySpan<byte> source, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, Span<byte> target)
        {
            fixed (byte* sourcePointer = source)
            fixed (byte* keyPointer = key)
            fixed (byte* noncePointer = nonce)
            fixed (byte* targetPointer = target)
            {
                return SodiumNativeMethods.SecretBoxOpenEasy(targetPointer, sourcePointer, (ulong)source.Length, noncePointer, keyPointer);
            }
        }
    }
}
