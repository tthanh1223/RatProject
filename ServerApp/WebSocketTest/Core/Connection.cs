using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketTest.Core
{
    public class Connection : IDisposable
    {
        private readonly WebSocket _socket;
        // Chuyển Semaphore vào đây để mỗi kết nối tự quản lý việc gửi của mình
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        public Connection(WebSocket socket)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        }

        public WebSocket Socket => _socket;
        
        // Kiểm tra xem kết nối còn sống không
        public bool IsConnected => _socket != null && _socket.State == WebSocketState.Open;

        // Hàm gửi chuỗi (JSON/Text)
        public async Task SendAsync(string message)
        {
            if (!IsConnected) return;

            var bytes = Encoding.UTF8.GetBytes(message);
            await SendBytesAsync(bytes, WebSocketMessageType.Text);
        }

        // Hàm gửi bytes (dùng chung cho cả Text và Binary/Image)
        public async Task SendBytesAsync(byte[] bytes, WebSocketMessageType type = WebSocketMessageType.Text)
        {
            if (!IsConnected) return;

            await _sendLock.WaitAsync(); // Khóa để tránh 2 luồng gửi cùng lúc gây lỗi socket
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(bytes), type, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending: {ex.Message}");
            }
            finally
            {
                _sendLock.Release();
            }
        }

        // Hàm đóng kết nối an toàn
        public async Task CloseAsync()
        {
            if (_socket.State == WebSocketState.Open)
            {
                try 
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closing", CancellationToken.None);
                }
                catch { }
            }
        }

        public void Dispose()
        {
            _socket?.Dispose();
            _sendLock?.Dispose();
        }
    }
}