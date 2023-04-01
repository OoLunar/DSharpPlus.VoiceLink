using System.Runtime.InteropServices;

namespace DSharpPlus.VoiceLink.Opus
{
    internal static partial class OpusNativeMethods
    {
        /// <summary>
        /// Parse an opus packet into one or more frames. Opus_decode will perform this operation internally so most applications do not need to use this function. This function does not copy the frames, the returned pointers are pointers into the input packet.
        /// </summary>
        /// <param name="data">Opus packet to be parsed.</param>
        /// <param name="length">Size of <paramref name="data"/>.</param>
        /// <param name="toc">TOC pointer.</param>
        /// <param name="frames">Encapsulated frames.</param>
        /// <param name="size">Sizes of the encapsulated frames.</param>
        /// <param name="payloadOffset">Returns the position of the payload within the packet (in bytes).</param>
        /// <returns>Number of frames.</returns>
        [LibraryImport("opus", EntryPoint = "opus_packet_parse")]
        public static unsafe partial int PacketParse(byte* data, int length, byte* toc, byte* frames, int* size, int* payloadOffset);

        /// <summary>
        /// Gets the bandwidth of an Opus packet.
        /// </summary>
        /// <param name="data">Opus packet.</param>
        [LibraryImport("opus", EntryPoint = "opus_packet_get_bandwidth")]
        public static unsafe partial OpusPacketBandwidth PacketGetBandwidth(byte* data);

        /// <summary>
        /// Gets the number of samples per frame from an Opus packet.
        /// </summary>
        /// <param name="data">Opus packet. This must contain at least one byte of data.</param>
        /// <param name="sampleRate">Sampling rate in Hz. This must be a multiple of 400, or inaccurate results will be returned.</param>
        /// <returns>Number of samples per frame.</returns>
        [LibraryImport("opus", EntryPoint = "opus_packet_get_samples_per_frame")]
        public static unsafe partial int PacketGetSamplesPerFrame(byte* data, int sampleRate);

        /// <summary>
        /// Gets the number of channels from an Opus packet.
        /// </summary>
        /// <param name="data">Opus packet.</param>
        /// <returns>Number of channels or <see cref="OpusErrorCode.InvalidPacket"/>.</returns>
        [LibraryImport("opus", EntryPoint = "opus_packet_get_nb_channels")]
        public static unsafe partial int PacketGetChannelCount(byte* data);

        /// <summary>
        /// Gets the number of frames in an Opus packet.
        /// </summary>
        /// <param name="data">Opus packet.</param>
        /// <param name="length">Length of packet.</param>
        /// <returns>Number of frames or <see cref="OpusErrorCode.BadArg"/> <see cref="OpusErrorCode.InvalidPacket"/>.</returns>
        [LibraryImport("opus", EntryPoint = "opus_packet_get_nb_frames")]
        public static unsafe partial int PacketGetFrameCount(byte* data, int length);

        /// <param name="sampleRate">Sampling rate in Hz. This must be a multiple of 400, or inaccurate results will be returned.</param>
        /// <inheritdoc cref="DecoderGetSampleCount(OpusDecoder*, byte*, int)"/>
        [LibraryImport("opus", EntryPoint = "opus_packet_get_nb_samples")]
        public static unsafe partial int PacketGetSampleCount(byte* data, int length, int sampleRate);
    }
}
