using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StartRide.Core
{
    /// <summary>
    /// 模组图标缓存：把 BeamNG 官方仓库里**模组作者自己上传的图标**（data/resource_icons/…）
    /// 下载到本地再交给界面绑定。
    ///
    /// 为什么不直接把远程 URL 塞给 Image.Source：
    ///   1. WPF 自己的下载不走我们配置的 HttpClient（无浏览器 UA、走系统代理、无超时控制），
    ///      在受限网络下经常空白；
    ///   2. 缓存到本地后，列表反复滚动、重启启动器都不会重复下载，也支持离线显示。
    ///
    /// 与 Steam 头像缓存同一套约定（见 SteamLoginClient.AvatarCacheDirectory）：
    /// 落到 %APPDATA%\StartRide\modicons\&lt;资源ID&gt;.&lt;ext&gt;，**绝不写 EXE 旁**。
    /// </summary>
    public static class ModIconCache
    {
        private static readonly HttpClient Http = CreateHttp();

        /// <summary>图标缓存目录：%APPDATA%\StartRide\modicons。</summary>
        public static string CacheDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StartRide",
            "modicons");

        // 进程内缓存：同一次会话里滚动/重排不会重复读盘
        private static readonly ConcurrentDictionary<long, ImageSource?> Memory = new();

        // 同一 ID 并发只下一次（列表重建时会出现多个相同请求）
        private static readonly ConcurrentDictionary<long, Task<ImageSource?>> InFlight = new();

        private static HttpClient CreateHttp()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false,
                UseProxy = false,
                ConnectTimeout = TimeSpan.FromSeconds(8),
                MaxConnectionsPerServer = 8,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            return client;
        }

        /// <summary>
        /// 取图标位图。命中内存/磁盘直接返回；否则下载后落盘。
        /// 任何失败都返回 null（调用方回退到默认图标），不抛异常。
        /// </summary>
        public static async Task<ImageSource?> GetAsync(long resourceId, string iconUrl, CancellationToken ct = default)
        {
            if (resourceId <= 0 || string.IsNullOrWhiteSpace(iconUrl))
            {
                return null;
            }
            if (Memory.TryGetValue(resourceId, out ImageSource? cached))
            {
                return cached;
            }
            return await InFlight.GetOrAdd(resourceId, id => LoadOrDownloadAsync(id, iconUrl, ct)).ConfigureAwait(false);
        }

        private static async Task<ImageSource?> LoadOrDownloadAsync(long resourceId, string iconUrl, CancellationToken ct)
        {
            try
            {
                string? path = FindCachedFile(resourceId);
                if (path == null)
                {
                    string ext = GuessExtension(iconUrl);
                    path = Path.Combine(CacheDirectory, resourceId.ToString() + ext);
                    if (!await TryDownloadToFileAsync(iconUrl, path, ct).ConfigureAwait(false))
                    {
                        return null;
                    }
                }

                ImageSource? image = LoadFrozen(path);
                if (image != null)
                {
                    Memory[resourceId] = image;
                }
                return image;
            }
            catch
            {
                return null;
            }
            finally
            {
                InFlight.TryRemove(resourceId, out _);
            }
        }

        private static string? FindCachedFile(long resourceId)
        {
            try
            {
                if (!Directory.Exists(CacheDirectory))
                {
                    return null;
                }
                foreach (string ext in new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" })
                {
                    string p = Path.Combine(CacheDirectory, resourceId.ToString() + ext);
                    if (File.Exists(p) && new FileInfo(p).Length > 128)
                    {
                        return p;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static string GuessExtension(string url)
        {
            string path = url.Split('?')[0].Split('#')[0];
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" ? ext : ".jpg";
        }

        private static async Task<bool> TryDownloadToFileAsync(string url, string path, CancellationToken ct)
        {
            try
            {
                byte[] bytes = await Http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
                if (bytes.Length < 128)
                {
                    return false;
                }
                Directory.CreateDirectory(CacheDirectory);
                // 先写临时文件再改名：避免半截文件被下次当成有效缓存
                string tmp = path + ".part";
                File.WriteAllBytes(tmp, bytes);
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { }
                }
                File.Move(tmp, path, overwrite: true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static ImageSource? LoadFrozen(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                // OnLoad + Freeze：读盘后立刻断开文件句柄，跨线程绑定安全
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.DecodePixelWidth = 160;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }
}
