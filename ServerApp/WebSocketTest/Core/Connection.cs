using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketTest.Core
{
    // Represents one WebSocket connection
    public class Connection
    {
        private readonly WebSocket _socket;

        public Connection(WebSocket socket)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        }

        public WebSocket Socket => _socket;

        public async Task SendAsync(string msg)
        {
            if (_socket == null || _socket.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(msg);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
