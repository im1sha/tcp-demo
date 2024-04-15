namespace TcpLibrary.Packets
{
    public class OkResponse : ReceivedPacket
    {
        private OkResponse(byte[] packet, byte[] number) : base(packet, number)
        {
        }

        public static OkResponse Create(ReceivedPacket packet) => new OkResponse(packet.Data, packet.PacketNumber);
    }
}