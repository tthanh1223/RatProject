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
        private HttpListener? _listener;
        private Action<string> _logger;
        
        // BIẾN QUAN TRỌNG: Lưu trữ client đang kết nối để gửi tin bất cứ lúc nào
        private WebSocket? _currentSocket;

        public SimpleWebSocketServer(Action<string> loggerMethod)
        {
            _logger = loggerMethod;
        }

        public async void Start(string url)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(url);
            _listener.Start();
            _logger($"Server đã khởi động tại: {url}");
            _logger("Đang chờ client kết nối...");

            try
            {
                while (_listener.IsListening)
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        // Không dùng await ở đây để không chặn vòng lặp chính
                        _ = ProcessClient(context);
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
                _logger("Lỗi Listener: " + ex.Message);
            }
        }

        private async Task ProcessClient(HttpListenerContext context)
        {
            try
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                _currentSocket = wsContext.WebSocket; // LƯU KẾT NỐI LẠI

                _logger("Client đã kết nối thành công!");
                await SendToClient("SERVER READY"); // Gửi lời chào chủ động

                byte[] buffer = new byte[1024 * 4];

                while (_currentSocket.State == WebSocketState.Open)
                {
                    var result = await _currentSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _currentSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by Server", CancellationToken.None);
                        _logger("Client đã ngắt kết nối.");
                    }
                    else
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        _logger($"[Client]: {message}");

                        // Auto-reply PING/PONG (Logic cũ)
                        if (message == "PING")
                        {
                            await SendToClient("PONG");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger("Client mất kết nối đột ngột: " + ex.Message);
            }
            finally
            {
                // Khi vòng lặp kết thúc, hủy biến lưu trữ
                _currentSocket = null;
            }
        }

        // --- TÍNH NĂNG MỚI: Gửi tin nhắn chủ động từ Server ---
        public async Task SendToClient(string msg)
        {
            if (_currentSocket != null && _currentSocket.State == WebSocketState.Open)
            {
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(msg);
                    await _currentSocket.SendAsync(
                        new ArraySegment<byte>(bytes), 
                        WebSocketMessageType.Text, 
                        true, 
                        CancellationToken.None
                    );
                    
                    // Log ra để biết Server đã gửi gì (trừ PONG cho đỡ spam log)
                    if(msg != "PONG") _logger($"[Server]: {msg}");
                }
                catch (Exception ex)
                {
                    _logger("Lỗi khi gửi tin: " + ex.Message);
                }
            }
            else
            {
                _logger("⚠️ Không thể gửi: Chưa có Client kết nối!");
            }
        }
    }
}