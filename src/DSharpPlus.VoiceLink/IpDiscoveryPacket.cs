using System;
using System.Buffers.Binary;
using System.Text;

namespace DSharpPlus.VoiceLink
{
    public sealed record DiscordIPDiscovery(ushort Type, ushort Length, uint SSRC, string Address, ushort Port)
    {
        public static implicit operator DiscordIPDiscovery(Span<byte> ipDiscoverySpan) => new(
            BinaryPrimitives.ReadUInt16BigEndian(ipDiscoverySpan[0..2]),
            BinaryPrimitives.ReadUInt16BigEndian(ipDiscoverySpan[2..4]),
            BinaryPrimitives.ReadUInt32BigEndian(ipDiscoverySpan[4..8]),
            Encoding.UTF8.GetString(ipDiscoverySpan[8..72].ToArray()).TrimEnd('\0'),
            BinaryPrimitives.ReadUInt16BigEndian(ipDiscoverySpan[72..74])
        );

        public static implicit operator byte[](DiscordIPDiscovery ipDiscovery)
        {
            byte[] addressBytes = Encoding.UTF8.GetBytes(ipDiscovery.Address);
            Span<byte> ipDiscoverySpan = stackalloc byte[74];

            BinaryPrimitives.WriteUInt16BigEndian(ipDiscoverySpan[0..2], 1);
            BinaryPrimitives.WriteUInt16BigEndian(ipDiscoverySpan[2..4], 70);
            BinaryPrimitives.WriteUInt32BigEndian(ipDiscoverySpan[4..8], ipDiscovery.SSRC);

            // TODO: Implement this correctly
            //addressBytes.CopyTo(ipDiscoverySpan[8..(8 + addressBytes.Length)]);
            //BinaryPrimitives.WriteUInt16BigEndian(ipDiscoverySpan.Slice(addressBytes.Length + 2, 2), ipDiscovery.Port);

            return ipDiscoverySpan.ToArray();
        }

        public override string ToString() => $"Type: {Type}, Length: {Length}, SSRC: {SSRC}, Address: {Address}, Port: {Port}";
    }
}
