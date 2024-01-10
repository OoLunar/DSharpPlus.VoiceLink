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
        bool TryEncryptOpusPacket(VoiceLinkUser voiceLinkUser, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> target);
        bool TryDecryptOpusPacket(VoiceLinkUser voiceLinkUser, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> target);
        bool TryDecryptReportPacket(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> target) => false;
    }
}
