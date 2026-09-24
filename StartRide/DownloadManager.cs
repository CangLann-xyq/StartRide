using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace StartRide.Core
{
    /// <summary>一个下载任务。</summary>
    public sealed class DownloadTask
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string TargetPath { get; set; } = "";
        public string Status { get; set; } = DownloadStatus.Waiting;
        public string Error { get; set; } = "";

        public long TotalBytes { get; set; }
        public long ReceivedBytes { get; set; }
        public double SpeedBps { get; set; }

        internal CancellationTokenSource? Cts { get; set; }

        public double Progress =>
            TotalBytes > 0 ? Math.Clamp((double)ReceivedBytes / TotalBytes, 0, 1) : 0;

        public bool IsRunning => Status == DownloadStatus.Downloading;
        public bool IsFinished => Status is DownloadStatus.Done or DownloadStatus.Failed
                                          or DownloadStatus.Canceled;

        public string SizeText
        {
            get
            {
                string got = Format(ReceivedBytes);
                return TotalBytes > 0 ? $"{got} / {Format(TotalBytes)}" : got;
            }
        }

        public string SpeedText => IsRunning ? Format((long)SpeedBps) + "/s" : "";

        internal static string Format(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes;
            int u = 0;
            while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
            return u == 0 ? $"{v:0} {units[u]}" : $"{v:0.0} {units[u]}";
        }
    }

    public static class DownloadStatus
    {
        public const string Waiting = "等待中";
        public const string Downloading = "下载中";
        public const string Done = "已完成";
        public const string Failed = "失败";
        public const string Canceled = "已取消";
    }

    /// <summary>
    /// 下载队列。对应原版的 InstallPageView —— 那里列的是"资源安装任务"，
    /// 这里做成通用文件下载：给一个地址 + 目标目录就能跑，带进度与限速。
    /// </summary>
    public sealed class DownloadManager
    {
        private readonly AppSettings _settings;
        private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

        /// <summary>
        /// 并发闸门。设置页里的「下载线程数」以前只写进 settings.json，下载引擎根本不读，
        /// 结果所有任务无条件并行（点一堆链接直接把带宽打满）。
        /// 这里按 DownloadThreads 真正限流：拿不到槽位的任务停在"等待中"。
        /// </summary>
        private readonly object _slotsLock = new();
        private SemaphoreSlim _slots = new(4, 4);
        private int _slotCount = 4;

        public ObservableCollection<DownloadTask> Tasks { get; } = new();

        public event Action<string>? Log;
        public event Action? Changed;

        public DownloadManager(AppSettings settings) => _settings = settings;

        /// <summary>当前生效的并发数（= 设置里的下载线程数，1..16）。</summary>
        public int Concurrency => Math.Clamp(_settings.DownloadThreads, 1, 16);

        /// <summary>
        /// 取当前并发闸门。设置改了并发数就换一个新闸门：新任务按新上限排队，
        /// 在跑的任务把槽位还到旧闸门上 —— 还槽时用 Release 的 Full 兜底，不会抛。
        /// </summary>
        private SemaphoreSlim EnsureSlots()
        {
            int want = Concurrency;
            lock (_slotsLock)
            {
                if (want != _slotCount)
                {
                    _slotCount = want;
                    _slots = new SemaphoreSlim(want, want);
                }
                return _slots;
            }
        }

        /// <summary>新建任务。fileName 为空时从 URL 推断。</summary>
        public DownloadTask Enqueue(string url, string targetDirectory, string? fileName = null)
        {
            string name = fileName ?? GuessFileName(url);
            var task = new DownloadTask
            {
                Name = name,
                Url = url,
                TargetPath = Path.Combine(targetDirectory, name),
            };
            Tasks.Add(task);
            Changed?.Invoke();
            _ = RunAsync(task);
            return task;
        }

        private static string GuessFileName(string url)
        {
            try
            {
                string path = new Uri(url).AbsolutePath;
                string leaf = Path.GetFileName(path);
                if (!string.IsNullOrWhiteSpace(leaf)) return Uri.UnescapeDataString(leaf);
            }
            catch { }
            return "download-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bin";
        }

        private async Task RunAsync(DownloadTask task)
        {
            task.Cts = new CancellationTokenSource();
            var token = task.Cts.Token;

            // 并发闸门：超过「下载线程数」的任务先停在"等待中"
            var slots = EnsureSlots();
            bool holdsSlot = false;

            try
            {
                task.Status = DownloadStatus.Waiting;
                Changed?.Invoke();
                await slots.WaitAsync(token).ConfigureAwait(false);
                holdsSlot = true;

                Directory.CreateDirectory(Path.GetDirectoryName(task.TargetPath)!);
                task.Status = DownloadStatus.Downloading;
                Changed?.Invoke();

                using var resp = await _http.GetAsync(task.Url, HttpCompletionOption.ResponseHeadersRead, token);
                resp.EnsureSuccessStatusCode();

                task.TotalBytes = resp.Content.Headers.ContentLength ?? 0;

                await using (var src = await resp.Content.ReadAsStreamAsync(token))
                await using (var dst = new FileStream(task.TargetPath, FileMode.Create, FileAccess.Write,
                                                      FileShare.None, 81920, useAsync: true))
                {
                    var buffer = new byte[81920];
                    int limitKbps = _settings.DownloadSpeedLimitKbps;
                    var sw = Stopwatch.StartNew();
                    long windowBytes = 0;
                    var windowStart = sw.Elapsed;

                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        int n = await src.ReadAsync(buffer, token);
                        if (n <= 0) break;

                        await dst.WriteAsync(buffer.AsMemory(0, n), token);

                        task.ReceivedBytes += n;
                        windowBytes += n;

                        var now = sw.Elapsed;
                        var span = now - windowStart;
                        if (span.TotalMilliseconds >= 200)
                        {
                            task.SpeedBps = windowBytes / span.TotalSeconds;

                            // 限速：睡够这一窗口该用的时间
                            if (limitKbps > 0)
                            {
                                double allowedMs = windowBytes / 1024.0 / limitKbps * 1000.0;
                                double excess = span.TotalMilliseconds - allowedMs;
                                if (excess > 0)
                                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(excess, 500)), token);
                            }

                            windowBytes = 0;
                            windowStart = sw.Elapsed;
                            Changed?.Invoke();
                        }
                    }
                }

                task.Status = DownloadStatus.Done;
                task.SpeedBps = 0;
                Log?.Invoke($"下载完成：{task.TargetPath}");
            }
            catch (OperationCanceledException)
            {
                task.Status = DownloadStatus.Canceled;
                TryDeletePartial(task);
            }
            catch (Exception ex)
            {
                task.Status = DownloadStatus.Failed;
                task.Error = ex.Message;
                Log?.Invoke($"下载失败（{task.Name}）：{ex.Message}");
            }
            finally
            {
                // 还槽位。并发数被改小过时可能还到已满的旧闸门 → 吞掉 Full，不影响流程。
                if (holdsSlot)
                {
                    try { slots.Release(); }
                    catch (SemaphoreFullException) { }
                }
                task.Cts?.Dispose();
                task.Cts = null;
                Changed?.Invoke();
            }
        }

        public void Cancel(string id)
        {
            var t = Tasks.FirstOrDefaultBy(t => t.Id == id);
            if (t == null) return;
            if (!t.IsFinished)
            {
                // 排队中（Waiting）也要取消：不撤掉 CTS 的话它拿到槽位后照样会开始下
                try { t.Cts?.Cancel(); } catch { }
                if (!t.IsRunning) t.Status = DownloadStatus.Canceled;
            }
            Changed?.Invoke();
        }

        public void Remove(DownloadTask task)
        {
            if (task.IsRunning) { try { task.Cts?.Cancel(); } catch { } }
            Tasks.Remove(task);
            Changed?.Invoke();
        }

        public void Retry(DownloadTask task)
        {
            if (task.IsRunning) return;
            task.Status = DownloadStatus.Waiting;
            task.Error = "";
            task.ReceivedBytes = 0;
            task.TotalBytes = 0;
            _ = RunAsync(task);
        }

        public void OpenInExplorer(DownloadTask task)
        {
            try
            {
                string dir = Path.GetDirectoryName(task.TargetPath) ?? "";
                if (File.Exists(task.TargetPath))
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{task.TargetPath}\""));
                else if (Directory.Exists(dir))
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\""));
                else
                    MessageBox.Show("目标目录不存在。");
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开目录失败：" + ex.Message);
            }
        }

        private static void TryDeletePartial(DownloadTask task)
        {
            try { if (File.Exists(task.TargetPath)) File.Delete(task.TargetPath); } catch { }
        }
    }

    internal static class EnumerableShim
    {
        public static T? FirstOrDefaultBy<T>(this System.Collections.Generic.IEnumerable<T> source,
                                             Func<T, bool> predicate)
        {
            foreach (var item in source) if (predicate(item)) return item;
            return default;
        }
    }
}
