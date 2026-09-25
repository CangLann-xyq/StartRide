using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace StartRide.Core
{
    /// <summary>本机 Steam 已登录用户（loginusers.vdf 里 MostRecent / Timestamp 最新的用户）。</summary>
    public sealed class SteamUserInfo
    {
        public string SteamId64 { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string PersonaName { get; set; } = "";
        public string AvatarHash { get; set; } = "";
        public long Timestamp { get; set; }

        /// <summary>优先展示 Steam 昵称（PersonaName），缺失时退回登录名 / SteamId。</summary>
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

    /// <summary>
    /// Steam 授权登录（本机免密检测）：
    /// 注册表 HKCU\Software\Valve\Steam 读 SteamPath → config/loginusers.vdf 取当前登录用户，
    /// 头像经 Steam CDN 下载到本地缓存（LauncherAccount.AvatarSource 用本地文件路径，离线可用）。
    /// 不做任何密码/OAuth 交互——"授权"即确认本机 Steam 客户端已有登录用户。
    /// </summary>
    public static class SteamLoginClient
    {
        // loginusers.vdf 用户块：  "7656119XXXXXXXXXX" { "AccountName" "x" ... }
        private static readonly Regex UserBlockRegex = new(
            "\"(?<id>\\d{17})\"\\s*\\{(?<body>[^{}]*)\\}",
            RegexOptions.Compiled);

        // 块内字段： "Key" "Value"
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

        /// <summary>从注册表读 Steam 安装目录（注册表值用正斜杠，直接返回原样）。</summary>
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
                // 注册表读取失败（未装 Steam 等）按未检测处理
            }
            return null;
        }

        /// <summary>检测本机 Steam 当前登录用户；没有 Steam 或没有登录用户时返回 null。</summary>
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
                // MostRecent=1 优先；其次 Timestamp 更新者优先
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

        /// <summary>
        /// 下载 Steam 头像到本地缓存（%APPDATA%\StartRidevatars\steam-<id>.jpg），返回本地绝对路径；失败返回 null。
        /// CDN 顺序很重要：国内只有 avatars.steamstatic.com 可达，
        /// 而 akamai.steamstatic.com / steamcdn-a.akamaihd.net 均不可达（实测 HTTP=000）。
        /// 全零 hash（未设置头像）直接跳过。
        /// </summary>
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

        /// <summary>
        /// 头像缓存目录：统一到 %APPDATA%\StartRide\avatars。
        /// 铁律：**不再写 EXE 旁的历史数据目录**——那会让头像路径随启动目录漂移，
        /// 且换目录启动就"头像丢了"。
        /// </summary>
        public static string AvatarCacheDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StartRide",
            "avatars");

        /// <summary>返回已缓存的本地头像路径；不存在/无效返回 null（避免重复下载）。</summary>
        public static string? GetCachedAvatarPath(string steamId64)
        {
            // 新位置优先，旧位置（EXE 旁的历史数据目录）兜底，保证历史账户头像不丢
            string[] candidates =
            {
                Path.Combine(AvatarCacheDirectory, $"steam-{steamId64}.jpg"),
                Path.Combine(AppContext.BaseDirectory, "StartRide", "avatars", $"steam-{steamId64}.jpg"),
            };
            foreach (string path in candidates)
            {
                try
                {
                    if (File.Exists(path) && new FileInfo(path).Length > 256)
                    {
                        return path;
                    }
                }
                catch
                {
                    // 缓存不可读按未缓存处理
                }
            }
            return null;
        }

        /// <summary>
        /// 下载指定头像 URL 到本地缓存（%APPDATA%\StartRide\avatars\steam-&lt;id&gt;.jpg），成功返回本地绝对路径。
        /// 缓存到本地后 AvatarSource 可离线使用；失败返回 null（调用方可退回远程 URL）。
        /// </summary>
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
                // EDR 会拦截对已存在文件的覆盖写 → 先删旧再写新
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
