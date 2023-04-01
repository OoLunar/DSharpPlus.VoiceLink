using System.Runtime.InteropServices;

namespace DSharpPlus.VoiceLink.Opus
{
    internal static class OpusNativeMethods
    {
        /// <summary>
        /// Gets the size of an <see cref="OpusEncoder"/> structure.
        /// </summary>
        /// <param name="channels">Number of channels. This must be 1 or 2.</param>
        /// <returns>The size in bytes.</returns>
        [LibraryImport("opus", EntryPoint = "opus_encoder_get_size")]
        public static extern unsafe int EncoderGetSize(int channels);

        /// <summary>
        /// Allocates and initializes an encoder state.
        /// </summary>
        /// <param name="sampleRate">Sampling rate of input signal (Hz).</param>
        /// <param name="channels">Number of channels (1 or 2) in input signal.</param>
        /// <param name="application">Coding mode.</param>
        /// <param name="error">Error codes.</param>
        /// <remarks>Regardless of the sampling rate and number channels selected, the Opus encoder can switch to a lower audio bandwidth or number of channels if the bitrate selected is too low. This also means that it is safe to always use 48 kHz stereo input and let the encoder optimize the encoding.</remarks>
        /// <returns>Returns a new <see cref="OpusEncoder"/> object.</returns>
        [LibraryImport("opus", EntryPoint = "opus_encoder_create")]
        public static extern unsafe OpusEncoder* EncoderCreate(OpusSampleRate sampleRate, int channels, OpusApplication application, out OpusError* error);

        /// <summary>
        /// Initializes a previously allocated encoder state The memory pointed to by st must be at least the size returned by <see cref="EncoderGetSize(int)"/>. This is intended for applications which use their own allocator instead of malloc.
        /// </summary>
        /// <param name="encoder">Encoder state.</param>
        /// <param name="sampleRate">Sampling rate of input signal (Hz).</param>
        /// <param name="channels">Number of channels (1 or 2) in input signal.</param>
        /// <param name="application">Coding mode.</param>
        /// <returns>Success or Error codes.</returns>
        [LibraryImport("opus", EntryPoint = "opus_encoder_init")]
        public static extern unsafe OpusError EncoderInit(OpusEncoder* encoder, OpusSampleRate sampleRate, int channels, OpusApplication application);

        /// <summary>
        /// Encodes an Opus frame.
        /// </summary>
        /// <param name="encoder">Encoder state.</param>
        /// <param name="pcm">Input signal (interleaved if 2 channels). Length is <c>frame_size * channels * sizeof(opus_int16)</c>.</param>
        /// <param name="frameSize">Number of samples per channel in the input signal. This must be an Opus frame size for the encoder's sampling rate. For example, at 48 kHz the permitted values are 120, 240, 480, 960, 1920, and 2880. Passing in a duration of less than 10 ms (480 samples at 48 kHz) will prevent the encoder from using the LPC or hybrid modes.</param>
        /// <param name="data">Output payload. This must contain storage for at least <paramref name="maxDataBytes"/>.</param>
        /// <param name="maxDataBytes">Size of the allocated memory for the output payload. This may be used to impose an upper limit on the instant bitrate, but should not be used as the only bitrate control. Use OPUS_SET_BITRATE to control the bitrate.</param>
        /// <returns>The length of the encoded packet (in bytes) on success or a negative error code (see Error codes) on failure.</returns>
        [LibraryImport("opus", EntryPoint = "opus_encode")]
        public static extern unsafe int Encode(OpusEncoder* encoder, short* pcm, int frameSize, out byte* data, int maxDataBytes);

        /// <inheritdoc cref="Encode(OpusEncoder*, short*, int, byte*, int)"/>
        /// <summary>
        /// Encodes an Opus frame from floating point input.
        /// </summary>
        /// <param name="pcm">Input in float format (interleaved if 2 channels), with a normal range of +/-1.0. Samples with a range beyond +/-1.0 are supported but will be clipped by decoders using the integer API and should only be used if it is known that the far end supports extended dynamic range. Length is <c>frame_size * channels * sizeof(<see cref="float"/>)</c>.</param>
        /// <param name="frameSize">Number of samples per channel in the input signal. This must be an Opus frame size for the encoder's sampling rate. For example, at 48 kHz the permitted values are 120, 240, 480, 960, 1920, and 2880. Passing in a duration of less than 10 ms (480 samples at 48 kHz) will prevent the encoder from using the LPC or hybrid modes.</param>
        /// <param name="maxDataBytes">Size of the allocated memory for the output payload. This may be used to impose an upper limit on the instant bitrate, but should not be used as the only bitrate control. Use OPUS_SET_BITRATE to control the bitrate.</param>
        [LibraryImport("opus", EntryPoint = "opus_encode_float")]
        public static extern unsafe int EncodeFloat(OpusEncoder* encoder, short* pcm, int frameSize, out byte* data, int maxDataBytes);

        /// <summary>
        /// Frees an <see cref="OpusEncoder"/> allocated by <see cref="EncoderCreate(int, int, int, out OpusError)"/>.
        /// </summary>
        /// <param name="encoder">State to be freed.</param>
        [LibraryImport("opus", EntryPoint = "opus_encoder_destroy")]
        public static extern unsafe void EncoderDestroy(OpusEncoder* encoder);

        /// <summary>
        /// Perform a CTL function on an <see cref="OpusEncoder"/>. Generally the request and subsequent arguments are generated by a convenience macro.
        /// </summary>
        /// <param name="encoder">Encoder state.</param>
        /// <param name="request">This and all remaining parameters should be replaced by one of the convenience macros in Generic CTLs or Encoder related CTLs.</param>
        [LibraryImport("opus", EntryPoint = "opus_encoder_ctl")]
        // TODO: VarArgs method, try passing `object[]` or `params object[]`
        public static extern unsafe OpusError EncoderControl(OpusEncoder* encoder, OpusControl request, int value);
    }
}
