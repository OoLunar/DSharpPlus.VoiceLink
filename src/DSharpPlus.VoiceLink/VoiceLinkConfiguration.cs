using System;
using DSharpPlus.VoiceLink.AudioCodecs;
using DSharpPlus.VoiceLink.VoiceEncryptionCiphers;

namespace DSharpPlus.VoiceLink
{
    public sealed record VoiceLinkConfiguration
    {
        /// <summary>
        /// How many missed heartbeats before the voice client attempts to reconnect.
        /// </summary>
        public int MaxHeartbeatQueueSize { get; set; } = 5;

        /// <summary>
        /// Which voice encryption cipher to use for voice data encryption/decryption.
        /// </summary>
        public IVoiceEncryptionCipher VoiceEncryptionCipher { get; set; } = new XSalsa20Poly1305EncryptionCipher();

        /// <summary>
        /// A delegate which creates a new audio codec instance. The audio codec is responsible for encoding and decoding audio data into the user's desired format.
        /// </summary>
        public AudioCodecFactory AudioCodecFactory { get; set; } = _ => new Pcm16BitAudioCodec();

        /// <summary>
        /// When <see cref="VoiceLinkConnection.StartSpeakingAsync"/> should timeout after attempting to read <see cref="VoiceLinkConnection.AudioInput"/> for too long.
        /// </summary>
        public TimeSpan SpeakingTimeout { get; set; } = TimeSpan.FromMilliseconds(200);
    }
}
