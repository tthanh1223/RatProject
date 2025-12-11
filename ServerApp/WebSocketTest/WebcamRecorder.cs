using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp; // Đảm bảo đã có cái này

namespace WebSocketTest
{
    /// <summary>
    /// Ghi hình từ webcam trong thời gian định sẵn với timeout bảo vệ
    /// </summary>
    public static class WebcamRecorder
    {
        /// <summary>
        /// Ghi hình từ webcam trong thời gian định sẵn với timeout bảo vệ
        /// </summary>
        /// <param name="timeoutMs">Timeout tối đa (mặc định 7000ms cho 5s ghi)</param>
        /// <returns>Danh sách frame base64 hoặc null nếu lỗi</returns>
        public static List<string>? RecordFrames(int timeoutMs = 7000)
        {
            VideoCapture? capture = null;
            Mat? frame = null;
            List<string> frames = new List<string>();

            try
            {
                capture = new VideoCapture(0);
                if (!capture.IsOpened()) 
                {
                    return null; // Không thể mở camera
                }

                frame = new Mat();

                // --- CẤU HÌNH ---
                int width = 800;    // 800x600 - vẫn tốt, không quá lớn
                int height = 600; 
                int recordTimeSec = 5; // Quay 5 giây
                int fps = 30;          // 30 FPS
                int jpegQuality = 65;  // Chất lượng tốt, không quá nặng

                capture.Set(VideoCaptureProperties.FrameWidth, width);
                capture.Set(VideoCaptureProperties.FrameHeight, height);
                capture.Set(VideoCaptureProperties.Fps, fps); 

                int totalFrames = recordTimeSec * fps;
                int delayBetweenFrames = 1000 / fps; 

                int[] compressionParams = new int[] { (int)ImwriteFlags.JpegQuality, jpegQuality };

                // Timeout token để thoát sớm nếu cần
                using (var cts = new CancellationTokenSource(timeoutMs))
                {
                    for (int i = 0; i < totalFrames && !cts.Token.IsCancellationRequested; i++)
                    {
                        var startTime = DateTime.Now;

                        capture.Read(frame);
                        if (frame.Empty()) break;

                        try
                        {
                            byte[] imgBytes = frame.ImEncode(".jpg", compressionParams);
                            string base64 = Convert.ToBase64String(imgBytes);
                            frames.Add(base64);
                        }
                        catch (Exception ex)
                        {
                            // Log lỗi encode nhưng tiếp tục
                            Console.WriteLine($"Lỗi encode frame {i}: {ex.Message}");
                            continue;
                        }

                        // Delay thông minh
                        var processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                        int waitTime = delayBetweenFrames - (int)processingTime;
                        if (waitTime > 0) Thread.Sleep(waitTime);
                    }
                }

                return frames.Count > 0 ? frames : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebcamRecorder Error: {ex.Message}");
                return null;
            }
            finally
            {
                // Đảm bảo dispose đúng cách
                if (frame != null) 
                {
                    frame.Dispose();
                    frame = null;
                }
                if (capture != null)
                {
                    capture.Release();
                    capture.Dispose();
                    capture = null;
                }
            }
        }
}
}