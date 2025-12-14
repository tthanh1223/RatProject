using System;
using System.Net;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace WebSocketTest.Core
{
    // Server lifecycle manager - handles HttpListener loop and health checks
    public class Server
    {
        private HttpListener? _listener;
        private readonly Action<string> _logger = _ => { };
        private bool _isRunning = false;

        public Server(Action<string>? logger = null)
        {
            if (logger != null) _logger = logger;
        }

        public async Task Start(string url, Func<HttpListenerContext, Task> contextHandler)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(url);
            _listener.Start();
            _isRunning = true;
            _logger($"Core.Server started at: {url}");

            try
            {
                while (_listener.IsListening && _isRunning)
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.Url?.AbsolutePath == "/health")
                    {
                        await HandleHealthCheck(context);
                    }
                    else if (context.Request.IsWebSocketRequest)
                    {
                        // pass context to the caller-provided handler
                        _ = contextHandler(context);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
            }
            catch (Exception ex) { _logger("Server error: " + ex.Message); }
        }

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
            _isRunning = false;
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
                _logger("Core.Server stopped");
            }
        }
    }
}
