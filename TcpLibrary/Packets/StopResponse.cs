namespace TcpLibrary.Packets
{
    public class StopResponse : ReceivedPacket
    {
        private StopResponse(byte[] packet, byte[] number) : base(packet, number)
        {
        }

        public static StopResponse Create(ReceivedPacket packet) => new StopResponse(packet.Data, packet.PacketNumber);
    }
}