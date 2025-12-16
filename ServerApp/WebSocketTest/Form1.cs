using System;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security.Principal;

namespace WebSocketTest
{
    public partial class Form1 : Form
    {
        // ✅ KHAI BÁO PORT Ở ĐÂY (Chỉ cần sửa số này là ăn toàn bộ code)
        private const int PORT = 8080;
        
        private SimpleWebSocketServer? _server;
        private bool _serverRunning = false;

        public Form1()
        {
            InitializeComponent();
            
            // ✅ KIỂM TRA ADMIN NGAY KHI KHỞI ĐỘNG
            if (!IsRunAsAdministrator())
            {
                MessageBox.Show(
                    "⚠️ CẢNH BÁO: Ứng dụng KHÔNG chạy với quyền Administrator!\n\n" +
                    "Điều này có thể gây lỗi khi bind vào tất cả network interfaces.\n\n" +
                    "Khuyến nghị:\n" +
                    "- Đóng app này\n" +
                    "- Click phải vào Visual Studio → Run as Administrator\n" +
                    "- Mở lại project và chạy",
                    "Admin Check",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }
        // Thêm hàm này vào trong class Form1
        private void btnKill_Click(object sender, EventArgs e) => System.Diagnostics.Process.GetCurrentProcess().Kill();            
        
        private void btnStart_Click(object sender, EventArgs e)
        {
            try 
            {
                // ✅ KIỂM TRA URL RESERVATION
                if (!CheckUrlReservation())
                {
                    var result = MessageBox.Show(
                        $"❌ PHÁT HIỆN: Windows chưa cho phép bind vào http://+:{PORT}/\n\n" +
                        "Bạn cần chạy lệnh sau trong CMD (Administrator):\n\n" +
                        $"netsh http add urlacl url=http://+:{PORT}/ user=Everyone\n\n" +
                        "Bấm YES để tự động chạy lệnh này (cần quyền Admin)\n" +
                        "Bấm NO để copy lệnh và tự chạy thủ công",
                        "URL Reservation Required",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning
                    );

                    if (result == DialogResult.Yes)
                    {
                        if (AddUrlReservation())
                        {
                            MessageBox.Show(
                                "✅ Đã thêm URL reservation thành công!\n\nBạn có thể Start Server ngay bây giờ.",
                                "Success",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information
                            );
                        }
                        else
                        {
                            MessageBox.Show(
                                "❌ Không thể thêm URL reservation tự động.\n\n" +
                                "Vui lòng chạy lệnh sau trong CMD (Administrator):\n\n" +
                                $"netsh http add urlacl url=http://+:{PORT}/ user=Everyone",
                                "Failed",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error
                            );
                            return;
                        }
                    }
                    else if (result == DialogResult.No)
                    {
                        Clipboard.SetText($"netsh http add urlacl url=http://+:{PORT}/ user=Everyone");
                        MessageBox.Show(
                            "✅ Đã copy lệnh vào clipboard!\n\n" +
                            "Mở CMD với quyền Administrator và paste lệnh vào.",
                            "Copied",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                        return;
                    }
                    else
                    {
                        return; // User cancelled
                    }
                }

                _server = new SimpleWebSocketServer(LogToUI);
                
                // ✅ SỬA: Dùng biến PORT
                _server.Start($"http://+:{PORT}/");
                
                _serverRunning = true;
                btnStart.Enabled = false;
                btnStart.Text = "Running...";
                btnStop.Enabled = true;
                
                // ✅ Hiển thị IP với đúng PORT
                string localIP = GetLocalIPAddress();
                LogToUI("═══════════════════════════════════════════════");
                LogToUI($"✅ Server đã khởi động thành công!");
                LogToUI($"🔗 Từ máy KHÁC, kết nối: ws://{localIP}:{PORT}/");
                LogToUI($"🔗 Từ máy này, kết nối: ws://localhost:{PORT}/");
                LogToUI($"🌐 Server đang lắng nghe trên TẤT CẢ network interfaces");
                LogToUI("═══════════════════════════════════════════════");
                
                // ✅ KIỂM TRA FIREWALL
                CheckFirewallStatus();
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5)
            {
                MessageBox.Show(
                    "❌ LỖI: Access Denied!\n\n" +
                    "Có 2 nguyên nhân:\n\n" +
                    "1. Bạn CHƯA chạy Visual Studio với quyền ADMINISTRATOR\n" +
                    "   → Đóng Visual Studio\n" +
                    "   → Click phải → Run as Administrator\n" +
                    "   → Mở lại project\n\n" +
                    $"2. Windows CHƯA cho phép bind vào http://+:{PORT}/\n" +
                    "   → Chạy lệnh sau trong CMD (Administrator):\n" +
                    $"   netsh http add urlacl url=http://+:{PORT}/ user=Everyone",
                    "Lỗi quyền truy cập",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Lỗi khởi động server:\n\n{ex.Message}\n\n" +
                    "Kiểm tra:\n" +
                    "- Chạy Visual Studio với quyền Administrator\n" +
                    $"- Port {PORT} có bị chiếm không?\n" +
                    "- Đã chạy lệnh netsh http add urlacl chưa?\n" +
                    "- Firewall có chặn không?",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            try
            {
                if (_server != null)
                {
                    _server.Stop();
                    _serverRunning = false;
                    btnStart.Enabled = true;
                    btnStart.Text = "Start Server";
                    btnStop.Enabled = false;
                    LogToUI("Server đã dừng!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi dừng server: " + ex.Message);
            }
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            if (_server == null) return;

            string msg = txtMessage.Text.Trim();
            if (!string.IsNullOrEmpty(msg))
            {
                await _server.SendToClient(msg);
                txtMessage.Clear();
                txtMessage.Focus();
            }
        }

        private void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSend_Click(this, new EventArgs());
                e.SuppressKeyPress = true;
            }
        }

        private void LogToUI(string msg)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(LogToUI), new object[] { msg });
                return;
            }
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            txtLog.ScrollToCaret();
        }

        // ✅ HÀM LẤY IP CỦA MÁY
        private string GetLocalIPAddress()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
                    if (endPoint != null)
                    {
                        return endPoint.Address.ToString();
                    }
                }
            }
            catch { }
            
            return "127.0.0.1";
        }

        // ✅ KIỂM TRA QUYỀN ADMIN
        private bool IsRunAsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        // ✅ KIỂM TRA URL RESERVATION
        private bool CheckUrlReservation()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "http show urlacl",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi)!)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    // Kiểm tra xem có URL reservation cho port hiện tại không
                    return output.Contains($"http://+:{PORT}/") || 
                           output.Contains($"http://*:{PORT}/");
                }
            }
            catch
            {
                return false;
            }
        }

        // ✅ TỰ ĐỘNG THÊM URL RESERVATION
        private bool AddUrlReservation()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"http add urlacl url=http://+:{PORT}/ user=Everyone",
                    UseShellExecute = true,
                    Verb = "runas", // Request admin
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi)!)
                {
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        // ✅ KIỂM TRA FIREWALL
        private void CheckFirewallStatus()
        {
            try
            {
                // Tên rule cũng nên có số port để dễ quản lý
                string ruleName = $"RAT Server Port {PORT}";
                
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi)!)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    if (!output.Contains("Rule Name"))
                    {
                        LogToUI($"⚠️ CẢNH BÁO: Chưa tìm thấy Firewall rule tên \"{ruleName}\"!");
                        LogToUI("💡 Nếu bạn chưa mở port thủ công, hãy chạy lệnh sau:");
                        LogToUI($"   netsh advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={PORT}");
                    }
                    else
                    {
                        LogToUI($"✅ Đã tìm thấy Firewall rule \"{ruleName}\"");
                    }
                }
            }
            catch
            {
                LogToUI("⚠️ Không thể kiểm tra Firewall status");
            }
        }
    }
}