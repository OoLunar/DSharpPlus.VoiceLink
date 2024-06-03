using System;
using DSharpPlus.VoiceLink.Rtp;

namespace DSharpPlus.VoiceLink.VoiceEncryptionCiphers
{
    public interface IVoiceEncryptionCipher
    {
        string Name { get; }

        int GetEncryptedSize(int length);
        int GetDecryptedSize(int length);
        bool TryEncryptOpusPacket(VoiceLinkUser voiceLinkUser, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> target);
        bool TryDecryptOpusPacket(VoiceLinkUser voiceLinkUser, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> target);
        bool TryDecryptReportPacket(RtcpHeader header, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> target) => false;
    }
}
