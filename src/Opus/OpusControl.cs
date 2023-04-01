namespace DSharpPlus.VoiceLink.Opus
{
    public enum OpusControlRequest
    {
        SetApplication = 4000,
        GetApplication = 4001,
        SetBitrate = 4002,
        GetBitrate = 4003,
        SetMaxBandwidth = 4004,
        GetMaxBandwidth = 4005,
        SetVbr = 4006,
        GetVbr = 4007,
        SetBandwidth = 4008,
        GetBandwidth = 4009,
        SetComplexity = 4010,
        GetComplexity = 4011,
        SetInbandFec = 4012,
        GetInbandFec = 4013,
        SetPacketLossPerc = 4014,
        GetPacketLossPerc = 4015,
        SetDtx = 4016,
        GetDtx = 4017,
        SetVbrConstraint = 4020,
        GetVbrConstraint = 4021,
        SetForceChannels = 4022,
        GetForceChannels = 4023,
        SetSignal = 4024,
        GetSignal = 4025,
        GetLookahead = 4027,
        //ResetState = 4028, //Commented out in the reference Implementtation
        GetSampleRate = 4029,
        GetFinalRange = 4031,
        GetPitch = 4033,
        SetGain = 4034,
        GetGain = 4045, //Typo, should have been 3035
        SetLsbDepth = 4036,
        GetLsbDepth = 4037,
        GetLastPacketDuration = 4039,
        SetExpertFrameDuration = 4040,
        GetExpertFrameDuration = 4041,
        SetPredictionDisabled = 4042,
        GetPredictionDisabled = 4043,
        //4045 is GetGain
        SetPhaseInversionDisabled = 4046,
        GetPhaseInversionDisabled = 4047,
        GetInDtx = 4049
    }
}
