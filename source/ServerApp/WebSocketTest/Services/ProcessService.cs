using System;
using System.Diagnostics;
using System.Text;
using WebSocketTest.Core;
using WebSocketTest.Models;

namespace WebSocketTest.Services
{
    public class ProcessService 
    {
        public string GetFullProcessList()
        {
            var processes = Process.GetProcesses();
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"type\": \"processes\", \"data\": [");

            for (int i = 0; i < processes.Length; i++)
            {
                var p = processes[i];
                long mem = 0;
                try { mem = p.WorkingSet64 / 1024 / 1024; } catch { }

                sb.Append($"{{\"pid\": {p.Id}, \"ten\": \"{p.ProcessName}\", \"mem\": {mem}}}");
                if (i < processes.Length - 1) sb.Append(",");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public string KillProcessByPid(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                p.Kill();
                return JsonResponse.Success($"Killed Process ID {pid} ({p.ProcessName})");
            }
            catch (ArgumentException) { return JsonResponse.Error($"No existed Process ID {pid}"); }
            catch (Exception ex) { return JsonResponse.Error("Can't kill: " + ex.Message); }
        }
    }
}
