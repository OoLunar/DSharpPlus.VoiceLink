using System;
using System.Buffers.Binary;
using System.Text;

namespace DSharpPlus.VoiceLink
{
    public readonly struct DiscordIPDiscovery
    {
        private readonly byte[] _data;
        public ushort Type => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(0, 2));
        public ushort Length => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(2, 4));
        public uint SSRC => BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(4, 8));
        public string Address => Encoding.UTF8.GetString(_data.AsSpan(8, 64).TrimEnd((byte)0));
        public ushort Port => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(72, 2));

        private DiscordIPDiscovery(byte[] data) => _data = data;
        public DiscordIPDiscovery(ushort Type, ushort Length, uint SSRC, string Address, ushort Port)
        {
            _data = new byte[74];

            Span<byte> dataSpan = _data.AsSpan();
            BinaryPrimitives.WriteUInt16BigEndian(dataSpan[0..2], Type);
            BinaryPrimitives.WriteUInt16BigEndian(dataSpan[2..4], Length);
            BinaryPrimitives.WriteUInt32BigEndian(dataSpan[4..8], SSRC);
            Encoding.UTF8.GetBytes(Address).CopyTo(dataSpan[8..72]);
            BinaryPrimitives.WriteUInt16BigEndian(dataSpan[^2..], Port);
        }

        public static implicit operator DiscordIPDiscovery(byte[] ipDiscoverySpan) => new(ipDiscoverySpan);
        public static implicit operator byte[](DiscordIPDiscovery ipDiscovery) => ipDiscovery._data;
        public override string ToString() => $"Type: {Type}, Length: {Length}, SSRC: {SSRC}, Address: {Address}, Port: {Port}";
    }
}
