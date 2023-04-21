using System;
using DSharpPlus.VoiceLink.Enums;

namespace DSharpPlus.VoiceLink.VoiceEncrypters
{
    public interface IVoiceEncrypter
    {
        string Name { get; init; }
        EncryptionMode EncryptionMode { get; init; }

        int GetEncryptedSize(int length);
        int GetDecryptedSize(int length);
        bool Encrypt(VoiceLinkUser voiceLinkUser, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> target);
        bool Decrypt(VoiceLinkUser voiceLinkUser, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> target);
    }
}
