using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebSocketTest.Models;

namespace WebSocketTest.Services
{
    public class WebcamService
    {
        private CancellationTokenSource? _recordingCts;
        private bool _isRecording = false;

        // ✅ Start recording với duration và stop support
        public async Task StartRecordingAsync(Func<string, Task> sendAsync, int durationSeconds)
        {
            if (_isRecording)
            {
                await sendAsync(JsonResponse.Error("Already recording!"));
                return;
            }

            _isRecording = true;
            _recordingCts = new CancellationTokenSource();

            try
            {
                var frames = await RecordFramesAsync(durationSeconds, _recordingCts.Token);
                
                if (frames == null || frames.Count == 0)
                {
                    await sendAsync(JsonResponse.Error("Không thể quay video"));
                    return;
                }

                // Send frames to client
                await SendFramesToClientAsync(sendAsync, frames);
            }
            catch (OperationCanceledException)
            {
                // User stopped - send what we have
                var frames = _recordingCts?.Token.IsCancellationRequested == true ? 
                    new List<string>() : new List<string>();
                
                if (frames.Count > 0)
                {
                    await SendFramesToClientAsync(sendAsync, frames);
                }
            }
            finally
            {
                _isRecording = false;
                _recordingCts?.Dispose();
                _recordingCts = null;
            }
        }

        // ✅ Stop recording
        public void StopRecording()
        {
            if (_isRecording && _recordingCts != null)
            {
                _recordingCts.Cancel();
            }
        }

        // ✅ Record frames với cancellation
        private async Task<List<string>> RecordFramesAsync(int durationSeconds, CancellationToken ct)
        {
            OpenCvSharp.VideoCapture? capture = null;
            OpenCvSharp.Mat? frame = null;
            List<string> frames = new List<string>();

            try
            {
                capture = new OpenCvSharp.VideoCapture(0);
                if (!capture.IsOpened()) return frames;

                frame = new OpenCvSharp.Mat();
                
                int width = 800;
                int height = 600;
                int fps = 30;
                int jpegQuality = 65;
                
                capture.Set(OpenCvSharp.VideoCaptureProperties.FrameWidth, width);
                capture.Set(OpenCvSharp.VideoCaptureProperties.FrameHeight, height);
                capture.Set(OpenCvSharp.VideoCaptureProperties.Fps, fps);

                int totalFrames = durationSeconds * fps;
                int delayBetweenFrames = 1000 / fps;
                int[] compressionParams = new int[] { 
                    (int)OpenCvSharp.ImwriteFlags.JpegQuality, jpegQuality 
                };

                for (int i = 0; i < totalFrames; i++)
                {
                    // ✅ Check cancellation
                    if (ct.IsCancellationRequested) break;
                    
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
                        Console.WriteLine($"Frame {i} error: {ex.Message}");
                        continue;
                    }

                    var processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                    int waitTime = delayBetweenFrames - (int)processingTime;
                    
                    if (waitTime > 0)
                    {
                        await Task.Delay(waitTime, ct);
                    }
                }

                return frames;
            }
            catch (OperationCanceledException)
            {
                return frames; // Return what we have
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Recording error: {ex.Message}");
                return frames;
            }
            finally
            {
                frame?.Dispose();
                if (capture != null)
                {
                    capture.Release();
                    capture.Dispose();
                }
            }
        }

        // ✅ Send frames in batches
        private async Task SendFramesToClientAsync(Func<string, Task> sendAsync, List<string> frames)
        {
            if (frames.Count == 0)
            {
                await sendAsync(JsonResponse.Error("No frames recorded"));
                return;
            }

            // Start
            var start = new { type = "video_start", count = frames.Count };
            await sendAsync(JsonSerializer.Serialize(start));

            // Batches
            const int batchSize = 30;
            for (int batchStart = 0; batchStart < frames.Count; batchStart += batchSize)
            {
                int batchEnd = Math.Min(batchStart + batchSize, frames.Count);
                var batch = new List<object>();
                
                for (int i = batchStart; i < batchEnd; i++)
                {
                    batch.Add(new { index = i, data = frames[i] });
                }

                var batchData = new { type = "video_batch", frames = batch };
                await sendAsync(JsonSerializer.Serialize(batchData));
                
                if (batchEnd < frames.Count)
                {
                    await Task.Delay(10);
                }
            }

            // End
            var end = new { type = "video_end" };
            await sendAsync(JsonSerializer.Serialize(end));
        }
    }
}