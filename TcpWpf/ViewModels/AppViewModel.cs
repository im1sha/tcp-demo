using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows;
using TcpLibrary.Sockets;
using TcpWpf.Models;

namespace TcpWpf.ViewModels
{
    public class AppViewModel : INotifyPropertyChanged, IDisposable
    {
        #region logs

        #region event handling

        private void ModelOnServerEvent(LogItem log)
        {
            Application.Current.Dispatcher.Invoke(() => {
                ServerLog.Insert(0, log);
                if (log is StateLogItem s)
                {
                    serverState = s.State.ToString();
                    OnPropertyChanged(nameof(ServerState));
                }
            });
        }

        private void ModelOnClientEvent(LogItem log)
        {
            Application.Current.Dispatcher.Invoke(() => {
                ClientLog.Insert(0, log);
                if (log is StateLogItem s)
                {
                    clientState = s.State.ToString();
                    OnPropertyChanged(nameof(ClientState));
                }
            });
        }

        #endregion

        public ObservableCollection<LogItem> ServerLog { get; } = new ObservableCollection<LogItem>();

        public ObservableCollection<LogItem> ClientLog { get; } = new ObservableCollection<LogItem>();

        #endregion

        #region model start, stop

        private AppModel model;

        private InteractCommand startCommand;
        public InteractCommand StartCommand => startCommand ??
            (startCommand = new InteractCommand(o => {
                stopped = false;

                model?.Dispose();

                model = new AppModel(serverAddress, serverPort, clientAddress, clientPort);

                model.ClientEvent += ModelOnClientEvent;
                model.ServerEvent += ModelOnServerEvent;

                model.Start();

                OnPropertyChanged(nameof(DisposeEnabled));
                OnPropertyChanged(nameof(RunEnabled));
                OnPropertyChanged(nameof(StopEnabled));
            }));

        private InteractCommand stopCommand;
        public InteractCommand StopCommand => stopCommand ??
            (stopCommand = new InteractCommand(o => {
                stopped = true;

                model.ProcessStopCommand();

                OnPropertyChanged(nameof(DisposeEnabled));
                OnPropertyChanged(nameof(RunEnabled));
                OnPropertyChanged(nameof(StopEnabled));
            }));

        private InteractCommand disposeCommand;
        public InteractCommand DisposeCommand => disposeCommand ??
            (disposeCommand = new InteractCommand(o => {
                stopped = true;

                model.Dispose();
                model.ClientEvent -= ModelOnClientEvent;
                model.ServerEvent -= ModelOnServerEvent;

                OnPropertyChanged(nameof(DisposeEnabled));
                OnPropertyChanged(nameof(RunEnabled));
                OnPropertyChanged(nameof(StopEnabled));

                model = null;
            }));

        private bool stopped = false;
        public bool DisposeEnabled => !RunEnabled;
        public bool RunEnabled => (!model?.Running ?? true);

        // TODO support auto reconnect
        public bool StopEnabled => !RunEnabled && !stopped;

        #endregion

        #region connection status

        private string serverState;
        private string clientState;
        public string ServerState => "Status: " + (serverState ?? nameof(SocketState.Disconnected));
        public string ClientState => "Status: " + (clientState ?? nameof(SocketState.Disconnected));

        #endregion

        #region addresses

        private IPAddress serverAddress = new IPAddress(new byte[] { 127, 0, 0, 1, });
        private IPAddress clientAddress = new IPAddress(new byte[] { 127, 0, 0, 1, });
        private int clientPort = 3000;
        private int serverPort = 502;

        public string ServerAddress
        {
            get => (!model?.Running ?? true) ? serverAddress.ToString() : model?.ServerAddress.Address.ToString();
            set
            {
                if (IPAddress.TryParse(value, out var address))
                {
                    serverAddress = address;
                    OnPropertyChanged();
                }
                else
                {
                }
            }
        }

        public string ClientAddress
        {
            get => (!model?.Running ?? true) ? clientAddress.ToString() : model?.ClientAddress.Address.ToString();
            set
            {
                if (IPAddress.TryParse(value, out var address))
                {
                    clientAddress = address;
                    OnPropertyChanged();
                }
                else
                {
                }
            }
        }

        public string ClientPort
        {
            get => (!model?.Running ?? true) ? clientPort.ToString() : model?.ClientAddress.Port.ToString();
            set
            {
                if (int.TryParse(value, out var port))
                {
                    clientPort = port;
                    OnPropertyChanged();
                }
                else
                {
                }
            }
        }

        public string ServerPort
        {
            get => (!model?.Running ?? true) ? serverPort.ToString() : model?.ServerAddress.Port.ToString();
            set
            {
                if (int.TryParse(value, out var port))
                {
                    serverPort = port;
                    OnPropertyChanged();
                }
                else
                {
                }
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            if (model != null)
            {
                model.ClientEvent -= ModelOnClientEvent;
                model.ServerEvent -= ModelOnServerEvent;
                model.Dispose();
            }
        }

        #endregion
    }
}