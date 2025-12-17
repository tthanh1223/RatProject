using System;

namespace WebSocketTest.Models
{
    public static class JsonResponse
    {
        public static string Success(string msg) => "{\"trang_thai\": \"thanh_cong\", \"thong_bao\": \"" + Escape(msg) + "\"}";
        public static string Error(string msg) => "{\"trang_thai\": \"loi\", \"thong_bao\": \"" + Escape(msg) + "\"}";
        public static string Info(string msg) => "{\"trang_thai\": \"info\", \"thong_bao\": \"" + Escape(msg) + "\"}";

        private static string Escape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }
    }
}
