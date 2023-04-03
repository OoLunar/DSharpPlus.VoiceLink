using System;

namespace DSharpPlus.VoiceLink.Opus
{
    public static class OpusPacket
    {
        /// <inheritdoc cref="OpusNativeMethods.PacketGetBandwidth(byte*)"/>
        public static unsafe OpusPacketBandwidth GetBandwidth(ReadOnlySpan<byte> data)
        {
            fixed (byte* packetPointer = data)
            {
                return OpusNativeMethods.PacketGetBandwidth(packetPointer);
            }
        }

        /// <inheritdoc cref="OpusNativeMethods.PacketGetSamplesPerFrame(byte*, int)"/>
        public static unsafe int GetSamplesPerFrame(ReadOnlySpan<byte> data, int sampleRate)
        {
            fixed (byte* packetPointer = data)
            {
                return OpusNativeMethods.PacketGetSamplesPerFrame(packetPointer, sampleRate);
            }
        }

        /// <returns>The number of channels in the packet.</returns>
        /// <exception cref="OpusException">The Opus library has thrown an exception.</exception>
        /// <inheritdoc cref="OpusNativeMethods.PacketGetNbChannels(byte*)"/>
        public static unsafe int GetChannelCount(ReadOnlySpan<byte> data)
        {
            int channelCount;
            fixed (byte* packetPointer = data)
            {
                channelCount = OpusNativeMethods.PacketGetNbChannels(packetPointer);
            }

            return channelCount < 0 ? throw new OpusException((OpusErrorCode)channelCount) : channelCount;
        }

        /// <returns>The number of frames in the packet.</returns>
        /// <exception cref="OpusException">The Opus library has thrown an exception.</exception>
        /// <inheritdoc cref="OpusNativeMethods.PacketGetNbFrames(byte*, int)"/>
        public static unsafe int GetFrameCount(ReadOnlySpan<byte> data)
        {
            int frameCount;
            fixed (byte* packetPointer = data)
            {
                frameCount = OpusNativeMethods.PacketGetNbFrames(packetPointer, data.Length);
            }

            return frameCount < 0 ? throw new OpusException((OpusErrorCode)frameCount) : frameCount;
        }

        /// <returns>The number of samples in the packet.</returns>
        /// <exception cref="OpusException">The Opus library has thrown an exception.</exception>
        /// <inheritdoc cref="OpusNativeMethods.PacketGetNbSamples(byte*, int, int)"/>
        public static unsafe int GetSampleCount(ReadOnlySpan<byte> packet, int sampleRate)
        {
            int sampleCount;
            fixed (byte* packetPointer = packet)
            {
                sampleCount = OpusNativeMethods.PacketGetNbSamples(packetPointer, packet.Length, sampleRate);
            }

            return sampleCount < 0 ? throw new OpusException((OpusErrorCode)sampleCount) : sampleCount;
        }
    }
}
