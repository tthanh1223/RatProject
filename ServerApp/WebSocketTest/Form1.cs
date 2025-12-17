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
        // ✅ KHAI BÁO PORT (Sửa ở đây là ăn toàn bộ code)
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
                    "Vui lòng chạy lại với quyền 'Run as Administrator' để ứng dụng hoạt động ổn định.",
                    "Admin Check",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        // Nút Kill Server
        private void btnKill_Click(object sender, EventArgs e) => System.Diagnostics.Process.GetCurrentProcess().Kill();

        private void btnStart_Click(object sender, EventArgs e)
        {
            try 
            {
                // 1. KIỂM TRA URL RESERVATION
                if (!CheckUrlReservation())
                {
                    var result = MessageBox.Show(
                        $"❌ PHÁT HIỆN: Windows chưa cho phép bind vào http://+:{PORT}/\n\n" +
                        "Bạn có muốn tự động thêm quyền này không?",
                        "URL Reservation Required",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );

                    if (result == DialogResult.Yes)
                    {
                        if (AddUrlReservation())
                            MessageBox.Show("✅ Đã thêm URL reservation thành công!", "Success");
                        else
                        {
                            MessageBox.Show("❌ Thất bại. Vui lòng chạy thủ công.", "Error");
                            return;
                        }
                    }
                    else return;
                }

                // 2. KHỞI ĐỘNG SERVER
                _server = new SimpleWebSocketServer(LogToUI);
                _server.Start($"http://+:{PORT}/");
                
                _serverRunning = true;
                btnStart.Enabled = false;
                btnStart.Text = "Running...";
                btnStop.Enabled = true;
                
                // 3. HIỂN THỊ THÔNG TIN
                string localIP = GetLocalIPAddress();
                LogToUI("═══════════════════════════════════════════════");
                LogToUI($"✅ Server đã khởi động thành công!");
                LogToUI($"🔗 Từ máy KHÁC, kết nối: ws://{localIP}:{PORT}/");
                LogToUI($"🔗 Từ máy này, kết nối: ws://localhost:{PORT}/");
                LogToUI($"🌐 Server đang lắng nghe trên TẤT CẢ network interfaces");
                LogToUI("═══════════════════════════════════════════════");
                
                // 4. KIỂM TRA & TỰ ĐỘNG ADD FIREWALL
                CheckAndFixFirewall();
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5)
            {
                MessageBox.Show("❌ LỖI: Access Denied (Mã 5). Hãy chạy Admin!", "Lỗi quyền", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Lỗi khởi động: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            catch (Exception ex) { MessageBox.Show("Lỗi dừng server: " + ex.Message); }
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

        // --- CÁC HÀM HỖ TRỢ HỆ THỐNG ---

        private string GetLocalIPAddress()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint?.Address.ToString() ?? "127.0.0.1";
                }
            }
            catch { return "127.0.0.1"; }
        }

        private bool IsRunAsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        // --- URL RESERVATION LOGIC ---

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
                    return output.Contains($"http://+:{PORT}/") || output.Contains($"http://*:{PORT}/");
                }
            }
            catch { return false; }
        }

        private bool AddUrlReservation()
        {
            return RunNetshCommand($"http add urlacl url=http://+:{PORT}/ user=Everyone");
        }

        // --- FIREWALL LOGIC (Đã hoàn thiện) ---

        // Hàm kiểm tra và sửa lỗi Firewall tự động
        private void CheckAndFixFirewall()
        {
            try
            {
                string ruleName = $"RAT Server Port {PORT}";
                
                // Kiểm tra rule có tồn tại không
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
                        // 1. Ghi log cảnh báo như yêu cầu
                        LogToUI($"⚠️ CẢNH BÁO: Chưa tìm thấy Firewall rule tên \"{ruleName}\"!");
                        LogToUI($"💡 Đang đề xuất tự động mở port {PORT}...");

                        // 2. Hỏi người dùng
                        var result = MessageBox.Show(
                            $"Firewall chưa cho phép port {PORT}.\nBạn có muốn tự động thêm Rule không?",
                            "Firewall Check",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question
                        );

                        if (result == DialogResult.Yes)
                        {
                            // 3. Gọi hàm AddFirewallRule
                            if (AddFirewallRule())
                            {
                                LogToUI($"✅ Đã thêm Firewall rule \"{ruleName}\" thành công!");
                                MessageBox.Show("✅ Đã mở port thành công!", "Success");
                            }
                            else
                            {
                                LogToUI("❌ Lỗi khi thêm Firewall rule.");
                                MessageBox.Show("❌ Không thể thêm Firewall rule.", "Error");
                            }
                        }
                        else
                        {
                            LogToUI("💡 Bạn đã chọn KHÔNG mở port. Kết nối từ máy khác có thể bị chặn.");
                        }
                    }
                    else
                    {
                        LogToUI($"✅ Firewall rule \"{ruleName}\" đang hoạt động tốt.");
                    }
                }
            }
            catch (Exception ex)
            {
                LogToUI($"⚠️ Lỗi kiểm tra Firewall: {ex.Message}");
            }
        }

        // Hàm Add Firewall Rule hoàn chỉnh
        private bool AddFirewallRule()
        {
            string ruleName = $"RAT Server Port {PORT}";
            // Lệnh netsh chuẩn để mở port
            string command = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={PORT}";
            
            return RunNetshCommand(command);
        }

        // Hàm chạy lệnh Netsh chung (để tái sử dụng code)
        private bool RunNetshCommand(string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    UseShellExecute = true, // Cần true để dùng runas
                    Verb = "runas",         // Yêu cầu quyền Admin
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
    }
}