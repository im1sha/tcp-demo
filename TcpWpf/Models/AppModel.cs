using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TcpLibrary;
using TcpLibrary.Packets;
using TcpLibrary.Sockets;

namespace TcpWpf.Models
{
    public class AppModel : IDisposable
    {
        public event Action<LogItem> ClientEvent;
        public event Action<LogItem> ServerEvent;

        public bool Running { get; private set; }

        public IPEndPoint ServerAddress { get; }
        public IPEndPoint ClientAddress { get; }

        private Server server;
        private Client client;

        private CancellationTokenSource disposingTokenSource;
        private Task serverTask;
        private Task clientTask;

        public AppModel(IPAddress serverIp, int serverPort, IPAddress clientIp, int clientPort)
        {
            if (serverIp == null) throw new ArgumentNullException(nameof(serverIp));
            if (clientIp == null) throw new ArgumentNullException(nameof(clientIp));

            ServerAddress = new IPEndPoint(serverIp, serverPort);
            ClientAddress = new IPEndPoint(clientIp, clientPort);
        }

        #region client

        private void CreateClient(byte[] beginFromPacketNumber)
        {
            client = new Client(ClientAddress, ServerAddress, beginFromPacketNumber);
            client.Timeout += ClientOnTimeout;
            client.ReceivedStop += ClientOnReceivedStop;
            client.ReceivedOk += ClientOnReceivedOk;
            client.Connecting += ClientOnConnecting;
            client.Connected += ClientOnConnected;
            client.Closed += ClientOnClosed;
            client.Sent += ClientOnSent;
            client.Fatal += ClientOnFatal;
            clientTask = client.Run(disposingTokenSource.Token);
        }

        private void DisposeClient()
        {
            try
            {
                client.Dispose();
            }
            catch (Exception) { /* should catch to continue disposing */ }
            client.Timeout -= ClientOnTimeout;
            client.ReceivedStop -= ClientOnReceivedStop;
            client.ReceivedOk -= ClientOnReceivedOk;
            client.Connected -= ClientOnConnected;
            client.Connecting -= ClientOnConnecting;
            client.Closed -= ClientOnClosed;
            client.Sent -= ClientOnSent;
            client.Fatal -= ClientOnFatal;
        }

        #endregion

        public void Start()
        {
            if (Running) throw new InvalidOperationException("Model is already started.");

            Running = true;

            disposingTokenSource = new CancellationTokenSource();

            server = new Server(ServerAddress);
            server.AcceptedClient += ServerOnAcceptedClient;
            server.Accepting += ServerOnAccepting;
            server.Closed += ServerOnClosed;
            server.DisconnectedClient += ServerOnDisconnectedClient;
            server.Received += ServerOnReceived;
            server.SentOk += ServerOnSentOk;
            server.SentStop += ServerOnSentStop;
            server.Fatal += ServerOnFatal;
            serverTask = server.Run(disposingTokenSource.Token);

            CreateClient(beginFromPacketNumber: null);

            Task.Run(() => {
                try
                {
                    serverTask.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    ServerEvent?.Invoke(new LogItem("Server failure", ex.ToString()));
                }

                try
                {
                    clientTask.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    ClientEvent?.Invoke(new LogItem("Client failure", ex.ToString()));
                }
            });
        }

        #region event handlers

        private void ClientOnFatal(Exception e)
        {
            ClientEvent?.Invoke(new LogItem("Event: Fatal error", e.ToString()));
        }

        void ClientOnReceivedOk(OkResponse p)
        {
            ClientEvent?.Invoke(new StateLogItem("State Exchanging: Received OK (0x80)", p.Format(), SocketState.Exchanging));
        }

        private void ClientOnReceivedStop(StopResponse p)
        {
            ClientEvent?.Invoke(new StateLogItem("State Exchanging: Received STOP (0x81)", p.Format(), SocketState.Exchanging));
        }

        private void ClientOnSent(SentPacket p)
        {
            ClientEvent?.Invoke(new StateLogItem("State Exchanging: Sent", p.Format(), SocketState.Exchanging));
        }

        private void ClientOnConnecting()
        {
            ClientEvent?.Invoke(new StateLogItem("State Connecting", null, SocketState.Connecting));
        }

        private void ClientOnConnected(EndPoint ep)
        {
            ClientEvent?.Invoke(new StateLogItem("State Connected", ep.ToString(), SocketState.Connected));
        }

        private void ClientOnClosed(string status, ReceivedPacket lastReceived)
        {
            ClientEvent?.Invoke(new StateLogItem("State Disconnected", null, SocketState.Disconnected));

            // TODO support auto reconnect
            /*
            Task.Run(() => {
                Task.Delay(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
                DisposeClient();
                if (Running) CreateClient(lastReceived?.PacketNumber);
            });
            */
        }

        private void ClientOnTimeout(Exception ex)
        {
            ClientEvent?.Invoke(new StateLogItem("State Idle: Timeout", ex.Message, SocketState.Idle));
        }

        private void ServerOnSentStop(SentPacket p)
        {
            ServerEvent?.Invoke(new StateLogItem("State Exchanging: Sent STOP (0x81)", p.Format(), SocketState.Exchanging));
        }

        private void ServerOnSentOk(SentPacket p)
        {
            ServerEvent?.Invoke(new StateLogItem("State Exchanging: Sent OK (0x80)", p.Format(), SocketState.Exchanging));
        }

        private void ServerOnReceived(ReceivedPacket p)
        {
            ServerEvent?.Invoke(new StateLogItem("State Exchanging: Received", p.Format(), SocketState.Exchanging));
        }
        private void ServerOnClosed()
        {
            ServerEvent?.Invoke(new StateLogItem("State Disconnected", null, SocketState.Disconnected));
        }

        private void ServerOnAccepting()
        {
            ServerEvent?.Invoke(new StateLogItem("State Connecting: Accepting clients", null, SocketState.Connecting));
        }

        private void ServerOnAcceptedClient(EndPoint ep)
        {
            ServerEvent?.Invoke(new StateLogItem("State Connected: Accepted client", ep.ToString(), SocketState.Connected));
        }

        private void ServerOnDisconnectedClient(EndPoint ep)
        {
            ServerEvent?.Invoke(new LogItem("Event: Disconnected client", ep.ToString()));
        }

        private void ServerOnFatal(Exception ex)
        {
            ServerEvent?.Invoke(new LogItem("Event: Fatal error", ex.ToString()));
        }

        #endregion

       public void ProcessStopCommand()
       {
            if (!Running) throw new InvalidOperationException();

            client.StopIfConnecting();
            server.StopClient();
       }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!Running) throw new InvalidOperationException("Model is not running.");

            Running = false;

            try
            {
                disposingTokenSource.Cancel();
                disposingTokenSource.Dispose();
            }
            catch (Exception) { /* should catch to continue disposing */ }

            try
            {
                server.Dispose();
            }
            catch (Exception) { /* should catch to continue disposing */ }
            server.Accepting -= ServerOnAccepting;
            server.Closed -= ServerOnClosed;
            server.AcceptedClient -= ServerOnAcceptedClient;
            server.DisconnectedClient -= ServerOnDisconnectedClient;
            server.Received -= ServerOnReceived;
            server.SentOk -= ServerOnSentOk;
            server.SentStop -= ServerOnSentStop;
            server.Fatal -= ServerOnFatal;
            server = null;

            DisposeClient();
            client = null;

            disposingTokenSource = null;
        }
    }
}