using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using TcpLibrary.Packets;

namespace TcpLibrary.Sockets
{
    public abstract class CustomSocket : IDisposable
    {
        protected const int clientPacketLength = 12;
        protected const int serverPacketLength = 11;
        protected const int numberLength = 2;
        protected readonly byte[] clientRequestTemplate;
        protected readonly byte[] serverOkResponseTemplate;
        protected readonly byte[] serverStopResponseTemplate;
        protected Socket socket;

        protected CustomSocket()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            clientRequestTemplate = new byte[] {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x01, 0x03, 0x00, 0x00, 0x00, 0x1,
            };

            serverOkResponseTemplate = new byte[] {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x03, 0x02, 0x00, 0x80,
            };

            serverStopResponseTemplate = new byte[] {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x03, 0x02, 0x00, 0x81,
            };
        }

        #region packet processing

        private static byte[] IncrementPacketNumber(byte[] number)
        {
            byte[] newPacketNumber;
            if (number[0] == 0xFF && number[1] == 0xFF)
            {
                newPacketNumber = new byte[] { 0, 0, };
            }
            else if (number[1] == 0xFF)
            {
                newPacketNumber = new byte[] { (byte)(number[0] + 1), 0, };
            }
            else
            {
                newPacketNumber = new byte[] { number[0], (byte)(number[1] + 1), };
            }
            return newPacketNumber;
        }

        private static byte[] CreatePacket(byte[] template, byte[] number)
        {
            return number.Concat(template.Skip(numberLength)).ToArray();
        }

        #endregion

        #region communication

        protected static async Task<SentPacket> SendAsyncImplementation(Socket socket, byte[] template, byte[] number)
        {
            var packet = CreatePacket(template, number);
            var bytesCount = await socket.SendAsync(new ArraySegment<byte>(packet), SocketFlags.None);
            var nextPacketNumber = IncrementPacketNumber(number);
            if (bytesCount != template.Length) throw new Exception($"Unexpected sent length client request: {bytesCount}.");
            return new SentPacket(packet, number, nextPacketNumber);
        }

        protected static async Task<ReceivedPacket> ReceiveAsyncImplementation(Socket socket, int expectedLength)
        {
            var packet = new byte[expectedLength];
            var bytesCount = await socket.ReceiveAsync(new ArraySegment<byte>(packet), SocketFlags.None);
            if (bytesCount != expectedLength) throw new Exception($"Unexpected server response length: {bytesCount}.");
            return new ReceivedPacket(packet, packet.Take(numberLength).ToArray());
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public virtual void Dispose()
        {
            try { socket?.Shutdown(SocketShutdown.Both); }
            catch (Exception) { /* ok case */ }

            try { socket?.Dispose(); }
            catch (ObjectDisposedException) { /* ok case */ }
            finally { socket = null; }
        }

        #endregion
    }
}