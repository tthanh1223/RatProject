using System;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using WebSocketTest.Core;
using WebSocketTest.Utils;
using WebSocketTest.Models;

namespace WebSocketTest.Services
{
    public class AppService : IService
    {
        public string Handle(string command, string arg)
        {
            return JsonResponse.Info("AppService not implemented yet");
        }

        public string GetApplicationList()
        {
            var processes = Process.GetProcesses();
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"type\": \"apps\", \"data\": [");

            bool isFirst = true;
            foreach (var p in processes)
            {
                string title = p.MainWindowTitle;
                string processName = p.ProcessName;

                if (!string.IsNullOrEmpty(title))
                {
                    if (!isFirst) sb.Append(",");
                    string safeTitle = title.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    sb.Append($"{{\"pid\": {p.Id}, \"ten\": \"{processName}\", \"tieu_de\": \"{safeTitle}\"}}");
                    isFirst = false;
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        public string StopAppByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return JsonResponse.Error("Thiếu tên App");
            var procs = Process.GetProcessesByName(name);
            if (procs.Length == 0) return JsonResponse.Error("Không tìm thấy App: " + name);

            foreach (var p in procs) { try { p.Kill(); } catch { } }
            return JsonResponse.Success($"Đã đóng {procs.Length} cửa sổ '{name}'");
        }

        public string StartApp(string path)
        {
            if (string.IsNullOrEmpty(path)) return JsonResponse.Error("Thiếu tên/đường dẫn");
            Process.Start(path);
            return JsonResponse.Success("Đã khởi động: " + path);
        }
    }
}
