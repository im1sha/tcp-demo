using System;

namespace TcpLibrary.Packets
{
    public class SentPacket : Packet
    {
        public byte[] NextPacketNumber { get; }

        public SentPacket(byte[] data, byte[] packetNumber, byte[] nextPacketNumber) : base(data, packetNumber)
        {
            if (packetNumber == null) throw new ArgumentNullException(nameof(packetNumber));

            NextPacketNumber = nextPacketNumber;
        }
    }
}