using System;

namespace TcpLibrary.Packets
{
    public abstract class Packet
    {
        public byte[] Data { get; }

        public byte[] PacketNumber { get; }

        protected Packet(byte[] data, byte[] packetNumber)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (packetNumber == null) throw new ArgumentNullException(nameof(packetNumber));

            Data = data;
            PacketNumber = packetNumber;
        }

        public string Format() => string.Join(" ", BitConverter.ToString(Data).Replace("-", " "));
    }
}