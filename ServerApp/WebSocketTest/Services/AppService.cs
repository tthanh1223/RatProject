using System;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using WebSocketTest.Core;
using WebSocketTest.Models;

namespace WebSocketTest.Services
{
    public class AppService 
    {
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
            if (string.IsNullOrEmpty(name)) return JsonResponse.Error("Missing app's name");
            var procs = Process.GetProcessesByName(name);
            if (procs.Length == 0) return JsonResponse.Error("Can't find app: " + name);

            foreach (var p in procs) { try { p.Kill(); } catch { } }
            return JsonResponse.Success($"Closed {procs.Length} with '{name}'");
        }

        public string StartApp(string path)
        {
            if (string.IsNullOrEmpty(path)) return JsonResponse.Error("Missing path");
            Process.Start(path);
            return JsonResponse.Success("Started: " + path);
        }
    }
}
