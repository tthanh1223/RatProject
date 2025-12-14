using System;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace WebSocketTest
{
    public partial class Form1 : Form
    {
        private SimpleWebSocketServer? _server;
        private bool _serverRunning = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try 
            {
                _server = new SimpleWebSocketServer(LogToUI);
                
                // ✅ SỬA: Dùng + để lắng nghe trên TẤT CẢ IP
                // Lưu ý: Phải chạy Visual Studio với quyền Administrator
                _server.Start("http://+:8080/");
                
                _serverRunning = true;
                btnStart.Enabled = false;
                btnStart.Text = "Running...";
                btnStop.Enabled = true;
                
                // ✅ Hiển thị IP của máy để người dùng biết
                string localIP = GetLocalIPAddress();
                LogToUI("═══════════════════════════════════════════════");
                LogToUI($"✅ Server đã khởi động thành công!");
                LogToUI($"🔗 Để kết nối từ máy KHÁC, dùng: ws://{localIP}:8080/");
                LogToUI($"🔗 Để test trên máy này, dùng: ws://localhost:8080/");
                LogToUI("═══════════════════════════════════════════════");
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5)
            {
                MessageBox.Show(
                    "❌ LỖI: Access Denied!\n\n" +
                    "Bạn phải chạy Visual Studio với quyền ADMINISTRATOR.\n\n" +
                    "Cách fix:\n" +
                    "1. Đóng Visual Studio\n" +
                    "2. Click phải vào Visual Studio → Run as Administrator\n" +
                    "3. Mở lại project và chạy",
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
                    "- Port 8080 có bị chiếm không?\n" +
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
    }
}