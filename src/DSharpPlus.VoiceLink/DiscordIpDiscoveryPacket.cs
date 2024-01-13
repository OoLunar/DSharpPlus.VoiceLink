using System;
using System.Buffers.Binary;
using System.Text;

namespace DSharpPlus.VoiceLink
{
    public readonly struct DiscordIpDiscoveryPacket
    {
        public ushort Type { get; init; }
        public ushort Length { get; init; }
        public uint Ssrc { get; init; }
        public string Address { get; init; }
        public ushort Port { get; init; }

        public DiscordIpDiscoveryPacket(byte[] data)
        {
            Span<byte> dataSpan = data.AsSpan();
            Type = BinaryPrimitives.ReadUInt16BigEndian(dataSpan[0..2]);
            Length = BinaryPrimitives.ReadUInt16BigEndian(dataSpan[2..4]);
            Ssrc = BinaryPrimitives.ReadUInt32BigEndian(dataSpan[4..8]);
            Address = Encoding.UTF8.GetString(dataSpan[8..72].TrimEnd((byte)'\0'));
            Port = BinaryPrimitives.ReadUInt16BigEndian(dataSpan[72..74]);
        }

        public DiscordIpDiscoveryPacket(ushort type, ushort length, uint ssrc, string address, ushort port)
        {
            Type = type;
            Length = length;
            Ssrc = ssrc;
            Address = address;
            Port = port;
        }

        public static implicit operator DiscordIpDiscoveryPacket(byte[] ipDiscoveryData) => new(ipDiscoveryData);
        public static implicit operator byte[](DiscordIpDiscoveryPacket ipDiscovery)
        {
            byte[] data = new byte[74];

            Span<byte> dataSpan = data.AsSpan();
            BinaryPrimitives.WriteUInt16BigEndian(dataSpan[0..2], ipDiscovery.Type);
            BinaryPrimitives.WriteUInt16BigEndian(dataSpan[2..4], ipDiscovery.Length);
            BinaryPrimitives.WriteUInt32BigEndian(dataSpan[4..8], ipDiscovery.Ssrc);
            Encoding.UTF8.TryGetBytes(ipDiscovery.Address, dataSpan[8..72], out _);
            dataSpan[71] = 0; // Need to null-terminate the IP string
            BinaryPrimitives.WriteUInt16BigEndian(dataSpan[72..74], ipDiscovery.Port);

            return data;
        }

        public override string ToString() => $"Type: {Type}, Length: {Length}, Ssrc: {Ssrc}, Address: {Address}, Port: {Port}";
    }
}
