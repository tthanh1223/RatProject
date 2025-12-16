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
            lock (_lockObj)
            {
                if (_isRecording)
                {
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
                await sendAsync(JsonResponse.Info($"Khởi động camera ({durationSeconds}s)..."));
                // Send message de client bat bo dem gio chi khi cam da mo
                var startedMsg = new {type = "rec_started"};
                await sendAsync(JsonSerializer.Serialize(startedMsg));
                // Start record
                await RecordFramesAsync(durationSeconds, _recordingCts.Token);
                                
                if (_frames.Count > 0)
                {
                    await SendFramesToClientAsync(sendAsync, _frames);
                }
                else
                {
                    await sendAsync(JsonResponse.Error("Không thể quay video"));
                }
            }
            catch (OperationCanceledException)
            {
                if (_frames.Count > 0)
                {
                    await SendFramesToClientAsync(sendAsync, _frames);
                }
            }
            catch (Exception ex)
            {
                await sendAsync(JsonResponse.Error("Lỗi camera: " + ex.Message));
            }
            finally
            {
                StopAndCleanup();
            }
        }

        public void StopRecording()
        {
            lock (_lockObj)
            {
                if (_isRecording && _recordingCts != null)
                {
                    _recordingCts.Cancel();
                }
            }
        }

        private async Task RecordFramesAsync(int durationSeconds, CancellationToken ct)
        {            
            const int fps = 30;
            const int jpegQuality = 65;
            
            _targetFrameCount = durationSeconds * fps;
            _frames = new List<string>(_targetFrameCount);
            
            try
            {
                // 1. Get camera list
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                
                if (videoDevices.Count == 0)
                {
                    throw new Exception("Không tìm thấy camera");
                }

                // 2. Initialize camera
                _videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
                
                // 3. Set resolution
                if (_videoSource.VideoCapabilities.Length > 0)
                {
                    var capability = _videoSource.VideoCapabilities
                        .OrderBy(c => Math.Abs(c.FrameSize.Width - 800) + Math.Abs(c.FrameSize.Height - 600))
                        .FirstOrDefault();
                    
                    if (capability != null)
                    {
                        _videoSource.VideoResolution = capability;
                    }
                }

                // 4. Setup event handler
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
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore individual frame errors to keep recording
                    }
                };

                // 5. Start camera
                _videoSource.Start();

                // 6. Wait for completion
                while (!ct.IsCancellationRequested && _frames.Count < _targetFrameCount)
                {
                    await Task.Delay(100, ct);
                }
            }
            catch
            {
                throw;
            }
        }

        private void StopAndCleanup()
        {
            lock (_lockObj)
            {
                try
                {
                    if (_videoSource != null)
                    {
                        if (_videoSource.IsRunning)
                        {
                            _videoSource.SignalToStop();
                            _videoSource.WaitForStop();
                        }
                        _videoSource = null;
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
                finally
                {
                    _isRecording = false;
                    _recordingCts?.Dispose();
                    _recordingCts = null;
                }
            }
        }

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