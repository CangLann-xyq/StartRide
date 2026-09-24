using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.Core
{
    /// <summary>
    /// StartRide 自己的启动器更新检查，替换掉反编译版里那段硬编码指向第三方仓库的更新实现。
    ///
    /// 以前"检查更新"请求的是那个项目的清单（日志里 Source=gitee 就是这么来的）——
    /// 既是别人的仓库，也永远不可能有 StartRide 的版本。
    ///
    /// 现在的行为：
    ///   · 依次尝试 SiteLinks.UpdateManifestCandidates(channel) 里的地址（自有域名 → GitHub raw）
    ///   · 第一个能取到并解析成功的清单生效；全部失败返回 Failed，界面按原逻辑提示"检查更新失败"
    ///   · 版本比较优先用 versionCode，缺失时退化为 2.6.0 这种点分版本号的语义比较
    ///
    /// 清单格式（update/launcher-release.json）：
    /// {
    ///   "version": "2.6.1", "displayVersion": "2.6.1", "versionCode": 20601,
    ///   "releasePageUrl": "...", "downloadUrl": "...", "downloadFileName": "...",
    ///   "changelog": "...", "assetKind": "ZipPackage", "sizeBytes": 0,
    ///   "sha256": "", "isMandatory": false, "minSupportedVersionCode": 0,
    ///   "publishedAt": "2026-09-22T00:00:00Z",
    ///   "downloadUrls": [ { "name": "GitHub", "url": "...", "priority": 0 } ]
    /// }
    /// </summary>
    public sealed class StartRideLauncherUpdateService : ILauncherUpdateService, IDisposable
    {
        private static readonly HttpClient Http = CreateClient();

        private readonly ILogger<StartRideLauncherUpdateService> _logger;

        public StartRideLauncherUpdateService(ILogger<StartRideLauncherUpdateService>? logger = null)
        {
            _logger = logger ?? NullLogger<StartRideLauncherUpdateService>.Instance;
        }

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            try
            {
                c.DefaultRequestHeaders.UserAgent.ParseAdd("StartRide-Launcher/" + BuildInfo.Version);
                c.DefaultRequestHeaders.CacheControl =
                    new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            }
            catch { }
            return c;
        }

        public async Task<LauncherUpdateCheckResult> CheckForUpdatesAsync(
            string currentVersion, LauncherUpdateChannel channel, CancellationToken cancellationToken)
        {
            string ch = channel == LauncherUpdateChannel.Beta ? "beta" : "release";
            var candidates = SiteLinks.UpdateManifestCandidates(ch);
            var errors = new List<string>();

            foreach (string url in candidates)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(12));

                    using HttpResponseMessage resp =
                        await Http.GetAsync(url, HttpCompletionOption.ResponseContentRead, cts.Token)
                                  .ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                    {
                        errors.Add(url + " -> HTTP " + (int)resp.StatusCode);
                        continue;
                    }

                    string json = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    var info = ParseManifest(json, ch);
                    if (info == null)
                    {
                        errors.Add(url + " -> 清单为空或格式不正确");
                        continue;
                    }

                    _logger.LogInformation(
                        "StartRide update manifest loaded. Source={Source} RemoteVersion={Remote} RemoteCode={Code}",
                        url, info.DisplayVersion, info.VersionCode);

                    if (IsNewer(info, currentVersion))
                    {
                        _logger.LogInformation(
                            "StartRide update available. Current={Current} Remote={Remote}", currentVersion, info.DisplayVersion);
                        return LauncherUpdateCheckResult.Available(currentVersion, info);
                    }

                    return LauncherUpdateCheckResult.Latest(currentVersion);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors.Add(url + " -> " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            string joined = string.Join(" | ", errors);
            _logger.LogWarning("StartRide update check failed for every source. Current={Current} Errors={Errors}",
                currentVersion, joined);
            return LauncherUpdateCheckResult.Failed(currentVersion,
                "所有更新源都不可用：" + (joined.Length > 300 ? joined.Substring(0, 300) : joined));
        }

        // ------------------------------------------------------------------ 解析

        internal static LauncherUpdateInfo ParseManifest(string json, string channel)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            using var doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            // 兼容 { "release": {...}, "beta": {...} } 这种一文件多通道的写法
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty(channel, out JsonElement chNode) &&
                chNode.ValueKind == JsonValueKind.Object)
            {
                root = chNode;
            }

            if (root.ValueKind != JsonValueKind.Object) return null;

            string version = Str(root, "version");
            if (string.IsNullOrWhiteSpace(version)) return null;

            string downloadUrl = Str(root, "downloadUrl");
            var downloadUrls = new List<LauncherUpdateDownloadUrl>();
            if (root.TryGetProperty("downloadUrls", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement e in arr.EnumerateArray())
                {
                    string u = e.ValueKind == JsonValueKind.String ? e.GetString() : Str(e, "url");
                    if (string.IsNullOrWhiteSpace(u)) continue;
                    string n = e.ValueKind == JsonValueKind.Object ? Str(e, "name") : null;
                    int prio = e.ValueKind == JsonValueKind.Object ? Int(e, "priority", downloadUrls.Count) : downloadUrls.Count;
                    downloadUrls.Add(new LauncherUpdateDownloadUrl(
                        string.IsNullOrWhiteSpace(n) ? "源 " + (downloadUrls.Count + 1) : n, u, prio));
                }
            }

            // 清单里没给 downloadUrl 但有 downloadUrls 时，用优先级最高的那个顶上
            if (string.IsNullOrWhiteSpace(downloadUrl) && downloadUrls.Count > 0)
            {
                downloadUrls.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                downloadUrl = downloadUrls[0].Url;
            }

            DateTimeOffset? published = null;
            string pubRaw = Str(root, "publishedAt");
            if (!string.IsNullOrWhiteSpace(pubRaw) &&
                DateTimeOffset.TryParse(pubRaw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dto))
            {
                published = dto;
            }

            return new LauncherUpdateInfo(
                Version: version,
                DisplayVersion: Fallback(Str(root, "displayVersion"), version),
                ReleasePageUrl: Fallback(Str(root, "releasePageUrl"), SiteLinks.GitHubReleases),
                DownloadUrl: downloadUrl ?? string.Empty,
                Changelog: Str(root, "changelog") ?? string.Empty,
                DownloadFileName: Fallback(Str(root, "downloadFileName"),
                    "StartRide-" + version + "-win-x64.zip"),
                AssetKind: ParseAssetKind(Str(root, "assetKind")),
                SizeBytes: Long(root, "sizeBytes", 0L),
                Sha256: Str(root, "sha256") ?? string.Empty,
                VersionCode: Int(root, "versionCode", VersionCodeFrom(version)),
                IsMandatory: Bool(root, "isMandatory", false),
                MinSupportedVersionCode: Int(root, "minSupportedVersionCode", 0),
                PublishedAt: published,
                DownloadUrls: downloadUrls);
        }

        internal static LauncherUpdateAssetKind ParseAssetKind(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return LauncherUpdateAssetKind.ZipPackage;
            return raw.Trim().ToLowerInvariant() switch
            {
                "none" => LauncherUpdateAssetKind.None,
                "windowsx64executable" or "exe" or "windows-x64-exe" => LauncherUpdateAssetKind.WindowsX64Executable,
                "otherexecutable" or "other-exe" => LauncherUpdateAssetKind.OtherExecutable,
                "zippackage" or "zip" => LauncherUpdateAssetKind.ZipPackage,
                "installer" or "msi" => LauncherUpdateAssetKind.Installer,
                "releasepage" or "page" => LauncherUpdateAssetKind.ReleasePage,
                _ => LauncherUpdateAssetKind.ZipPackage,
            };
        }

        /// <summary>2.6.0 → 20600，用来和清单里的 versionCode 对齐。</summary>
        internal static int VersionCodeFrom(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return 0;
            string core = version.TrimStart('v', 'V');
            int cut = core.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0) core = core.Substring(0, cut);

            string[] parts = core.Split('.');
            int[] n = { 0, 0, 0 };
            for (int i = 0; i < parts.Length && i < 3; i++)
            {
                int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i]);
            }
            return n[0] * 10000 + n[1] * 100 + n[2];
        }

        /// <summary>远端是否比当前新：先比 versionCode，再退化到点分版本号。</summary>
        internal static bool IsNewer(LauncherUpdateInfo remote, string currentVersion)
        {
            if (remote == null) return false;
            int currentCode = VersionCodeFrom(currentVersion);
            if (remote.VersionCode > 0 && currentCode > 0 && remote.VersionCode != currentCode)
            {
                return remote.VersionCode > currentCode;
            }
            return CompareVersions(remote.Version, currentVersion) > 0;
        }

        private static int CompareVersions(string a, string b)
        {
            static int[] Parts(string v)
            {
                if (string.IsNullOrWhiteSpace(v)) return new[] { 0, 0, 0 };
                string core = v.Trim().TrimStart('v', 'V');
                int cut = core.IndexOfAny(new[] { '-', '+' });
                if (cut >= 0) core = core.Substring(0, cut);
                string[] ps = core.Split('.');
                var r = new int[Math.Max(3, ps.Length)];
                for (int i = 0; i < ps.Length; i++)
                {
                    int.TryParse(ps[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out r[i]);
                }
                return r;
            }

            int[] ra = Parts(a), rb = Parts(b);
            int len = Math.Max(ra.Length, rb.Length);
            for (int i = 0; i < len; i++)
            {
                int x = i < ra.Length ? ra[i] : 0;
                int y = i < rb.Length ? rb[i] : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }

        // ------------------------------------------------------------------ JSON 取值helper

        private static string Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

        private static int Int(JsonElement e, string name, int fallback) =>
            e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number &&
            v.TryGetInt32(out int r) ? r : fallback;

        private static long Long(JsonElement e, string name, long fallback) =>
            e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number &&
            v.TryGetInt64(out long r) ? r : fallback;

        private static bool Bool(JsonElement e, string name, bool fallback) =>
            e.TryGetProperty(name, out JsonElement v) &&
            (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                ? v.GetBoolean() : fallback;

        private static string Fallback(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value;

        public void Dispose() { }
    }
}
