using System;
using System.Diagnostics;

namespace WebSocketTest
{
    public static class ShutdownRestart
    {
        public static void Shutdown()
        {
            try
            {
                // Shutdown máy tính sau 5 giây
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C shutdown /s /t 5",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
            }
        }

        public static void Restart()
        {
            try
            {
                // Restart máy tính sau 5 giây
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C shutdown /r /t 5",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
            }
        }
    }
}
