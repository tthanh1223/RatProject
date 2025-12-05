using System;
using System.Windows.Forms;

namespace WebSocketTest
{
    public partial class Form1 : Form
    {
        private SimpleWebSocketServer? _server;

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
                _server.Start("http://localhost:8080/");
                
                btnStart.Enabled = false;
                btnStart.Text = "Running...";
                LogToUI("Server sẵn sàng. Hãy kết nối Client!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi (Chạy Admin chưa?): " + ex.Message);
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