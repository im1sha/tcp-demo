using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using TcpLibrary.Packets;

namespace TcpLibrary.Sockets
{
    public class ServerSocket : CustomSocket
    {
        private readonly IPEndPoint serverAddress;
        private Socket clientSocket;
        private byte[] currentPacketNumber;

        public ServerSocket(IPEndPoint serverAddress)
        {
            this.serverAddress = serverAddress;
        }

        public void Start()
        {
            if (clientSocket != null) throw new InvalidOperationException();

            socket.Bind(serverAddress);
            socket.Listen(backlog: 1);
        }

        public async Task<EndPoint> AcceptAsync()
        {
            if (clientSocket != null) throw new InvalidOperationException();

            clientSocket = await socket.AcceptAsync();
            return clientSocket.RemoteEndPoint;
        }

        /// <exception cref="InvalidOperationException">No clients have been accepted yet.</exception>
        /// <exception cref="SocketException">The client is disconnected, but its IP address could not be read.</exception>
        /// <exception cref="ObjectDisposedException">The client is disconnected, but its IP address could not be read.</exception>
        public EndPoint DisconnectClient()
        {
            if (clientSocket == null) throw new InvalidOperationException();

            EndPoint address = null;
            Exception ex = null;

            try { address = clientSocket.RemoteEndPoint; }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException) { ex = e; }

            try { clientSocket.Shutdown(SocketShutdown.Both); }
            catch (Exception) { /* ignore */ }
            try { clientSocket.Dispose(); }
            catch (Exception) { /* ignore */ }
            finally { clientSocket = null; }

            return ex != null ? throw ex : address;
        }

        public async Task<SentPacket> SendOkAsync()
        {
            if (clientSocket == null) throw new InvalidOperationException();

            return await SendAsyncImplementation(clientSocket, serverOkResponseTemplate, currentPacketNumber);
        }

        public async Task<SentPacket> SendStopAsync()
        {
            if (clientSocket == null) throw new InvalidOperationException();

            return await SendAsyncImplementation(clientSocket, serverStopResponseTemplate, currentPacketNumber);
        }

        public async Task<ReceivedPacket> ReceiveAsync()
        {
            if (clientSocket == null) throw new InvalidOperationException();

            var result = await ReceiveAsyncImplementation(clientSocket, clientPacketLength);

            if (!result.Data.Skip(numberLength).SequenceEqual(clientRequestTemplate.Skip(numberLength)))
            {
                throw new Exception("Unknown request received.");
            }

            currentPacketNumber = result.PacketNumber;

            return result;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            try { clientSocket?.Shutdown(SocketShutdown.Both); }
            catch (Exception) { /* ok case */ }

            try { clientSocket?.Dispose(); }
            catch (ObjectDisposedException) { /* ok case */ }
            finally { clientSocket = null; }
            base.Dispose();
        }
    }
}