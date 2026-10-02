using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Diagnostics;
using System.Threading;

namespace StartRide.Core
{
    public sealed class SteamUserInfo
    {
        public string SteamId64 { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string PersonaName { get; set; } = "";
        public string AvatarHash { get; set; } = "";
        public long Timestamp { get; set; }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(PersonaName))
                {
                    return PersonaName.Trim();
                }
                if (!string.IsNullOrWhiteSpace(AccountName))
                {
                    return AccountName.Trim();
                }
                return SteamId64;
            }
        }
    }

    public static class SteamLoginClient
    {
        private static readonly Regex UserBlockRegex = new(
            "\"(?<id>\\d{17})\"\\s*\\{(?<body>[^{}]*)\\}",
            RegexOptions.Compiled);

        private static readonly Regex FieldRegex = new(
            "\"(?<key>[A-Za-z]+)\"\\s*\"(?<val>[^\"]*)\"",
            RegexOptions.Compiled);

        private static readonly HttpClient Http = CreateHttp();

        private static string? cachedSteamPath;

        private static HttpClient CreateHttp()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false,
                UseProxy = false,
                ConnectTimeout = TimeSpan.FromSeconds(8),
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            return client;
        }

        public static string? GetSteamPath()
        {
            if (!string.IsNullOrWhiteSpace(cachedSteamPath) && Directory.Exists(cachedSteamPath))
            {
                return cachedSteamPath;
            }
            try
            {
                object? value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
                if (value is string path && Directory.Exists(path))
                {
                    cachedSteamPath = path;
                    return path;
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>Steam 客户端可执行文件路径（注册表 SteamExe 优先，退回 SteamPath\steam.exe）。</summary>
        public static string? GetSteamExePath()
        {
            try
            {
                object? value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamExe", null);
                if (value is string exe && exe.Length > 0 && File.Exists(exe))
                {
                    return exe;
                }
            }
            catch
            {
            }

            string? root = GetSteamPath();
            if (!string.IsNullOrWhiteSpace(root))
            {
                string candidate = Path.Combine(root, "steam.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>Steam 客户端是否已在运行。</summary>
        public static bool IsSteamRunning()
        {
            var list = new List<Process>();
            try
            {
                list.AddRange(Process.GetProcessesByName("steam"));
                foreach (Process p in list)
                {
                    try
                    {
                        if (!p.HasExited)
                        {
                            return true;
                        }
                    }
                    catch
                    {
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                foreach (Process p in list)
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }

        /// <summary>
        /// 确保 Steam 客户端在运行 —— 这是「从启动器启动的游玩被 Steam 记入时长」的前提。
        ///
        /// 实测结论：即使直接启动 BeamNG.drive.exe（不经 Steam 启动），只要 Steam 客户端在运行，
        /// Steam 依然会把这段时长记到账上（rtime_last_played 会刷新）；
        /// 而 Steam 没开时，这段时间 Steam 完全不知情。
        ///
        /// 拿不到 Steam 路径或拉起失败都返回 false —— 调用方必须放行游戏，
        /// 绝不能因为 Steam 的问题让人进不去。
        /// </summary>
        public static bool EnsureSteamRunning(int waitMs = 30000, Action<string>? log = null)
        {
            if (IsSteamRunning())
            {
                return true;
            }

            string? exe = GetSteamExePath();
            if (string.IsNullOrWhiteSpace(exe))
            {
                log?.Invoke("本机找不到 Steam 客户端（注册表 SteamExe / SteamPath 都没有）");
                return false;
            }

            try
            {
                log?.Invoke("Steam 未运行，正在拉起：" + exe);
                Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                log?.Invoke("拉起 Steam 失败：" + ex.Message);
                return false;
            }

            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < waitMs)
            {
                if (IsSteamRunning())
                {
                    // 进程起来 ≠ 客户端就绪：登录/加载还要一点时间，
                    // 太早启动游戏会以「Steam 未运行」的方式失败。
                    Thread.Sleep(4000);
                    return true;
                }
                Thread.Sleep(700);
            }
            log?.Invoke("等待 Steam 启动超时（" + (waitMs / 1000) + " 秒）");
            return false;
        }

        public static SteamUserInfo? DetectSignedInUser()
        {
            string? steamPath = GetSteamPath();
            if (string.IsNullOrWhiteSpace(steamPath))
            {
                return null;
            }
            string vdfPath = Path.Combine(steamPath, "config", "loginusers.vdf");
            if (!File.Exists(vdfPath))
            {
                return null;
            }
            string text;
            try
            {
                text = File.ReadAllText(vdfPath);
            }
            catch
            {
                return null;
            }

            SteamUserInfo? best = null;
            long bestMostRecent = 0;
            foreach (Match block in UserBlockRegex.Matches(text))
            {
                string id = block.Groups["id"].Value;
                string body = block.Groups["body"].Value;
                var user = new SteamUserInfo { SteamId64 = id };
                long mostRecent = 0;
                foreach (Match field in FieldRegex.Matches(body))
                {
                    string key = field.Groups["key"].Value;
                    string val = field.Groups["val"].Value.Trim();
                    switch (key)
                    {
                        case "AccountName": user.AccountName = val; break;
                        case "PersonaName": user.PersonaName = val; break;
                        case "Avatar": user.AvatarHash = val; break;
                        case "MostRecent": long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out mostRecent); break;
                        case "Timestamp": long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ts); user.Timestamp = ts; break;
                    }
                }
                if (string.IsNullOrWhiteSpace(user.AccountName) && string.IsNullOrWhiteSpace(user.PersonaName))
                {
                    continue;
                }
                if (best == null)
                {
                    best = user;
                    bestMostRecent = mostRecent;
                    continue;
                }
                bool better = mostRecent > bestMostRecent
                    || (mostRecent == bestMostRecent && user.Timestamp > best.Timestamp);
                if (better)
                {
                    best = user;
                    bestMostRecent = mostRecent;
                }
            }
            return best;
        }

        public static async Task<string?> DownloadAvatarToFileAsync(string avatarHash, string steamId64, CancellationToken ct = default)
        {
            string hash = (avatarHash ?? "").Trim();
            if (hash.Length == 0 || hash.Trim('0').Length == 0)
            {
                return null;
            }
            string[] urls =
            {
                $"https://avatars.steamstatic.com/{hash}_full.jpg",
                $"https://avatars.akamai.steamstatic.com/{hash}_full.jpg",
                $"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/{hash.Substring(0, Math.Min(2, hash.Length))}/{hash}_full.jpg",
            };
            foreach (string url in urls)
            {
                string? path = await DownloadAvatarFromUrlAsync(url, steamId64, ct).ConfigureAwait(false);
                if (path != null)
                {
                    return path;
                }
            }
            return null;
        }

        public static string AvatarCacheDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StartRide",
            "avatars");

        private static string DataRootDirectory => Path.GetDirectoryName(AvatarCacheDirectory) ?? "";

        private static readonly Lazy<string[]> AvatarSearchDirectories = new(() =>
        {
            var candidates = new List<string>
            {
                AvatarCacheDirectory,
                Path.Combine(DataRootDirectory, "app", "avatars"),
            };
            try
            {
                foreach (string directory in Directory.EnumerateDirectories(AppContext.BaseDirectory))
                {
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    {
                        candidates.Add(Path.Combine(directory, "avatars"));
                    }
                }
            }
            catch
            {
            }

            var unique = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string directory in candidates)
            {
                if (seen.Add(directory))
                {
                    unique.Add(directory);
                }
            }
            return unique.ToArray();
        });

        public static string? GetCachedAvatarPath(string steamId64)
        {
            string fileName = $"steam-{steamId64}.jpg";
            foreach (string directory in AvatarSearchDirectories.Value)
            {
                string path = Path.Combine(directory, fileName);
                try
                {
                    if (File.Exists(path) && new FileInfo(path).Length > 256)
                    {
                        return path;
                    }
                }
                catch
                {
                }
            }
            return null;
        }

        public static async Task<string?> DownloadAvatarFromUrlAsync(string url, string steamId64, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }
            try
            {
                using HttpResponseMessage resp = await Http.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    return null;
                }
                byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                if (bytes.Length < 256)
                {
                    return null;
                }
                string dir = AvatarCacheDirectory;
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"steam-{steamId64}.jpg");
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { }
                }
                File.WriteAllBytes(path, bytes);
                return path;
            }
            catch
            {
                return null;
            }
        }
    }
}
