using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TcpLibrary.Packets;
using TcpLibrary.Sockets;

namespace TcpLibrary
{
    public class Client : IDisposable
    {
        public event Action Connecting;
        public event Action<string, ReceivedPacket> Closed;
        public event Action<EndPoint> Connected;
        public event Action<OkResponse> ReceivedOk;
        public event Action<StopResponse> ReceivedStop;
        public event Action<SentPacket> Sent;
        public event Action<Exception> Timeout;
        public event Action<Exception> Fatal;

        private ClientSocket clientSocket;
        private readonly TimeSpan pollingPeriod;
        private readonly TimeSpan pollingTimeout;
        private readonly TimeSpan requestPeriod;
        private readonly TimeSpan responseTimeout;

        private bool stopTermination;
        private bool connectingTermination;

        private bool connected;
        private readonly object connectedLock;

        private bool closed;
        private readonly object closedLock;

        private ReceivedPacket lastReceived;

        public Client(IPEndPoint clientAddress, IPEndPoint serverAddress, byte[] beginFromPacketNumber)
        {
            if (clientAddress == null) throw new ArgumentNullException(nameof(clientAddress));
            if (serverAddress == null) throw new ArgumentNullException(nameof(serverAddress));

            connectedLock = new object();
            closedLock = new object();
            requestPeriod = TimeSpan.FromMilliseconds(300);
            responseTimeout = TimeSpan.FromMilliseconds(300);
            pollingPeriod = TimeSpan.FromMilliseconds(250);
            pollingTimeout = TimeSpan.FromMilliseconds(250);

            clientSocket = new ClientSocket(clientAddress, serverAddress, pollingTimeout, beginFromPacketNumber);
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

        private async Task RunImplementation(CancellationToken token)
        {
            Connecting?.Invoke();
            EndPoint ep = await clientSocket.ConnectAsync();

            lock (connectedLock) connected = true;

            if (connectingTermination) throw new Exception("Connection terminated by command before establishing.");

            Connected?.Invoke(ep);

            var polling = new Thread(poll);
            polling.Start();

            var spentTimeTracker = Stopwatch.StartNew();

            while (!token.IsCancellationRequested)
            {
                spentTimeTracker.Restart();

                ReceivedPacket received;

                Sent?.Invoke(await clientSocket.SendAsync());

                using (new Timer(_ => {
                                     var timeout = false;
                                     lock (closedLock)
                                     {
                                         if (!closed) timeout = true;
                                     }
                                     if (timeout) Timeout?.Invoke(new Exception("Response receiving timeout"));
                                 },
                                 null,
                                 responseTimeout,
                                 TimeSpan.FromMilliseconds(System.Threading.Timeout.Infinite)))
                {
                    lastReceived = received = await clientSocket.ReceiveAsync();
                }

                if (received is OkResponse ok) ReceivedOk?.Invoke(ok);
                else if (received is StopResponse stop)
                {
                    ReceivedStop?.Invoke(stop);
                    stopTermination = true;
                    break;
                }
                else throw new ArgumentOutOfRangeException(nameof(received));

                spentTimeTracker.Stop();
                var timeToWait = requestPeriod - spentTimeTracker.Elapsed > TimeSpan.Zero
                    ? requestPeriod - spentTimeTracker.Elapsed
                    : TimeSpan.Zero;

                await Task.Delay(timeToWait, token);
            }

            return;

            async void poll()
            {
                try
                {
                    while (!token.IsCancellationRequested && clientSocket != null)
                    {
                        try
                        {
                            bool isOk = clientSocket.Poll();
                            var check = false;
                            lock (closedLock)
                            {
                                if (!closed) check = true;
                            }
                            if (check && !isOk) Timeout?.Invoke(new Exception("Poll failed."));
                        }
                        catch (Exception e) // poll throws
                        {
                            var timeout = false;
                            lock (closedLock)
                            {
                                if (!closed) timeout = true;
                            }
                            if (timeout) Timeout?.Invoke(e);
                        }

                        await Task.Delay(pollingPeriod, token);
                    }
                }
                catch (Exception e) // Task.Delay throws
                {
                    /* expected case */
                }
            }
        }

        public void StopIfConnecting()
        {
            lock (connectedLock)
            {
                if (!connected)
                {
                    connectingTermination = true;
                }
            }

            if (connectingTermination) Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (closedLock) closed = true;
            try
            {
                if (clientSocket != null)
                {
                    clientSocket.Dispose();
                    var message = connectingTermination
                        ? "Not connected yet"
                        : stopTermination
                            ? "Stopped (0x81)"
                            : "Standard termination (0x80)";

                    Closed?.Invoke(message, lastReceived);
                }
            }
            finally { clientSocket = null; }
        }
    }
}