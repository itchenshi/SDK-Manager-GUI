using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SDK_Manager_GUI.Models;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.Services
{
    public class DownloadEngine : IDownloadEngine
    {
        private HttpClient _httpClient;
        private readonly ILanguageService _languageService;
        private const int MaxRetryCount = 3;
        private const int RetryDelayMs = 2000;
        private const int BufferSize = 65536; // 64KB 缓冲区，提升大文件下载性能
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeCancellations = new ConcurrentDictionary<string, CancellationTokenSource>();

        // 速度计算相关
        private readonly ConcurrentDictionary<string, SpeedTracker> _speedTrackers = new ConcurrentDictionary<string, SpeedTracker>();

        public DownloadEngine(ILanguageService languageService)
        {
            _languageService = languageService;
            _httpClient = CreateHttpClient();
        }

        private HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                AllowAutoRedirect = true
            };

            var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(30) };
            client.DefaultRequestHeaders.Add("User-Agent", "SDK-Manager-GUI/1.0");
            return client;
        }

        public async Task<DownloadTask> DownloadAsync(string url, string targetPath, string taskId, IProgress<DownloadProgressInfo> progress, CancellationToken cancellationToken)
        {
            var task = new DownloadTask
            {
                TaskId = taskId,
                DownloadUrl = url,
                TargetPath = targetPath,
                Status = DownloadStatus.Downloading,
                StartTime = DateTime.Now
            };

            var tracker = new SpeedTracker();
            _speedTrackers[taskId] = tracker;

            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                _activeCancellations[taskId] = linkedCts;
                try
                {
                    return await DownloadWithRetryAsync(url, targetPath, task, progress, linkedCts.Token, tracker);
                }
                finally
                {
                    _activeCancellations.TryRemove(taskId, out _);
                    _speedTrackers.TryRemove(taskId, out _);
                }
            }
        }

        private async Task<DownloadTask> DownloadWithRetryAsync(string url, string targetPath, DownloadTask task, IProgress<DownloadProgressInfo> progress, CancellationToken cancellationToken, SpeedTracker tracker)
        {
            var tempPath = targetPath + ".downloading";

            for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
            {
                try
                {
                    var directory = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    // 断点续传：检查临时文件是否存在，获取已下载大小
                    long existingSize = 0;
                    if (File.Exists(tempPath))
                    {
                        existingSize = new FileInfo(tempPath).Length;
                    }

                    // 发送 Range 请求实现断点续传
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    if (existingSize > 0)
                    {
                        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingSize, null);
                    }

                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        // 处理不支持断点续传的情况
                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            // 服务器不支持 Range，从头开始下载
                            existingSize = 0;
                            task.TotalSize = response.Content.Headers.ContentLength ?? 0;
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.PartialContent)
                        {
                            // 服务器支持断点续传
                            var contentLength = response.Content.Headers.ContentLength ?? 0;
                            task.TotalSize = existingSize + contentLength;
                        }
                        else
                        {
                            response.EnsureSuccessStatusCode();
                            return task;
                        }

                        tracker.TotalSize = task.TotalSize;
                        tracker.DownloadedSize = existingSize;

                        // 报告初始进度
                        if (task.TotalSize > 0 && existingSize > 0)
                        {
                            var initPercent = (double)existingSize / task.TotalSize * 100;
                            progress?.Report(new DownloadProgressInfo
                            {
                                Percent = initPercent,
                                DownloadedSize = existingSize,
                                TotalSize = task.TotalSize,
                                Speed = 0,
                                RemainingTime = TimeSpan.Zero
                            });
                        }

                        var fileMode = existingSize > 0 ? FileMode.Append : FileMode.Create;
                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(tempPath, fileMode, FileAccess.Write, FileShare.None, BufferSize, true))
                        {
                            var buffer = new byte[BufferSize];
                            long totalRead = existingSize;
                            int bytesRead;
                            int lastReportedPercent = task.TotalSize > 0 ? (int)(existingSize / (double)task.TotalSize * 100) : 0;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                totalRead += bytesRead;
                                task.DownloadedSize = totalRead;

                                if (task.TotalSize > 0)
                                {
                                    var percent = (double)totalRead / task.TotalSize * 100;
                                    task.Progress = percent;

                                    // 更新速度追踪
                                    tracker.RecordBytes(bytesRead);

                                    // 节流：仅在百分比变化时报告进度，避免UI线程过载
                                    var currentPercent = (int)percent;
                                    if (currentPercent != lastReportedPercent)
                                    {
                                        lastReportedPercent = currentPercent;
                                        var speed = tracker.GetSpeed();
                                        var remaining = speed > 0 ? TimeSpan.FromSeconds((task.TotalSize - totalRead) / (double)speed) : TimeSpan.Zero;

                                        progress?.Report(new DownloadProgressInfo
                                        {
                                            Percent = percent,
                                            DownloadedSize = totalRead,
                                            TotalSize = task.TotalSize,
                                            Speed = speed,
                                            RemainingTime = remaining
                                        });
                                    }
                                }
                            }
                        }
                    }

                    // 下载完成，重命名临时文件
                    if (File.Exists(targetPath))
                        File.Delete(targetPath);
                    File.Move(tempPath, targetPath);

                    task.Status = DownloadStatus.Completed;
                    task.CompleteTime = DateTime.Now;
                    task.Progress = 100;
                    task.Speed = 0;
                    return task;
                }
                catch (OperationCanceledException)
                {
                    // 暂停时保留临时文件（断点续传），取消时删除
                    if (task.Status != DownloadStatus.Paused)
                    {
                        SafeDeleteFile(tempPath);
                        task.Status = DownloadStatus.Cancelled;
                    }
                    return task;
                }
                catch (HttpRequestException ex)
                {
                    task.ErrorMessage = string.Format(_languageService.GetString("Dialog_DownloadFailedAttempt"), attempt, MaxRetryCount, ex.Message);
                    if (attempt < MaxRetryCount)
                    {
                        // 报告重试状态
                        progress?.Report(new DownloadProgressInfo
                        {
                            Percent = task.Progress,
                            DownloadedSize = task.DownloadedSize,
                            TotalSize = task.TotalSize,
                            Speed = 0,
                            RemainingTime = TimeSpan.Zero,
                            Message = string.Format(_languageService.GetString("Dialog_DownloadFailRetry"), RetryDelayMs * attempt / 1000)
                        });
                        try { await Task.Delay(RetryDelayMs * attempt, cancellationToken); } catch (OperationCanceledException) { break; }
                    }
                }
                catch (Exception ex)
                {
                    task.ErrorMessage = string.Format(_languageService.GetString("Dialog_DownloadFailedAttempt"), attempt, MaxRetryCount, ex.Message);
                    if (attempt < MaxRetryCount)
                    {
                        progress?.Report(new DownloadProgressInfo
                        {
                            Percent = task.Progress,
                            DownloadedSize = task.DownloadedSize,
                            TotalSize = task.TotalSize,
                            Speed = 0,
                            RemainingTime = TimeSpan.Zero,
                            Message = string.Format(_languageService.GetString("Dialog_DownloadFailRetry"), RetryDelayMs * attempt / 1000)
                        });
                        try { await Task.Delay(RetryDelayMs * attempt, cancellationToken); } catch (OperationCanceledException) { break; }
                    }
                }
            }

            SafeDeleteFile(tempPath);
            task.Status = DownloadStatus.Failed;
            return task;
        }

        private void SafeDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        public Task<bool> PauseAsync(string taskId)
        {
            if (_activeCancellations.TryGetValue(taskId, out var cts))
            {
                cts.Cancel();
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> ResumeAsync(string taskId)
        {
            // 恢复由 BackgroundTaskManager 重新调用 DownloadAsync 实现
            // DownloadAsync 会检查 .downloading 临时文件实现断点续传
            return Task.FromResult(true);
        }

        public Task CancelAsync(string taskId)
        {
            if (_activeCancellations.TryGetValue(taskId, out var cts))
            {
                cts.Cancel();
                _activeCancellations.TryRemove(taskId, out _);
            }
            return Task.CompletedTask;
        }

        public Task<bool> ValidateFileAsync(string filePath, string expectedHash)
        {
            if (!File.Exists(filePath)) return Task.FromResult(false);
            if (string.IsNullOrEmpty(expectedHash)) return Task.FromResult(true);

            using (var stream = File.OpenRead(filePath))
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(stream);
                var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                return Task.FromResult(string.Equals(actualHash, expectedHash.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// 速度追踪器，使用滑动窗口计算下载速度
        /// </summary>
        private class SpeedTracker
        {
            private readonly object _lock = new object();
            private long _totalBytes;
            private DateTime _startTime;
            private readonly Queue<(DateTime Time, long Bytes)> _samples = new Queue<(DateTime, long)>();
            private long _sampleWindowBytes;

            public long TotalSize { get; set; }
            public long DownloadedSize { get; set; }

            public SpeedTracker()
            {
                _startTime = DateTime.Now;
            }

            public void RecordBytes(long bytes)
            {
                lock (_lock)
                {
                    _totalBytes += bytes;
                    DownloadedSize += bytes;
                    var now = DateTime.Now;
                    _samples.Enqueue((now, bytes));
                    _sampleWindowBytes += bytes;

                    // 在记录时也清理过期样本，防止样本无限累积
                    while (_samples.Count > 0 && (now - _samples.Peek().Time).TotalSeconds > 2.0)
                    {
                        var old = _samples.Dequeue();
                        _sampleWindowBytes -= old.Bytes;
                    }
                }
            }

            public long GetSpeed()
            {
                lock (_lock)
                {
                    var now = DateTime.Now;

                    // 移除2秒前的样本
                    while (_samples.Count > 0 && (now - _samples.Peek().Time).TotalSeconds > 2.0)
                    {
                        var old = _samples.Dequeue();
                        _sampleWindowBytes -= old.Bytes;
                    }

                    if (_samples.Count == 0) return 0;

                    var oldest = _samples.Peek().Time;
                    var elapsed = (now - oldest).TotalSeconds;

                    if (elapsed < 0.3) return 0; // 样本太少，不计算

                    return (long)(_sampleWindowBytes / elapsed);
                }
            }
        }
    }

    /// <summary>
    /// 下载进度信息，包含速度和剩余时间
    /// </summary>
    public class DownloadProgressInfo
    {
        public double Percent { get; set; }
        public long DownloadedSize { get; set; }
        public long TotalSize { get; set; }
        public long Speed { get; set; }
        public TimeSpan RemainingTime { get; set; }
        public string Message { get; set; }
    }
}
