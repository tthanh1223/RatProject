using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using WebSocketTest.Models;

namespace WebSocketTest.Services
{
    public class WebcamService
    {
        public async Task CaptureAndSendAsync(Func<string, Task> sendAsync, int timeoutMs)
        {
            var frames = RecordFrames(timeoutMs);
            if (frames == null || frames.Count == 0)
            {
                await sendAsync(JsonResponse.Error("Lỗi: Không thể mở Webcam hoặc không ghi được frame nào."));
                return;
            }

            // Send start
            var start = new { type = "video_start", count = frames.Count };
            await sendAsync(JsonSerializer.Serialize(start));

            // Send frames in batches to avoid huge payloads
            const int batchSize = 30;
            for (int batchStart = 0; batchStart < frames.Count; batchStart += batchSize)
            {
                int batchEnd = Math.Min(batchStart + batchSize, frames.Count);
                var batch = new List<object>();
                for (int i = batchStart; i < batchEnd; i++) batch.Add(new { index = i, data = frames[i] });

                var batchData = new { type = "video_batch", frames = batch };
                await sendAsync(JsonSerializer.Serialize(batchData));
            }

            var end = new { type = "video_end" };
            await sendAsync(JsonSerializer.Serialize(end));
        }
        
        // Integrated recorder logic (previously in WebcamRecorderService)
        private static List<string>? RecordFrames(int timeoutMs = 7000)
        {
            OpenCvSharp.VideoCapture? capture = null;
            OpenCvSharp.Mat? frame = null;
            List<string> frames = new List<string>();

            try
            {
                capture = new OpenCvSharp.VideoCapture(0);
                if (!capture.IsOpened()) return null;

                frame = new OpenCvSharp.Mat();
                int width = 800; int height = 600; int recordTimeSec = 5; int fps = 30; int jpegQuality = 65;
                capture.Set(OpenCvSharp.VideoCaptureProperties.FrameWidth, width);
                capture.Set(OpenCvSharp.VideoCaptureProperties.FrameHeight, height);
                capture.Set(OpenCvSharp.VideoCaptureProperties.Fps, fps);

                int totalFrames = recordTimeSec * fps;
                int delayBetweenFrames = 1000 / fps;
                int[] compressionParams = new int[] { (int)OpenCvSharp.ImwriteFlags.JpegQuality, jpegQuality };

                using (var cts = new System.Threading.CancellationTokenSource(timeoutMs))
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
                        catch (Exception ex) { Console.WriteLine($"Lỗi encode frame {i}: {ex.Message}"); continue; }

                        var processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                        int waitTime = delayBetweenFrames - (int)processingTime;
                        if (waitTime > 0) System.Threading.Thread.Sleep(waitTime);
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
                if (frame != null) { frame.Dispose(); frame = null; }
                if (capture != null) { capture.Release(); capture.Dispose(); capture = null; }
            }
        }
    }
}
