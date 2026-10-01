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

    public static class ModIconCache
    {
        private static readonly HttpClient Http = CreateHttp();

        public static string CacheDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StartRide",
            "modicons");

        private static readonly ConcurrentDictionary<long, ImageSource?> Memory = new();

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
