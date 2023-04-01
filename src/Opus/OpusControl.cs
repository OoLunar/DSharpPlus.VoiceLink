namespace DSharpPlus.VoiceLink.Opus
{
    // Docs are from these three pages:
    // https://opus-codec.org/docs/opus_api-1.3.1/group__opus__genericctls.html
    // https://opus-codec.org/docs/opus_api-1.3.1/group__opus__encoderctls.html
    // https://opus-codec.org/docs/opus_api-1.3.1/group__opus__decoderctls.html
    public enum OpusControlRequest
    {
        /// <summary>
        /// Configures the encoder's intended application. The initial value is a mandatory argument to the encoder_create function.
        /// </summary>
        SetApplication = 4000,
        
        /// <summary>
        /// Gets the encoder's configured application.
        /// </summary>
        GetApplication = 4001,
        
        /// <summary>
        /// Configures the bitrate in the encoder. Rates from 500 to 512000 bits per second are meaningful, as well as the special values <see cref="OpusApplication.Auto"/> and OPUS_BITRATE_MAX. The value OPUS_BITRATE_MAX can be used to cause the codec to use as much rate as it can, which is useful for controlling the rate by adjusting the output buffer size.
        /// </summary>
        SetBitrate = 4002,
        
        /// <summary>
        /// Gets the encoder's bitrate configuration.
        /// </summary>
        GetBitrate = 4003,
        
        /// <summary>
        /// Configures the maximum bandpass that the encoder will select automatically. Applications should normally use this instead of <see cref="SetBandwidth"/> (leaving that set to the default, <see cref="OpusApplication.Auto"/>). This allows the application to set an upper bound based on the type of input it is providing, but still gives the encoder the freedom to reduce the bandpass when the bitrate becomes too low, for better overall quality.
        /// </summary>
        SetMaxBandwidth = 4004,
        
        /// <summary>
        /// Gets the encoder's configured maximum allowed bandpass.
        /// </summary>
        GetMaxBandwidth = 4005,
        
        /// <summary>
        /// Enables or disables variable bitrate (VBR) in the encoder. The configured bitrate may not be met exactly because frames must be an integer number of bytes in length.
        /// </summary>
        SetVbr = 4006,
        
        /// <summary>
        /// Determine if variable bitrate (VBR) is enabled in the encoder.
        /// </summary>
        GetVbr = 4007,
        
        /// <summary>
        /// Sets the encoder's bandpass to a specific value. This prevents the encoder from automatically selecting the bandpass based on the available bitrate. If an application knows the bandpass of the input audio it is providing, it should normally use <see cref="SetMaxBandwidth"/> instead, which still gives the encoder the freedom to reduce the bandpass when the bitrate becomes too low, for better overall quality.
        /// </summary>
        SetBandwidth = 4008,
        
        /// <summary>
        /// Gets the encoder's configured bandpass or the decoder's last bandpass.
        /// </summary>
        GetBandwidth = 4009,
        
        /// <summary>
        /// Configures the encoder's computational complexity. The supported range is 0-10 inclusive with 10 representing the highest complexity.
        /// </summary>
        SetComplexity = 4010,
        
        /// <summary>
        /// Gets the encoder's complexity configuration.
        /// </summary>
        GetComplexity = 4011,
        
        /// <summary>
        /// Configures the encoder's use of inband forward error correction (FEC).
        /// </summary>
        /// <remarks>
        /// This is only applicable to the LPC layer
        /// </remarks>
        SetInbandFec = 4012,
        
        /// <summary>
        /// Gets encoder's configured use of inband forward error correction.
        /// </summary>
        GetInbandFec = 4013,
        
        /// <summary>
        /// Configures the encoder's expected packet loss percentage. Higher values trigger progressively more loss resistant behavior in the encoder at the expense of quality at a given bitrate in the absence of packet loss, but greater quality under loss.
        /// </summary>
        SetPacketLossPerc = 4014,
        
        /// <summary>
        /// Gets the encoder's configured packet loss percentage.
        /// </summary>
        GetPacketLossPerc = 4015,
        
        /// <summary>
        /// Configures the encoder's use of discontinuous transmission (DTX).
        /// </summary>
        /// <remarks>
        /// This is only applicable to the LPC layer
        /// </remarks>
        SetDtx = 4016,
        
        /// <summary>
        /// Gets encoder's configured use of discontinuous transmission.
        /// </summary>
        GetDtx = 4017,
        
        /// <summary>
        /// Enables or disables constrained VBR in the encoder. This setting is ignored when the encoder is in CBR mode.
        /// </summary>
        /// <remarks>
        /// Only the MDCT mode of Opus currently heeds the constraint. Speech mode ignores it completely, hybrid mode may fail to obey it if the LPC layer uses more bitrate than the constraint would have permitted.
        /// </remarks>
        SetVbrConstraint = 4020,
        
        /// <summary>
        /// Determine if constrained VBR is enabled in the encoder.
        /// </summary>
        GetVbrConstraint = 4021,
        
        /// <summary>
        /// Configures mono/stereo forcing in the encoder. This can force the encoder to produce packets encoded as either mono or stereo, regardless of the format of the input audio. This is useful when the caller knows that the input signal is currently a mono source embedded in a stereo stream.
        /// </summary>
        SetForceChannels = 4022,
        
        /// <summary>
        /// Gets the encoder's forced channel configuration.
        /// </summary>
        GetForceChannels = 4023,
        
        /// <summary>
        /// Configures the type of signal being encoded. This is a hint which helps the encoder's mode selection.
        /// </summary>
        SetSignal = 4024,
        
        /// <summary>
        /// Gets the encoder's configured signal type.
        /// </summary>
        GetSignal = 4025,
        
        /// <summary>
        /// Gets the total samples of delay added by the entire codec. This can be queried by the encoder and then the provided number of samples can be skipped on from the start of the decoder's output to provide time aligned input and output. From the perspective of a decoding application the real data begins this many samples late.
        /// The decoder contribution to this delay is identical for all decoders, but the encoder portion of the delay may vary from implementation to implementation, version to version, or even depend on the encoder's initial configuration. Applications needing delay compensation should call this CTL rather than hard-coding a value.
        /// </summary>
        GetLookahead = 4027,
        
        //ResetState = 4028, //Commented out in the reference Implementation.
        
        /// <summary>
        /// Gets the sampling rate the encoder or decoder was initialized with. This simply returns the value passed to <see cref="OpusEncoder.Init"/> or <see cref="OpusDecoder.Init"/>.
        /// </summary>
        GetSampleRate = 4029,
        
        /// <summary>
        /// Gets the final state of the codec's entropy coder. This is used for testing purposes, The encoder and decoder state should be identical after coding a payload (assuming no data corruption or software bugs)
        /// </summary>
        GetFinalRange = 4031,
        
        /// <summary>
        /// Gets the pitch of the last decoded frame, if available. This can be used for any post-processing algorithm requiring the use of pitch, e.g. time stretching/shortening. If the last frame was not voiced, or if the pitch was not coded in the frame, then zero is returned.
        /// </summary>
        /// <remarks>
        /// This CTL is only implemented for decoder instances.
        /// </remarks>
        GetPitch = 4033,
        
        /// <summary>
        /// Configures decoder gain adjustment. Scales the decoded output by a factor specified in Q8 dB units. This has a maximum range of -32768 to 32767 inclusive, and returns <see cref="OpusErrorCode.BadArg"/> otherwise. The default is zero indicating no adjustment. This setting survives decoder reset.
        /// </summary>
        /// <remarks>
        /// <c>gain = pow(10, x/(20.0*256))</c>
        /// </remarks>
        SetGain = 4034,
        
        /// <summary>
        /// Gets the decoder's configured gain adjustment.
        /// </summary>
        GetGain = 4045, //Typo, should have been 4035
        
        /// <summary>
        /// Configures the depth of signal being encoded.
        /// This is a hint which helps the encoder identify silence and near-silence. It represents the number of significant bits of linear intensity below which the signal contains ignorable quantization or other noise.
        /// For example, <see cref="SetLsbDepth"/> with a value of 14 would be an appropriate setting for G.711 u-law input. <see cref="SetLsbDepth"/> with a value of 16 would be appropriate for 16-bit linear pcm input with <see cref="OpusEncoder.EncodeFloat"/>.
        /// When using <see cref="OpusEncoder.Encode"/> instead of <see cref="OpusEncoder.EncodeFloat"/>, or when libopus is compiled for fixed-point, the encoder uses the minimum of the value set here and the value 16.
        /// </summary>
        SetLsbDepth = 4036,
        
        /// <summary>
        /// Gets the encoder's configured signal depth.
        /// </summary>
        GetLsbDepth = 4037,
        
        /// <summary>
        /// Gets the duration (in samples) of the last packet successfully decoded or concealed.
        /// </summary>
        GetLastPacketDuration = 4039,
        
        /// <summary>
        /// Configures the encoder's use of variable duration frames. When variable duration is enabled, the encoder is free to use a shorter frame size than the one requested in the <see cref="OpusEncoder.Encode"/> call. It is then the user's responsibility to verify how much audio was encoded by checking the ToC byte of the encoded packet. The part of the audio that was not encoded needs to be resent to the encoder for the next call. Do not use this option unless you really know what you are doing.
        /// </summary>
        SetExpertFrameDuration = 4040,
        
        /// <summary>
        /// Gets the encoder's configured use of variable duration frames.
        /// </summary>
        GetExpertFrameDuration = 4041,
        
        /// <summary>
        /// If set to <see langword="true"/>, disables almost all use of prediction, making frames almost completely independent. This reduces quality.
        /// </summary>
        SetPredictionDisabled = 4042,
        
        /// <summary>
        /// Gets the encoder's configured prediction status.
        /// </summary>
        GetPredictionDisabled = 4043,
        
        //4045 is GetGain

        /// <summary>
        /// If set to <see langword="true"/>, disables the use of phase inversion for intensity stereo, improving the quality of mono downmixes, but slightly reducing normal stereo quality. Disabling phase inversion in the decoder does not comply with RFC 6716, although it does not cause any interoperability issue and is expected to become part of the Opus standard once RFC 6716 is updated by draft-ietf-codec-opus-update.
        /// </summary>
        SetPhaseInversionDisabled = 4046,
        
        /// <summary>
        /// Gets the encoder's configured phase inversion status.
        /// </summary>
        GetPhaseInversionDisabled = 4047,
        
        /// <summary>
        /// Gets the DTX state of the encoder. Returns whether the last encoded frame was either a comfort noise update during DTX or not encoded because of DTX.
        /// </summary>
        GetInDtx = 4049
    }
}
