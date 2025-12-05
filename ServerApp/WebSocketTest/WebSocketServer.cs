using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketTest
{
    public class SimpleWebSocketServer
    {
        private HttpListener? _listener; // Thêm ? để cho phép null
        private Action<string> _logger;

        public SimpleWebSocketServer(Action<string> loggerMethod)
        {
            _logger = loggerMethod;
        }

        public async void Start(string url)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(url);
            _listener.Start();
            _logger("Server đã khởi động tại: " + url);

            try
            {
                while (_listener.IsListening)
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        ProcessClient(context);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger("Lỗi Server: " + ex.Message);
            }
        }

        private async void ProcessClient(HttpListenerContext context)
        {
            var wsContext = await context.AcceptWebSocketAsync(null);
            WebSocket socket = wsContext.WebSocket;

            _logger("Client đã kết nối!");
            await SendMessage(socket, "SERVER READY");

            byte[] buffer = new byte[1024];

            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                        _logger("Client đã ngắt kết nối.");
                    }
                    else
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        _logger("Nhận: " + message);

                        if (message == "PING")
                        {
                            await SendMessage(socket, "PONG");
                            _logger("Gửi: PONG");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger("Client mất kết nối: " + ex.Message);
            }
        }

        private async Task SendMessage(WebSocket socket, string msg)
        {
            if (socket.State == WebSocketState.Open)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(msg);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}