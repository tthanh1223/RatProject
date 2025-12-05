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
            // Tạo server và truyền hàm Log vào
            _server = new SimpleWebSocketServer(LogToUI);
            
            // Chạy server ở localhost cổng 8080
            _server.Start("http://localhost:8080/");
            
            btnStart.Enabled = false;
            btnStart.Text = "Running...";
        }

        // Hàm ghi log an toàn với luồng (Thread-safe)
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