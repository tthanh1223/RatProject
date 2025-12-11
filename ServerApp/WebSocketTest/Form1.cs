using System;
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
                // Truyền hàm LogToUI vào server để nó gọi khi cần in log
                _server = new SimpleWebSocketServer(LogToUI);
                // Thay đổi: Lắng nghe trên 0.0.0.0 để cho phép kết nối từ máy khác
                _server.Start("http://0.0.0.0:8080/");
                
                _serverRunning = true;
                btnStart.Enabled = false;
                btnStart.Text = "Running...";
                btnStop.Enabled = true;
                LogToUI("Server sẵn sàng. Hãy kết nối Client!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi (Chạy Admin chưa?): " + ex.Message);
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

        // SỰ KIỆN MỚI: Bấm nút Gửi
        private async void btnSend_Click(object sender, EventArgs e)
        {
            if (_server == null) return;

            string msg = txtMessage.Text.Trim();
            if (!string.IsNullOrEmpty(msg))
            {
                // Gọi hàm gửi tin nhắn của Server
                await _server.SendToClient(msg);
                
                // Xóa ô nhập sau khi gửi
                txtMessage.Clear();
                txtMessage.Focus();
            }
        }

        // Cho phép ấn Enter để gửi luôn
        private void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSend_Click(this, new EventArgs());
                e.SuppressKeyPress = true; // Chặn tiếng 'ding' của Windows
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
    }
}