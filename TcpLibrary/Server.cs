using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TcpLibrary.Packets;
using TcpLibrary.Sockets;

namespace TcpLibrary
{
    public class Server : IDisposable
    {
        public event Action Accepting;
        public event Action Closed;
        public event Action<EndPoint> AcceptedClient;
        public event Action<EndPoint> DisconnectedClient;
        public event Action<ReceivedPacket> Received;
        public event Action<SentPacket> SentOk;
        public event Action<SentPacket> SentStop;
        public event Action<Exception> Fatal;

        private ServerSocket serverSocket;
        private CancellationTokenSource stopTokenSource;

        public Server(IPEndPoint endpoint)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            serverSocket = new ServerSocket(endpoint);
            stopTokenSource = new CancellationTokenSource();
        }

        public async Task Run(CancellationToken disposingToken)
        {
            try
            {
                await RunImplementation(disposingToken);
            }
            catch (Exception e)
            {
                Fatal?.Invoke(e);
                throw;
            }
            finally
            {
                Dispose();
            }
        }

        private async Task RunImplementation(CancellationToken disposingToken)
        {
            serverSocket.Start();

            while (!disposingToken.IsCancellationRequested)
            {
                Accepting?.Invoke();
                var ep = await serverSocket.AcceptAsync();
                AcceptedClient?.Invoke(ep);
                var stopToken = stopTokenSource.Token;

                while (!disposingToken.IsCancellationRequested && !stopToken.IsCancellationRequested)
                {
                    Received?.Invoke(await serverSocket.ReceiveAsync());

                    if (!disposingToken.IsCancellationRequested && !stopToken.IsCancellationRequested)
                    {
                        SentOk?.Invoke(await serverSocket.SendOkAsync());
                    }
                    else
                    {
                        SentStop?.Invoke(await serverSocket.SendStopAsync());
                    }
                }

                // client disconnection
                {
                    Exception ex = null;
                    EndPoint clientEp = null;

                    try { clientEp = serverSocket.DisconnectClient(); }
                    catch (Exception e) when (e is SocketException || e is ObjectDisposedException) { ex = e; }

                    // default scenario: using the EP from the server socket
                    // fallback: using the EP value from the moment of connection, since its value should not change
                    DisconnectedClient?.Invoke(ex == null ? clientEp : ep);

                    try { stopTokenSource.Dispose(); }
                    catch (Exception) { /* ignore, we want to use a new token source */ }
                    finally { stopTokenSource = new CancellationTokenSource(); }
                }
            }
        }

        public void StopClient()
        {
            if (stopTokenSource == null) throw new ObjectDisposedException(nameof(Server));

            stopTokenSource.Cancel();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            try { stopTokenSource?.Dispose(); }
            catch (Exception) { /* continue disposing */ }
            finally { stopTokenSource = null; }

            try
            {
                if (serverSocket != null)
                {
                    serverSocket.Dispose();
                    Closed?.Invoke();
                }
            }
            catch (Exception) { /* ignore */ }
            finally { serverSocket = null; }
        }
    }
}