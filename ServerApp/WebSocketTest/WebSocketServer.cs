using System;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace WebSocketTest
{
    public class SimpleWebSocketServer
    {
        private HttpListener? _listener;
        private Action<string> _logger;
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

            try
            {
                while (_listener.IsListening)
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = ProcessClient(context);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
            }
            catch (Exception ex) { _logger("Lỗi Server: " + ex.Message); }
        }

        private async Task ProcessClient(HttpListenerContext context)
        {
            var wsContext = await context.AcceptWebSocketAsync(null);
            _currentSocket = wsContext.WebSocket;
            _logger("Client đã kết nối!");
            await SendToClient("{\"trang_thai\": \"info\", \"thong_bao\": \"Server Ready\"}");

            byte[] buffer = new byte[1024 * 64]; // Tăng buffer lên để chứa danh sách App dài

            try
            {
                while (_currentSocket.State == WebSocketState.Open)
                {
                    var result = await _currentSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _currentSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                        _logger("Client ngắt kết nối.");
                    }
                    else
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        
                        // Log theo yêu cầu
                        _logger($"[Client]: {message}");

                        // Xử lý lệnh
                        string response = HandleCommand(message);
                        
                        // Gửi phản hồi
                        await SendToClient(response);
                    }
                }
            }
            catch (Exception ex) { _logger("Lỗi kết nối: " + ex.Message); }
            finally { _currentSocket = null; }
        }

        public async Task SendToClient(string msg)
        {
            if (_currentSocket != null && _currentSocket.State == WebSocketState.Open)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(msg);
                await _currentSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        // --- KHU VỰC XỬ LÝ LỆNH ---
        private string HandleCommand(string command)
        {
            string[] parts = command.Split(new char[] { ' ' }, 2); // Tách lệnh và tham số
            string cmd = parts[0].Trim();
            string arg = parts.Length > 1 ? parts[1].Trim() : "";

            try
            {
                switch (cmd)
                {
                    case "listProcesses":
                        return GetProcessList();
                    case "listApps":
                        return GetApplicationList();

                    case "stopApp":
                        if (string.IsNullOrEmpty(arg)) return JsonError("Chưa nhập tên App");
                        return StopProcess(arg);

                    case "startApp":
                        if (string.IsNullOrEmpty(arg)) return JsonError("Chưa nhập đường dẫn/tên App");
                        return StartProcess(arg);

                    default:
                        // Nếu không phải lệnh, coi như chat bình thường -> Trả về nguyên văn hoặc PONG
                        if (cmd == "PING") return "PONG";
                        return command; // Echo lại tin nhắn chat
                }
            }
            catch (Exception ex)
            {
                return JsonError("Lỗi Server: " + ex.Message);
            }
        }

        // 1. Lệnh listApps
        private string GetApplicationList()
        {
            var processes = Process.GetProcesses();
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            
            bool isFirst = true;
            foreach (var p in processes)
            {
                string title = p.MainWindowTitle;
                string processName = p.ProcessName;

                // --- FIX BUG EXPLORER ---
                // Nếu là explorer, dù không có title vẫn lấy, và đặt tên hiển thị thủ công
                if (processName.ToLower() == "explorer")
                {
                    // Windows Explorer thường có MainWindowHandle != 0 nhưng Title có thể rỗng
                    if (string.IsNullOrEmpty(title))
                    {
                        title = "Windows Explorer (Shell/Folder)";
                    }
                }

                // --- ĐIỀU KIỆN LỌC ---
                // Chỉ lấy process có Tiêu đề cửa sổ (hoặc là explorer đã được fix ở trên)
                if (!string.IsNullOrEmpty(title))
                {
                    if (!isFirst) sb.Append(",");
                    
                    // Xử lý Escape ký tự đặc biệt cho JSON
                    string safeTitle = title
                        .Replace("\\", "\\\\")
                        .Replace("\"", "\\\""); 
                    
                    sb.Append($"{{\"pid\": {p.Id}, \"ten\": \"{processName}\", \"tieu_de\": \"{safeTitle}\"}}");
                    isFirst = false;
                }
            }
            sb.Append("]");
            return sb.ToString();
        }
        // Lệnh list process
        private string GetProcessList()
        {
            var processes = Process.GetProcesses();
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            
            for (int i = 0; i < processes.Length; i++)
            {
                // Chỉ lấy process có tên, lọc bớt process hệ thống nếu cần
                sb.Append($"{{\"pid\": {processes[i].Id}, \"ten\": \"{processes[i].ProcessName}\"}}");
                if (i < processes.Length - 1) sb.Append(",");
            }
            sb.Append("]");
            return sb.ToString();
        }

        // 2. Lệnh stopApp
        private string StopProcess(string name)
        {
            // Bỏ đuôi .exe nếu người dùng lỡ nhập
            if (name.EndsWith(".exe")) name = name.Substring(0, name.Length - 4);

            var processes = Process.GetProcessesByName(name);
            if (processes.Length == 0) return JsonError("Không tìm thấy ứng dụng: " + name);

            foreach (var p in processes)
            {
                try { p.Kill(); } catch { /* Bỏ qua nếu không quyền kill */ }
            }
            return JsonSuccess($"Đã dừng {processes.Length} tiến trình tên '{name}'");
        }

        // 3. Lệnh startApp
        private string StartProcess(string path)
        {
            Process.Start(path);
            return JsonSuccess("Đã khởi động: " + path);
        }

        // Helper tạo JSON phản hồi nhanh
        private string JsonSuccess(string msg)
        {
            return $"{{\"trang_thai\": \"thanh_cong\", \"thong_bao\": \"{msg}\"}}";
        }

        private string JsonError(string msg)
        {
            return $"{{\"trang_thai\": \"loi\", \"thong_bao\": \"{msg}\"}}";
        }
    }
}