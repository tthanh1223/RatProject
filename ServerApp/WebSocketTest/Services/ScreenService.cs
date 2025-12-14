using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using WebSocketTest.Models;

namespace WebSocketTest.Services
{
    public class ScreenService
    {
        public string GetScreen()
        {
            string base64Image = GetScreenshotBase64();

            if (base64Image.StartsWith("ERROR"))
            {
                return JsonResponse.Error("Lỗi chụp màn hình: " + base64Image);
            }

            return "{\"type\": \"screen_capture\", \"data\": \"" + base64Image + "\"}";
        }

        private static string GetScreenshotBase64()
        {
            try
            {
                Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;

                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        bitmap.Save(ms, ImageFormat.Jpeg);
                        byte[] imageBytes = ms.ToArray();
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
