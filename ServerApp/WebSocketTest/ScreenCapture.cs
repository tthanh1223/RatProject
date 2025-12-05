using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace WebSocketTest
{
    public static class ScreenCapture
    {
        public static string GetScreenshotBase64()
        {
            try
            {
                // Lấy kích thước màn hình chính
                Rectangle bounds = Screen.PrimaryScreen.Bounds;

                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        // Chụp toàn bộ màn hình
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Lưu ảnh vào MemoryStream dưới dạng JPEG (để nhẹ hơn PNG)
                        // Nếu muốn nét hơn thì dùng ImageFormat.Png
                        bitmap.Save(ms, ImageFormat.Jpeg);
                        
                        byte[] imageBytes = ms.ToArray();
                        
                        // Chuyển sang Base64
                        return Convert.ToBase64String(imageBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                return "ERROR:" + ex.Message;
            }
        }
    }
}