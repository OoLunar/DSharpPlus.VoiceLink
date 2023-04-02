using System.Runtime.InteropServices;

namespace DSharpPlus.VoiceLink.Sodium
{
    internal static partial class SodiumNativeMethods
    {
        static SodiumNativeMethods()
        {
            if (Init() == -1)
            {
                throw new SodiumException("Sodium initialization failed.");
            }
        }

        [LibraryImport("libsodium", EntryPoint = "sodium_init")]
        public static unsafe partial int Init();

        [LibraryImport("libsodium", EntryPoint = "crypto_secretbox_xsalsa20poly1305_keybytes")]
        public static unsafe partial int SecretBoxXSalsa20Poly1305KeyBytes();

        [LibraryImport("libsodium", EntryPoint = "crypto_secretbox_xsalsa20poly1305_noncebytes")]
        public static unsafe partial int SecretBoxXSalsa20Poly1305NonceBytes();

        [LibraryImport("libsodium", EntryPoint = "crypto_secretbox_xsalsa20poly1305_macbytes")]
        public static unsafe partial int SecretBoxXSalsa20Poly1305MacBytes();

        [LibraryImport("libsodium", EntryPoint = "crypto_secretbox_easy")]
        public static unsafe partial int SecretBoxEasy(byte* buffer, byte* message, ulong messageLength, byte* nonce, byte* key);

        [LibraryImport("libsodium", EntryPoint = "crypto_secretbox_open_easy")]
        public static unsafe partial int SecretBoxOpenEasy(byte* buffer, byte* encryptedMessage, ulong encryptedLength, byte* nonce, byte* key);
    }
}
