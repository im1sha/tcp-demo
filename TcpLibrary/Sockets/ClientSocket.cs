using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using TcpLibrary.Packets;

namespace TcpLibrary.Sockets
{
    public class ClientSocket : CustomSocket
    {
        private readonly IPEndPoint clientAddress;
        private readonly IPEndPoint serverAddress;
        private readonly TimeSpan pollTimeout;
        private byte[] currentPacketNumber;
        private byte[] nextPacketNumber;
        private bool connected;

        public ClientSocket(IPEndPoint clientAddress, IPEndPoint serverAddress, TimeSpan pollTimeout, byte[] beginFromPacketNumber = null)
        {
            if (clientAddress == null) throw new ArgumentNullException(nameof(clientAddress));
            if (serverAddress == null) throw new ArgumentNullException(nameof(serverAddress));

            this.clientAddress = clientAddress;
            this.serverAddress = serverAddress;
            this.pollTimeout = pollTimeout;
            currentPacketNumber = beginFromPacketNumber ?? new byte[] { 0, 0, };
        }

        public async Task<EndPoint> ConnectAsync()
        {
            if (connected) throw new InvalidOperationException();

            socket.Bind(clientAddress);
            await socket.ConnectAsync(serverAddress.Address, serverAddress.Port);
            connected = true;
            return socket.RemoteEndPoint;
        }

        public bool Poll()
        {
            if (!connected) throw new InvalidOperationException();

            // https://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c
            bool part1 = socket.Poll(microSeconds: (int)pollTimeout.TotalMilliseconds * 1_000,
                                     mode: SelectMode.SelectRead);
            bool part2 = socket.Available == 0;
            if ((part1 && part2) || !socket.Connected) return false;
            else return true;
        }

        public async Task<SentPacket> SendAsync()
        {
            if (!connected) throw new InvalidOperationException();

            var result = await SendAsyncImplementation(socket, clientRequestTemplate, currentPacketNumber);
            currentPacketNumber = result.PacketNumber;
            nextPacketNumber = result.NextPacketNumber;
            return result;
        }

        public async Task<ReceivedPacket> ReceiveAsync()
        {
            if (!connected) throw new InvalidOperationException();

            var result = await ReceiveAsyncImplementation(socket, serverPacketLength);

            if (!result.PacketNumber.SequenceEqual(currentPacketNumber))
            {
                throw new Exception("Received number is unexpected.");
            }

            currentPacketNumber = nextPacketNumber;
            nextPacketNumber = null;

            if (result.Data.Skip(numberLength).SequenceEqual(serverStopResponseTemplate.Skip(numberLength)))
            {
                return StopResponse.Create(result);
            }
            else if (!result.Data.Skip(numberLength).SequenceEqual(serverOkResponseTemplate.Skip(numberLength)))
            {
                throw new Exception("Unknown message received.");
            }
            else
            {
                return OkResponse.Create(result);
            }
        }
    }
}