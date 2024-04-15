using System;

namespace TcpWpf.Models
{
    public class LogItem
    {
        public DateTime DateTimeUtc { get; }

        public string Message { get; }

        public string Event { get; }

        public LogItem(string eventName, string message)
        {
            DateTimeUtc = DateTime.UtcNow;
            Message = message;
            Event = eventName;
        }
    }
}