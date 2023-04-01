using System.Runtime.InteropServices;

namespace DSharpPlus.VoiceLink.Opus
{
    internal static partial class OpusNativeMethods
    {
        /// <summary>
        /// Gets the size of an <see cref="OpusDecoder"/> structure.
        /// </summary>
        /// <param name="channels">Number of channels. This must be 1 or 2.</param>
        /// <returns>The size in bytes.</returns>
        [LibraryImport("opus", EntryPoint = "opus_decoder_get_size")]
        public static unsafe partial int DecoderGetSize(int channels);

        /// <summary>
        /// Allocates and initializes a decoder state.
        /// </summary>
        /// <remarks>Internally Opus stores data at 48000 Hz, so that should be the default value for Fs. However, the decoder can efficiently decode to buffers at 8, 12, 16, and 24 kHz so if for some reason the caller cannot use data at the full sample rate, or knows the compressed data doesn't use the full frequency range, it can request decoding at a reduced rate. Likewise, the decoder is capable of filling in either mono or interleaved stereo pcm buffers, at the caller's request.</remarks>
        /// <param name="sampleRate">Sample rate to decode at (Hz).</param>
        /// <param name="channels">Number of channels (1 or 2) to decode.</param>
        /// <returns><see cref="OpusErrorCode.Ok"/> or other error codes.</returns>
        [LibraryImport("opus", EntryPoint = "opus_decoder_create")]
        public static unsafe partial OpusDecoder* DecoderCreate(OpusSampleRate sampleRate, int channels, out OpusErrorCode error);

        /// <summary>
        /// Initializes a previously allocated decoder state. The state must be at least the size returned by <see cref="DecoderGetSize(int)"/>. This is intended for applications which use their own allocator instead of malloc.
        /// </summary>
        /// <param name="decoder">Decoder state.</param>
        /// <param name="sampleRate">Sampling rate to decode to (Hz).</param>
        /// <param name="channels">Number of channels (1 or 2) to decode.</param>
        /// <returns><see cref="OpusErrorCode.Ok"/> or other error codes.</returns>
        [LibraryImport("opus", EntryPoint = "opus_decoder_init")]
        public static unsafe partial OpusErrorCode DecoderInit(OpusDecoder* decoder, OpusSampleRate sampleRate, int channels);

        /// <summary>
        /// Decode an Opus packet.
        /// </summary>
        /// <param name="decoder">Decoder state.</param>
        /// <param name="data">Input payload. Use a <see cref="IntPtr.Zero"/> (<see langword="null"/>) pointer to indicate packet loss.</param>
        /// <param name="length">Number of bytes in <paramref name="data"/>.</param>
        /// <param name="pcm">Output signal (interleaved if 2 channels). Length is frame_size * channels * sizeof(<see cref="Int16"/>)</param>
        /// <param name="frameSize">Number of samples per channel of available space in <paramref name="pcm"/>. If this is less than the maximum packet duration (120ms; 5760 for 48kHz), this function will not be capable of decoding some packets. In the case of PLC (<paramref name="data"/> is <see langword="null"/>) or FEC (<paramref name="decodeFec"/> is <see langword="true"/>), then <paramref name="frameSize"/> needs to be exactly the duration of audio that is missing, otherwise the decoder will not be in the optimal state to decode the next incoming packet. For the PLC and FEC cases, <paramref name="frameSize"/> must be a multiple of 2.5 ms.</param>
        /// <param name="decodeFec">Flag (0 or 1) to request that any in-band forward error correction data be decoded. If no such data is available, the frame is decoded as if it were lost.</param>
        /// <returns>Number of decoded samples or an <see cref="OpusErrorCode"/></returns>
        [LibraryImport("opus", EntryPoint = "opus_decode")]
        public static unsafe partial int Decode(OpusDecoder* decoder, byte* data, int length, byte* pcm, int frameSize, int decodeFec);

        /// <inheritdoc cref="Decode(OpusDecoder*, byte*, int, byte*, int, int)"/>
        [LibraryImport("opus", EntryPoint = "opus_decode_float")]
        public static unsafe partial int DecodeFloat(OpusDecoder* decoder, byte* data, int length, byte* pcm, int frameSize, int decodeFec);

        /// <summary>
        /// Perform a CTL function on an Opus decoder.
        /// </summary>
        /// <remarks>VoiceLink: This method is a varargs method, and as such, it is not supported by the C# language. There are no known alternatives at the time of writing.</remarks>
        /// <param name="decoder">Decoder state.</param>
        /// <param name="request">This and all remaining parameters should be replaced by one of the convenience macros in Generic CTLs or Decoder related CTLs.</param>
        [LibraryImport("opus", EntryPoint = "opus_decoder_ctl")]
        public static unsafe partial OpusErrorCode DecoderControl(OpusDecoder* decoder, OpusControlRequest request, int value);

        /// <summary>
        /// Frees an OpusDecoder allocated by <see cref="DecoderCreate(OpusSampleRate, int, out OpusErrorCode)"/>.
        /// </summary>
        /// <param name="decoder">State to be freed.</param>
        [LibraryImport("opus", EntryPoint = "opus_decoder_destroy")]
        public static unsafe partial void DecoderDestroy(OpusDecoder* decoder);

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

        /// <summary>
        /// Gets the number of samples of an Opus packet.
        /// </summary>
        /// <param name="decoder">Decoder state.</param>
        /// <param name="data">Opus packet.</param>
        /// <param name="length">Length of packet.</param>
        /// <returns>Number of samples or <see cref="OpusErrorCode.BadArg"/> or <see cref="OpusErrorCode.InvalidPacket"/>.</returns>
        [LibraryImport("opus", EntryPoint = "opus_decoder_get_nb_samples")]
        public static unsafe partial int DecoderGetSampleCount(OpusDecoder* decoder, byte* data, int length);

        /// <summary>
        /// Applies soft-clipping to bring a float signal within the [-1,1] range. If the signal is already in that range, nothing is done. If there are values outside of [-1,1], then the signal is clipped as smoothly as possible to both fit in the range and avoid creating excessive distortion in the process.
        /// </summary>
        /// <param name="pcm">Input PCM and modified PCM.</param>
        /// <param name="frameSize">Number of samples per channel to process.</param>
        /// <param name="channels">Number of channels.</param>
        /// <param name="softclipMem">State memory for the soft clipping process (one float per channel, initialized to zero).</param>
        [LibraryImport("opus", EntryPoint = "opus_pcm_soft_clip")]
        public static unsafe partial void PcmSoftClip(byte* pcm, int frameSize, int channels, int softclipMem);
    }
}
