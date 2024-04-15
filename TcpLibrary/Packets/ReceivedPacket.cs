namespace TcpLibrary.Packets
{
    public class ReceivedPacket : Packet
    {
        public ReceivedPacket(byte[] packet, byte[] number) : base(packet, number)
        {
        }
    }
}