using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using AForge.Video;
using AForge.Video.DirectShow;
using WebSocketTest.Models;

namespace WebSocketTest.Services
{
    public class WebcamService
    {
        private VideoCaptureDevice? _videoSource;
        private List<string> _frames = new List<string>();
        private CancellationTokenSource? _recordingCts;
        private bool _isRecording = false;
        private readonly object _lockObj = new object();
        private int _targetFrameCount = 0;
        private DateTime _recordingStartTime;

        public async Task StartRecordingAsync(Func<string, Task> sendAsync, int durationSeconds)
        {
            Console.WriteLine($"🔴 [WEBCAM] StartRecordingAsync called, duration: {durationSeconds}s");
            
            lock (_lockObj)
            {
                if (_isRecording)
                {
                    Console.WriteLine("🔴 [WEBCAM] Already recording!");
                    _ = sendAsync(JsonResponse.Error("Already recording!"));
                    return;
                }
                _isRecording = true;
            }

            _recordingCts = new CancellationTokenSource();
            _frames.Clear();
            _recordingStartTime = DateTime.Now;

            try
            {
                Console.WriteLine("🔴 [WEBCAM] Sending info message...");
                await sendAsync(JsonResponse.Info($"Khởi động camera ({durationSeconds}s)..."));
                
                Console.WriteLine("🔴 [WEBCAM] Starting RecordFramesAsync...");
                await RecordFramesAsync(durationSeconds, _recordingCts.Token);
                
                Console.WriteLine($"🔴 [WEBCAM] Recording finished, frames: {_frames.Count}");
                
                if (_frames.Count > 0)
                {
                    Console.WriteLine("🔴 [WEBCAM] Sending frames to client...");
                    await SendFramesToClientAsync(sendAsync, _frames);
                }
                else
                {
                    Console.WriteLine("🔴 [WEBCAM] No frames captured!");
                    await sendAsync(JsonResponse.Error("Không thể quay video"));
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"🔴 [WEBCAM] Recording cancelled by user, frames: {_frames.Count}");
                if (_frames.Count > 0)
                {
                    await SendFramesToClientAsync(sendAsync, _frames);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔴 [WEBCAM] ERROR: {ex.Message}");
                Console.WriteLine($"🔴 [WEBCAM] StackTrace: {ex.StackTrace}");
                await sendAsync(JsonResponse.Error("Lỗi camera: " + ex.Message));
            }
            finally
            {
                Console.WriteLine("🔴 [WEBCAM] Calling StopAndCleanup...");
                StopAndCleanup();
            }
        }

        public void StopRecording()
        {
            Console.WriteLine("🔴 [WEBCAM] StopRecording called");
            
            lock (_lockObj)
            {
                if (_isRecording && _recordingCts != null)
                {
                    Console.WriteLine("🔴 [WEBCAM] Cancelling recording...");
                    _recordingCts.Cancel();
                }
                else
                {
                    Console.WriteLine("🔴 [WEBCAM] Not recording or CTS is null");
                }
            }
        }

        private async Task RecordFramesAsync(int durationSeconds, CancellationToken ct)
        {
            Console.WriteLine($"🔴 [WEBCAM] RecordFramesAsync start, duration: {durationSeconds}s");
            
            const int fps = 30;
            const int jpegQuality = 65;
            
            _targetFrameCount = durationSeconds * fps;
            _frames = new List<string>(_targetFrameCount);
            
            Console.WriteLine($"🔴 [WEBCAM] Target frames: {_targetFrameCount}");

            try
            {
                // 1. Get camera list
                Console.WriteLine("🔴 [WEBCAM] Getting camera list...");
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                Console.WriteLine($"🔴 [WEBCAM] Found {videoDevices.Count} camera(s)");
                
                if (videoDevices.Count == 0)
                {
                    throw new Exception("Không tìm thấy camera");
                }

                // 2. Initialize camera
                Console.WriteLine($"🔴 [WEBCAM] Initializing camera: {videoDevices[0].Name}");
                _videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
                
                // 3. Set resolution
                if (_videoSource.VideoCapabilities.Length > 0)
                {
                    Console.WriteLine($"🔴 [WEBCAM] Available resolutions: {_videoSource.VideoCapabilities.Length}");
                    
                    var capability = _videoSource.VideoCapabilities
                        .OrderBy(c => Math.Abs(c.FrameSize.Width - 800) + Math.Abs(c.FrameSize.Height - 600))
                        .FirstOrDefault();
                    
                    if (capability != null)
                    {
                        _videoSource.VideoResolution = capability;
                        Console.WriteLine($"🔴 [WEBCAM] Resolution: {capability.FrameSize.Width}x{capability.FrameSize.Height}");
                    }
                }

                // 4. Setup event handler
                Console.WriteLine("🔴 [WEBCAM] Setting up NewFrame event...");
                
                _videoSource.NewFrame += (sender, eventArgs) =>
                {
                    if (ct.IsCancellationRequested || _frames.Count >= _targetFrameCount)
                    {
                        return;
                    }

                    try
                    {
                        using (Bitmap frame = (Bitmap)eventArgs.Frame.Clone())
                        {
                            using (var ms = new MemoryStream())
                            {
                                var encoderParams = new EncoderParameters(1);
                                encoderParams.Param[0] = new EncoderParameter(
                                    System.Drawing.Imaging.Encoder.Quality, (long)jpegQuality);
                                
                                var jpegEncoder = GetEncoder(ImageFormat.Jpeg);
                                frame.Save(ms, jpegEncoder, encoderParams);
                                
                                string base64 = Convert.ToBase64String(ms.ToArray());
                                
                                lock (_lockObj)
                                {
                                    _frames.Add(base64);
                                    
                                    // Log every 30 frames
                                    if (_frames.Count % 30 == 0)
                                    {
                                        Console.WriteLine($"🔴 [WEBCAM] Captured {_frames.Count}/{_targetFrameCount} frames");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"🔴 [WEBCAM] Frame capture error: {ex.Message}");
                    }
                };

                // 5. Start camera
                Console.WriteLine("🔴 [WEBCAM] Starting camera...");
                _videoSource.Start();
                Console.WriteLine("🔴 [WEBCAM] Camera started!");

                // 6. Wait for completion
                while (!ct.IsCancellationRequested && _frames.Count < _targetFrameCount)
                {
                    await Task.Delay(100, ct);
                }

                Console.WriteLine($"🔴 [WEBCAM] Recording loop finished: {_frames.Count} frames");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔴 [WEBCAM] RecordFramesAsync ERROR: {ex.Message}");
                throw;
            }
        }

        private void StopAndCleanup()
        {
            Console.WriteLine("🔴 [WEBCAM] StopAndCleanup start");
            
            lock (_lockObj)
            {
                try
                {
                    if (_videoSource != null)
                    {
                        Console.WriteLine($"🔴 [WEBCAM] VideoSource exists, IsRunning: {_videoSource.IsRunning}");
                        
                        if (_videoSource.IsRunning)
                        {
                            Console.WriteLine("🔴 [WEBCAM] Calling SignalToStop...");
                            _videoSource.SignalToStop();
                            
                            Console.WriteLine("🔴 [WEBCAM] Calling WaitForStop...");
                            _videoSource.WaitForStop();
                            
                            Console.WriteLine("🔴 [WEBCAM] ✅ Camera stopped!");
                        }

                        _videoSource = null;
                        Console.WriteLine("🔴 [WEBCAM] ✅ VideoSource set to null");
                    }
                    else
                    {
                        Console.WriteLine("🔴 [WEBCAM] VideoSource is already null");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"🔴 [WEBCAM] Cleanup error: {ex.Message}");
                }
                finally
                {
                    _isRecording = false;
                    _recordingCts?.Dispose();
                    _recordingCts = null;
                    Console.WriteLine("🔴 [WEBCAM] ✅ Cleanup complete");
                }
            }
        }

        private async Task SendFramesToClientAsync(Func<string, Task> sendAsync, List<string> frames)
        {
            Console.WriteLine($"🔴 [WEBCAM] SendFramesToClient start, frames: {frames.Count}");
            
            if (frames.Count == 0)
            {
                await sendAsync(JsonResponse.Error("No frames recorded"));
                return;
            }

            // Start
            var start = new { type = "video_start", count = frames.Count };
            Console.WriteLine("🔴 [WEBCAM] Sending video_start...");
            await sendAsync(JsonSerializer.Serialize(start));

            // Batches
            const int batchSize = 30;
            int batchCount = 0;
            
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
                
                batchCount++;
                Console.WriteLine($"🔴 [WEBCAM] Sent batch {batchCount}, frames {batchStart}-{batchEnd}");
                
                if (batchEnd < frames.Count)
                {
                    await Task.Delay(10);
                }
            }

            // End
            var end = new { type = "video_end" };
            Console.WriteLine("🔴 [WEBCAM] Sending video_end...");
            await sendAsync(JsonSerializer.Serialize(end));
            
            Console.WriteLine("🔴 [WEBCAM] ✅ All frames sent!");
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return codecs[0];
        }
    }
}