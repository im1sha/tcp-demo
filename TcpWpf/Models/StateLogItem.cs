using TcpLibrary.Sockets;

namespace TcpWpf.Models
{
    public class StateLogItem : LogItem
    {
        public SocketState State { get; }

        /// <inheritdoc />
        public StateLogItem(string eventName, string message, SocketState state) : base(eventName, message)
        {
            State = state;
        }
    }
}