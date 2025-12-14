using System;

namespace WebSocketTest.Utils
{
    public static class Helper
    {
        public static string EscapeJson(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }
    }
}
