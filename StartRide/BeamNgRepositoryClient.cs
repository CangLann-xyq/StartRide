using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace StartRide.Core
{
    public sealed class BeamNgModInfo
    {
        public long Id { get; init; }
        public string Slug { get; init; } = "";
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string CategorySlug { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Version { get; set; } = "";
        public string RatingText { get; set; } = "";
        public string DownloadsText { get; set; } = "";
        public string UpdatedText { get; set; } = "";

        public string IconUrl { get; set; } = "";

        public string PageUrl => $"https://www.beamng.com/resources/{Slug}.{Id}/";
    }

    public sealed class BeamNgResourceDetail
    {
        public string Description { get; init; } = "";

        public string IconUrl { get; init; } = "";

        public string DownloadsText { get; init; } = "";

        public string Version { get; init; } = "";

        public string FileSizeText { get; init; } = "";

        public string? DownloadUrl { get; init; }
    }

    public readonly record struct BeamNgDownloadProgress(long Bytes, long? TotalBytes);

    public static class BeamNgRepositoryClient
    {
        private const string BaseUrl = "https://www.beamng.com/resources/";

        private const string RelayBase = "https://windseek.cloud/api/startride/repo";

        private static volatile bool directBeamNgReachable;

        private static IWebProxy? usableProxy;

        private static int channelProbeStarted;

        private static volatile HttpClient[]? probedListChannels;
        private static volatile HttpClient[]? probedDownloadChannels;

        private static bool DirectBeamNgReachable => directBeamNgReachable;

        private static readonly HttpClient RelayHttp = CreateRelayHttp();

        private static readonly HttpClient DirectListHttp = CreateListHttp(null, relay: false);
        private static readonly HttpClient DirectDownloadHttp = CreateDownloadHttp(null);
        private static readonly HttpClient RelayListHttp = CreateListHttp(null, relay: true);

        private static readonly HttpClient[] RelayOnlyListChannels = { RelayListHttp };

        private static readonly HttpClient[] DirectOnlyDownloadChannels = { DirectDownloadHttp };

        private static volatile int preferredListChannel;
        private static volatile int preferredDownloadChannel;

        private static HttpClient[] ListChannels => probedListChannels ?? RelayOnlyListChannels;

        private static HttpClient[] DownloadChannels => probedDownloadChannels ?? DirectOnlyDownloadChannels;

        private static void EnsureChannelProbeStarted()
        {
            if (Interlocked.CompareExchange(ref channelProbeStarted, 1, 0) != 0)
            {
                return;
            }
            _ = Task.Run(() =>
            {
                try
                {
                    directBeamNgReachable = TcpReachable("www.beamng.com", 443, 2500);
                }
                catch
                {
                    directBeamNgReachable = false;
                }
                try
                {
                    usableProxy = ResolveUsableProxy();
                }
                catch
                {
                    usableProxy = null;
                }
                try
                {
                    probedListChannels = BuildChannelSet(list: true, usableProxy, directBeamNgReachable);
                    probedDownloadChannels = BuildChannelSet(list: false, usableProxy, directBeamNgReachable);
                    preferredListChannel = 0;
                    preferredDownloadChannel = 0;
                }
                catch
                {
                }
            });
        }

        private sealed class RelayRewritingHandler : DelegatingHandler
        {
            public RelayRewritingHandler(HttpMessageHandler inner) : base(inner)
            {
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                if (request.RequestUri is { } u
                    && (u.Host.Equals("beamng.com", StringComparison.OrdinalIgnoreCase)
                        || u.Host.EndsWith(".beamng.com", StringComparison.OrdinalIgnoreCase)))
                {
                    request.RequestUri = new Uri(RelayBase + "?p=" + Uri.EscapeDataString(u.PathAndQuery));
                }
                return base.SendAsync(request, ct);
            }
        }

        private static IWebProxy? ResolveUsableProxy()
        {
            Uri? proxyUri = null;
            foreach (string name in new[] { "https_proxy", "HTTPS_PROXY", "http_proxy", "HTTP_PROXY", "all_proxy", "ALL_PROXY" })
            {
                string? v = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrWhiteSpace(v)
                    && Uri.TryCreate(v, UriKind.Absolute, out Uri? pu)
                    && pu.Port > 0)
                {
                    proxyUri = pu;
                    break;
                }
            }
            if (proxyUri != null && TcpReachable(proxyUri.Host, proxyUri.Port, 1500))
            {
                return new WebProxy(proxyUri);
            }

            try
            {
                IWebProxy dp = HttpClient.DefaultProxy;
                if (!dp.IsBypassed(new Uri(BaseUrl)))
                {
                    return dp;
                }
            }
            catch
            {
            }
            return null;
        }

        private static bool TcpReachable(string host, int port, int timeoutMs)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                if (!tcp.ConnectAsync(host, port).Wait(timeoutMs))
                {
                    return false;
                }
                return tcp.Connected;
            }
            catch
            {
                return false;
            }
        }

        private static HttpClient[] BuildChannelSet(bool list, IWebProxy? proxy, bool directReachable)
        {
            var channels = new List<HttpClient>(3);
            if (proxy != null)
            {
                channels.Add(list ? CreateListHttp(proxy, relay: false) : CreateDownloadHttp(proxy));
            }
            if (proxy == null || directReachable)
            {
                channels.Add(list ? DirectListHttp : DirectDownloadHttp);
            }
            if (list)
            {

                channels.Add(RelayListHttp);
            }
            return channels.ToArray();
        }

        private static HttpClient ListChannelFor(int attempt)
        {

            HttpClient[] ch = ListChannels;
            if (ch.Length <= 1)
            {
                return ch[0];
            }
            int start = preferredListChannel;
            if (start < 0 || start >= ch.Length)
            {
                start = 0;
            }
            return ch[(start + attempt) % ch.Length];
        }

        private static void MarkListChannelOk(int attempt)
        {
            HttpClient[] ch = ListChannels;
            if (ch.Length > 1)
            {
                int start = preferredListChannel;
                if (start < 0 || start >= ch.Length)
                {
                    start = 0;
                }
                preferredListChannel = (start + attempt) % ch.Length;
            }
        }

        private static HttpClient CreateListHttp(IWebProxy? proxy, bool relay)
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false,
                UseProxy = proxy != null,
                Proxy = proxy,

                ConnectTimeout = TimeSpan.FromSeconds(25),
                MaxConnectionsPerServer = 16,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            };
            var client = new HttpClient(relay ? new RelayRewritingHandler(handler) : handler)
            {
                Timeout = TimeSpan.FromSeconds(35),
            };
            ApplyBrowserHeaders(client);
            return client;
        }

        private static HttpClient CreateDownloadHttp(IWebProxy? proxy)
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                UseProxy = proxy != null,
                Proxy = proxy,
                ConnectTimeout = TimeSpan.FromSeconds(25),
                MaxConnectionsPerServer = 16,
            };
            var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            ApplyBrowserHeaders(client);
            return client;
        }

        private static async Task<string> ResolveDirectCdnUrlAsync(string url, CancellationToken ct)
        {
            EnsureChannelProbeStarted();
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? u))
            {
                return url;
            }
            if (!(u.Host.Equals("beamng.com", StringComparison.OrdinalIgnoreCase)
                  || u.Host.EndsWith(".beamng.com", StringComparison.OrdinalIgnoreCase)))
            {
                return url;
            }
            if (DirectBeamNgReachable)
            {
                return url;
            }
            try
            {
                string body = await RelayHttp
                    .GetStringAsync(RelayBase + "/resolve?p=" + Uri.EscapeDataString(u.PathAndQuery), ct)
                    .ConfigureAwait(false);
                using JsonDocument doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out JsonElement data)
                    && data.TryGetProperty("url", out JsonElement target))
                {
                    string? s = target.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        return s!;
                    }
                }
            }
            catch
            {
            }
            return url;
        }

        private static HttpClient CreateRelayHttp()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false,
                UseProxy = false,
                ConnectTimeout = TimeSpan.FromSeconds(15),
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
            ApplyBrowserHeaders(client);
            return client;
        }

        private static void ApplyBrowserHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.6");
        }

        public static string CategoryToChinese(string slug) => slug switch
        {
            "vehicles" => "车辆",
            "land" => "车辆",
            "terrains-levels-maps" => "地图",
            "skins" => "涂装",
            "scenarios" => "场景",
            "user-interface-apps" => "界面应用",
            "mods-of-mods" => "模组扩展",
            "sounds" => "音效",
            "license-plates" => "车牌",
            "automation" => "Automation",
            "track-builder" => "赛道生成",
            _ => "其他",
        };

        public static string? ChineseToCategorySlug(string chinese) => chinese switch
        {
            "车辆" => "vehicles",
            "地图" => "terrains-levels-maps",
            "涂装" => "skins",
            "场景" => "scenarios",
            "界面应用" => "user-interface-apps",
            "模组扩展" => "mods-of-mods",
            "音效" => "sounds",
            _ => null,
        };

        private static string PageUrl(string? categorySlug, int page)
        {
            return categorySlug == null
                ? $"{BaseUrl}?order=download_count&direction=desc&page={page}"
                : $"{BaseUrl}categories/{categorySlug}/?order=download_count&direction=desc&page={page}";
        }

        public static async Task<(List<BeamNgModInfo> Items, int TotalPages)> FetchFirstPageAsync(string? categorySlug, CancellationToken ct)
        {
            string html = await FetchPageWithRetryAsync(categorySlug, 1, ct).ConfigureAwait(false);
            var items = new List<BeamNgModInfo>();
            ParseListPage(html, items, new HashSet<long>());
            int total = 1;
            Match last = PageNavLastRegex.Match(html);
            if (last.Success && int.TryParse(last.Groups[1].Value, out int lastVal) && lastVal > 0 && lastVal < 100000)
            {
                total = lastVal;
            }
            else
            {
                foreach (Match m in TotalPageRegex.Matches(html))
                {
                    if (int.TryParse(m.Groups[1].Value, out int v) && v > total && v < 100000)
                    {
                        total = v;
                    }
                }
            }
            return (items, total);
        }

        public sealed class ListPageBatch
        {
            public List<BeamNgModInfo> Items { get; } = new();

            public int LastNonEmptyPage { get; set; }

            public int FirstEmptyPage { get; set; }

            public List<int> FailedPages { get; } = new();

            public int RequestedFrom { get; set; }

            public int RequestedTo { get; set; }

            public bool ReachedEnd => FirstEmptyPage > 0 && FailedPages.Count == 0;
        }

        private const int ListPageParallelism = 4;

        public static async Task<ListPageBatch> FetchPageRangeAsync(string? categorySlug, int fromPage, int toPage, CancellationToken ct)
        {
            var batch = new ListPageBatch { RequestedFrom = fromPage, RequestedTo = toPage };
            if (toPage < fromPage)
            {
                return batch;
            }

            var perPage = new Dictionary<int, List<BeamNgModInfo>>();
            var emptyPages = new List<int>();
            var failedPages = new List<int>();
            var seen = new HashSet<long>();
            var gate = new SemaphoreSlim(ListPageParallelism);
            var tasks = new List<Task>(toPage - fromPage + 1);

            for (int page = fromPage; page <= toPage; page++)
            {
                int p = page;
                tasks.Add(Task.Run(async () =>
                {
                    await gate.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        string html = await FetchPageWithRetryAsync(categorySlug, p, ct).ConfigureAwait(false);
                        var pageItems = new List<BeamNgModInfo>();
                        ParseListPage(html, pageItems, new HashSet<long>());
                        lock (perPage)
                        {
                            if (pageItems.Count == 0)
                            {
                                emptyPages.Add(p);
                                return;
                            }
                            var merged = new List<BeamNgModInfo>(pageItems.Count);
                            foreach (BeamNgModInfo item in pageItems)
                            {
                                if (seen.Add(item.Id))
                                {
                                    merged.Add(item);
                                }
                            }
                            perPage[p] = merged;
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {

                        lock (perPage)
                        {
                            failedPages.Add(p);
                        }
                    }
                    finally
                    {
                        gate.Release();
                    }
                }, ct));
            }

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
            }

            foreach (KeyValuePair<int, List<BeamNgModInfo>> kv in perPage.OrderBy(kv => kv.Key))
            {
                batch.Items.AddRange(kv.Value);
            }
            if (perPage.Count > 0)
            {
                batch.LastNonEmptyPage = perPage.Keys.Max();
            }
            if (emptyPages.Count > 0)
            {
                batch.FirstEmptyPage = emptyPages.Min();
            }
            failedPages.Sort();
            batch.FailedPages.AddRange(failedPages);
            return batch;
        }

        private static async Task<string> FetchPageWithRetryAsync(string? categorySlug, int page, CancellationToken ct)
        {
            EnsureChannelProbeStarted();
            string url = PageUrl(categorySlug, page);
            if (TryGetCachedPage(url, out string cached))
            {
                return cached;
            }
            for (int attempt = 0; ; attempt++)
            {
                HttpClient http = ListChannelFor(attempt);
                try
                {
                    string html = await http.GetStringAsync(url, ct).ConfigureAwait(false);
                    MarkListChannelOk(attempt);
                    PutCachedPage(url, html);
                    return html;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch when (attempt < ListChannels.Length)
                {

                    await Task.Delay(400 * (attempt + 1), ct).ConfigureAwait(false);
                }
            }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PageCacheEntry> PageCache = new();
        private static readonly TimeSpan PageCacheTtl = TimeSpan.FromMinutes(10);
        private const int PageCacheMaxEntries = 400;

        private readonly record struct PageCacheEntry(string Html, DateTime Utc);

        private static bool TryGetCachedPage(string url, out string html)
        {
            html = "";
            if (!PageCache.TryGetValue(url, out PageCacheEntry e))
            {
                return false;
            }
            if (DateTime.UtcNow - e.Utc > PageCacheTtl)
            {
                PageCache.TryRemove(url, out _);
                return false;
            }
            html = e.Html;
            return true;
        }

        private static void PutCachedPage(string url, string html)
        {
            if (PageCache.Count >= PageCacheMaxEntries)
            {
                foreach (KeyValuePair<string, PageCacheEntry> kv in PageCache
                             .OrderBy(kv => kv.Value.Utc).Take(PageCacheMaxEntries / 2).ToList())
                {
                    PageCache.TryRemove(kv.Key, out _);
                }
            }
            PageCache[url] = new PageCacheEntry(html, DateTime.UtcNow);
        }

        public static void InvalidatePageCache() => PageCache.Clear();

        public static async Task WarmUpAsync(string? categorySlug, CancellationToken ct)
        {
            try
            {
                await FetchPageWithRetryAsync(categorySlug, 1, ct).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        public static async Task<string?> ResolveDownloadUrlAsync(string pageUrl, CancellationToken ct)
        {
            BeamNgResourceDetail? d = await FetchResourceDetailAsync(pageUrl, ct).ConfigureAwait(false);
            return d?.DownloadUrl;
        }

        public static async Task<BeamNgResourceDetail?> FetchResourceDetailAsync(string pageUrl, CancellationToken ct)
        {
            EnsureChannelProbeStarted();
            for (int attempt = 0; ; attempt++)
            {
                HttpClient http = ListChannelFor(attempt);
                try
                {
                    string html = await http.GetStringAsync(pageUrl, ct).ConfigureAwait(false);
                    MarkListChannelOk(attempt);
                    return ParseDetailPage(html);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch when (attempt < ListChannels.Length)
                {
                    await Task.Delay(300 * (attempt + 1), ct).ConfigureAwait(false);
                }
                catch
                {

                    return null;
                }
            }
        }

        private static BeamNgResourceDetail ParseDetailPage(string html)
        {
            string icon = "";
            Match m = ResourceImageRegex.Match(html);
            if (m.Success)
            {
                icon = Absolutize(m.Groups[1].Value);
            }
            if (icon.Length == 0)
            {
                Match any = IconRegex.Match(html);
                if (any.Success)
                {
                    icon = Absolutize(any.Groups[1].Value);
                }
            }

            string version = "";
            Match h1 = DetailTitleRegex.Match(html);
            if (h1.Success)
            {
                version = Clean(h1.Groups[1].Value);
            }

            string downloadUrl = "";
            string fileSize = "";
            Match dl = DownloadLinkRegex.Match(html);
            if (dl.Success)
            {
                downloadUrl = Absolutize(dl.Groups[1].Value);
                Match size = DownloadSizeRegex.Match(html, dl.Index);
                if (size.Success)
                {
                    fileSize = Clean(size.Groups[1].Value);
                }
            }

            string downloads = "";
            Match dv = DetailDownloadsRegex.Match(html);
            if (dv.Success)
            {
                downloads = Clean(dv.Groups[1].Value);
            }

            return new BeamNgResourceDetail
            {
                Description = ExtractDescription(html),
                IconUrl = icon,
                DownloadsText = downloads,
                Version = version,
                FileSizeText = fileSize,
                DownloadUrl = downloadUrl.Length == 0 ? null : downloadUrl,
            };
        }

        private static string ExtractDescription(string html)
        {
            Match m = DescriptionRegex.Match(html);
            if (!m.Success)
            {
                Match meta = MetaDescriptionRegex.Match(html);
                return meta.Success ? Clean(meta.Groups[1].Value) : "";
            }

            string body = m.Groups[1].Value;
            body = IframeRegex.Replace(body, " ");
            body = ScriptStyleRegex.Replace(body, " ");
            body = SpoilerButtonRegex.Replace(body, " ");
            body = LineBreakRegex.Replace(body, "\n");
            body = TagRegex.Replace(body, " ");
            body = WebUtility.HtmlDecode(body);
            var lines = body.Split('\n')
                .Select(l => Regex.Replace(l, "[ \t\u00a0]+", " ").Trim())
                .Where(l => l.Length > 0);
            string text = string.Join("\n", lines).Trim();
            const int MaxLen = 1200;
            if (text.Length > MaxLen)
            {
                text = text.Substring(0, MaxLen).TrimEnd() + " …";
            }
            return text;
        }

        private static string Absolutize(string href)
        {
            href = WebUtility.HtmlDecode(href).Trim();
            if (href.Length == 0)
            {
                return "";
            }
            return href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : "https://www.beamng.com/" + href.TrimStart('/');
        }

        public static async Task DownloadToFileAsync(string downloadUrl, string targetPath, Action<BeamNgDownloadProgress>? progress, CancellationToken ct, int maxSegments = 0)
        {
            EnsureChannelProbeStarted();

            downloadUrl = await ResolveDirectCdnUrlAsync(downloadUrl, ct).ConfigureAwait(false);

            long? total = null;

            using (HttpResponseMessage probe = await SendDownloadAsync(downloadUrl, null, ct).ConfigureAwait(false))
            {
                probe.EnsureSuccessStatusCode();
                total = probe.Content.Headers.ContentLength;
                bool acceptRanges = probe.Headers.AcceptRanges.Contains("bytes") || probe.StatusCode == HttpStatusCode.PartialContent;
                if (acceptRanges && total.HasValue && total.Value >= 8L * 1024 * 1024)
                {
                    long reportedTotal = total.Value;
                    try
                    {
                        await DownloadSegmentedAsync(downloadUrl, targetPath, probe, reportedTotal, progress, ct, maxSegments).ConfigureAwait(false);
                        await VerifyDownloadedAsync(targetPath, reportedTotal, probe.Headers.ETag?.Tag, ct).ConfigureAwait(false);
                        return;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        TryDelete(targetPath);
                    }
                }
            }

            string? fallbackEtag;
            using (HttpResponseMessage resp = await SendDownloadAsync(downloadUrl, null, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                total = resp.Content.Headers.ContentLength;
                fallbackEtag = resp.Headers.ETag?.Tag;
                await using Stream src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using FileStream dst = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true);
                byte[] buffer = new byte[1024 * 1024];
                long done = 0;
                int n;
                while ((n = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    done += n;
                    progress?.Invoke(new BeamNgDownloadProgress(done, total));
                }
            }
            await VerifyDownloadedAsync(targetPath, total, fallbackEtag, ct).ConfigureAwait(false);
        }

        private static async Task VerifyDownloadedAsync(string path, long? total, string? etag, CancellationToken ct)
        {
            long written = 0;
            try
            {
                written = new FileInfo(path).Length;
            }
            catch
            {
            }
            if (total.HasValue && written != total.Value)
            {
                throw new IOException($"下载长度不符：{written}/{total.Value} 字节");
            }

            string tag = (etag ?? string.Empty).Trim().Trim('"');
            if (tag.Length != 32 || !tag.All(Uri.IsHexDigit))
            {
                return;
            }
            using var md5 = System.Security.Cryptography.MD5.Create();
            await using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);
            byte[] hash = await md5.ComputeHashAsync(fs, ct).ConfigureAwait(false);
            string hex = Convert.ToHexString(hash).ToLowerInvariant();
            if (!string.Equals(hex, tag, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"下载校验失败：本地 MD5 {hex} ≠ 服务器 {tag}");
            }
        }

        private static async Task DownloadSegmentedAsync(string downloadUrl, string targetPath, HttpResponseMessage probe, long total, Action<BeamNgDownloadProgress>? progress, CancellationToken ct, int maxSegments)
        {
            int segCount = total >= 64L * 1024 * 1024 ? 8 : total >= 24L * 1024 * 1024 ? 6 : 4;
            if (maxSegments > 0)
            {
                segCount = Math.Min(segCount, Math.Max(1, maxSegments));
            }
            segCount = (int)Math.Min(segCount, Math.Max(1, total / (4L * 1024 * 1024)));
            long segLen = total / segCount;
            if (segLen <= 0)
            {
                throw new IOException("文件过小，无需分段");
            }

            long[] sharedDone = new long[1];
            void Report()
            {
                try
                {
                    progress?.Invoke(new BeamNgDownloadProgress(Interlocked.Read(ref sharedDone[0]), total));
                }
                catch
                {
                }
            }

            using (SafeFileHandle handle = File.OpenHandle(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.Asynchronous))
            {
                RandomAccess.SetLength(handle, total);

                Stream firstStream = await probe.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                var tasks = new List<Task>(segCount);
                for (int i = 0; i < segCount; i++)
                {
                    long start = i * segLen;
                    long end = (i == segCount - 1) ? total - 1 : start + segLen - 1;
                    Stream? reuse = (i == 0) ? firstStream : null;
                    tasks.Add(Task.Run(() => CopySegmentAsync(downloadUrl, handle, start, end, reuse, sharedDone, Report, ct), ct));
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        private static async Task CopySegmentAsync(string downloadUrl, SafeFileHandle handle, long start, long end, Stream? reused, long[] sharedDone, Action report, CancellationToken ct)
        {
            long segLen = end - start + 1;
            for (int attempt = 0; ; attempt++)
            {
                HttpResponseMessage? resp = null;
                Stream? src = null;
                try
                {
                    if (reused != null)
                    {
                        src = reused;
                    }
                    else
                    {
                        resp = await SendDownloadAsync(downloadUrl, (start, end), ct).ConfigureAwait(false);
                        resp.EnsureSuccessStatusCode();
                        src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    }

                    await using (src)
                    {
                        byte[] buffer = new byte[1024 * 1024];
                        long pos = 0;
                        int n;
                        while (pos < segLen && (n = await src.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, segLen - pos)), ct).ConfigureAwait(false)) > 0)
                        {
                            await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, n), start + pos, ct).ConfigureAwait(false);
                            pos += n;
                            Interlocked.Add(ref sharedDone[0], n);
                            report();
                        }
                        if (pos < segLen)
                        {
                            throw new IOException($"分段数据不完整：{pos}/{segLen} 字节");
                        }
                    }
                    return;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch when (attempt < 1 && reused == null)
                {
                    await Task.Delay(300, ct).ConfigureAwait(false);
                }
                finally
                {
                    resp?.Dispose();
                }
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static async Task<HttpResponseMessage> SendDownloadAsync(string downloadUrl, (long Start, long End)? range, CancellationToken ct, int channelAttempt = 0)
        {
            using HttpRequestMessage req = new(HttpMethod.Get, downloadUrl);
            if (range.HasValue)
            {
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(range.Value.Start, range.Value.End);
            }
            HttpClient[] ch = DownloadChannels;
            int start = preferredDownloadChannel;
            if (start < 0 || start >= ch.Length)
            {
                start = 0;
            }
            HttpClient http = ch.Length == 1 ? ch[0] : ch[(start + channelAttempt) % ch.Length];
            HttpResponseMessage resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (ch.Length > 1)
            {
                preferredDownloadChannel = (start + channelAttempt) % ch.Length;
            }
            return resp;
        }

        private static void ParseListPage(string html, List<BeamNgModInfo> sink, HashSet<long> seen)
        {
            foreach (Match li in ListItemRegex.Matches(html))
            {
                if (!long.TryParse(li.Groups[1].Value, out long id) || !seen.Add(id))
                {
                    continue;
                }
                string block = li.Groups[2].Value;

                Match link = ResourceLinkRegex.Match(block);
                if (!link.Success)
                {
                    continue;
                }

                var info = new BeamNgModInfo
                {
                    Id = id,
                    Slug = link.Groups[1].Value,
                };

                Match title = TitleRegex.Match(block);
                info.Name = title.Success ? Clean(title.Groups[1].Value) : info.Slug;

                Match version = VersionRegex.Match(block);
                info.Version = version.Success ? version.Groups[1].Value.Trim() : "";

                Match author = AuthorRegex.Match(block);
                info.Author = author.Success ? Clean(author.Groups[1].Value) : "";

                Match category = CategoryRegex.Match(block);
                if (category.Success)
                {
                    info.CategorySlug = category.Groups[1].Value;
                    info.CategoryName = CategoryToChinese(info.CategorySlug);
                }
                else
                {
                    info.CategoryName = "其他";
                }

                Match tag = TagLineRegex.Match(block);
                info.Description = tag.Success ? Clean(tag.Groups[1].Value) : "";

                Match icon = IconRegex.Match(block);
                info.IconUrl = icon.Success ? Absolutize(icon.Groups[1].Value) : "";

                Match rating = RatingRegex.Match(block);
                info.RatingText = rating.Success ? double.TryParse(rating.Groups[1].Value, out double r)
                    ? r.ToString("0.0")
                    : rating.Groups[1].Value : "";

                Match downloads = DownloadsRegex.Match(block);
                info.DownloadsText = downloads.Success ? downloads.Groups[1].Value.Trim() : "";

                Match updated = UpdatedRegex.Match(block);
                info.UpdatedText = updated.Success ? updated.Groups[1].Value.Trim() : "";

                sink.Add(info);
            }
        }

        private static string Clean(string htmlText)
        {
            return WebUtility.HtmlDecode(htmlText).Trim();
        }

        private static readonly Regex TotalPageRegex = new(
            @"[?&](?:amp;)?page=(\d+)",
            RegexOptions.Compiled);

        private static readonly Regex PageNavLastRegex = new(
            @"data-last=""(\d+)""",
            RegexOptions.Compiled);

        private static readonly Regex ListItemRegex = new(
            @"<li[^>]*id=""resource-(\d+)""[^>]*>(.*?)</li>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex IconRegex = new(
            @"<img[^>]*src=""(data/resource_icons/[^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ResourceLinkRegex = new(
            @"href=""resources/([a-z0-9\-\.]+)\.\d+/""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TitleRegex = new(
            @"<h3 class=""title"">.*?<a[^>]*href=""resources/[^""]+"">([^<]+)</a>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex VersionRegex = new(
            @"<span class=""version"">([^<]*)</span>",
            RegexOptions.Compiled);

        private static readonly Regex AuthorRegex = new(
            @"href=""resources/authors/[^""]*"">([^<]+)</a>",
            RegexOptions.Compiled);

        private static readonly Regex CategoryRegex = new(
            @"href=""resources/categories/([a-z\-]+)\.\d+/""[^>]*>([^<]+)<",
            RegexOptions.Compiled);

        private static readonly Regex TagLineRegex = new(
            @"<div class=""tagLine"">\s*(.*?)\s*</div>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex RatingRegex = new(
            @"<span class=""ratings"" title=""([^""]+)"">",
            RegexOptions.Compiled);

        private static readonly Regex DownloadsRegex = new(
            @"<dt>Downloads:</dt>\s*<dd>([^<]+)</dd>",
            RegexOptions.Compiled);

        private static readonly Regex UpdatedRegex = new(
            @"data-datestring=""([^""]+)""",
            RegexOptions.Compiled);

        private static readonly Regex DownloadLinkRegex = new(
            @"href=""(resources/[^""]+\.(\d+)/download\?version=\d+)""",
            RegexOptions.Compiled);

        private static readonly Regex ResourceImageRegex = new(
            @"<div class=""resourceImage"">\s*<img[^>]*src=""([^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex DetailTitleRegex = new(
            @"<h1>[^<]*<span class=""muted"">([^<]*)</span>",
            RegexOptions.Compiled);

        private static readonly Regex DownloadSizeRegex = new(
            @"<small class=""minorText"">([^<]*)</small>",
            RegexOptions.Compiled);

        private static readonly Regex DetailDownloadsRegex = new(
            @"<dl class=""downloadCount"">.*?<dd[^>]*>([^<]+)</dd>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex DescriptionRegex = new(
            @"<blockquote class=""ugc baseHtml messageText"">(.*?)</blockquote>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex MetaDescriptionRegex = new(
            @"<meta name=""description"" content=""([^""]*)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex IframeRegex = new(
            @"<iframe\b.*?</iframe>|<iframe\b[^>]*/?>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex ScriptStyleRegex = new(
            @"<(script|style)\b.*?</\1>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex SpoilerButtonRegex = new(
            @"<button\b.*?</button>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex LineBreakRegex = new(
            @"<br\s*/?>|</p>|</div>|</li>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TagRegex = new(
            @"<[^>]+>",
            RegexOptions.Compiled);
    }
}
