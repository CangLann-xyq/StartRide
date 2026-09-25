using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace StartRide.Core
{
    /// <summary>BeamNG 官方资源库条目（www.beamng.com/resources 列表页解析结果）。</summary>
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

        /// <summary>
        /// 作者上传的模组图标（列表页 &lt;img src="data/resource_icons/N/ID.jpg"&gt;），已补成绝对地址。
        /// 列表页里每个条目自带这张图，不用额外请求。
        /// </summary>
        public string IconUrl { get; set; } = "";

        public string PageUrl => $"https://www.beamng.com/resources/{Slug}.{Id}/";
    }

    /// <summary>
    /// 模组详情页解析结果。**和下载直链共用同一次 HTTP 请求**（详情页本来就要拉一次才能拿直链），
    /// 所以「介绍 / 图标 / 精确下载量 / 文件大小」等于白送，不产生额外流量。
    /// </summary>
    public sealed class BeamNgResourceDetail
    {
        /// <summary>模组作者写的完整介绍（详情页正文，已去 HTML 标签）。</summary>
        public string Description { get; init; } = "";

        /// <summary>作者上传的图标（绝对地址）。</summary>
        public string IconUrl { get; init; } = "";

        /// <summary>该模组页面上的真实下载量（带千分位，如 "2,839,510"）。</summary>
        public string DownloadsText { get; init; } = "";

        /// <summary>版本号（详情页标题右侧的 span.muted）。</summary>
        public string Version { get; init; } = "";

        /// <summary>安装包体积文案（如 "237 MB .zip"）。</summary>
        public string FileSizeText { get; init; } = "";

        /// <summary>download?version=NNN 绝对直链（拿不到为 null）。</summary>
        public string? DownloadUrl { get; init; }
    }

    /// <summary>下载进度快照（Bytes 累计已下载字节，TotalBytes 可能为 null 表示总大小未知）。</summary>
    public readonly record struct BeamNgDownloadProgress(long Bytes, long? TotalBytes);

    /// <summary>
    /// BeamNG 官方模组仓库（www.beamng.com/resources）轻量客户端。
    /// 直接拉取列表页 HTML 解析条目，不内嵌网页；全库约 1574 页 / 15 万+ 条目，
    /// 支持按页区间并发拉取（由调用方分波加载实现"滚动到底自动续拉"）。
    /// 下载走详情页解析出的 download?version=NNN 直链（302 到 Cloudflare R2，
    /// 实测支持 Accept-Ranges/206）→ 大文件按 8 段并行分块下载，小文件单流。
    /// </summary>
    public static class BeamNgRepositoryClient
    {
        private const string BaseUrl = "https://www.beamng.com/resources/";

        // 列表拉取客户端（短超时）
        private static readonly HttpClient Http = CreateListHttp();

        // 下载客户端（流式长任务 → 不设总超时，仅连接超时）
        private static readonly HttpClient DownloadHttp = CreateDownloadHttp();

        private static HttpClient CreateListHttp()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false,
                UseProxy = false,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                // 首屏要并发拉多页；默认不限制，但显式给出更利于连接复用。
                MaxConnectionsPerServer = 16,
                // 站点走 Cloudflare，长连接复用能省掉 TLS 握手（实测首包 12.9s、复用后每页 ~0.7s）
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
            ApplyBrowserHeaders(client);
            return client;
        }

        private static HttpClient CreateDownloadHttp()
        {
            var handler = new SocketsHttpHandler
            {
                // 下载是原始 zip 字节流，不要自动解压（Range 场景也无 gzip）
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                UseProxy = false,
                ConnectTimeout = TimeSpan.FromSeconds(15),
                // R2 单连接也很快；多路复用由分段请求天然达成
                MaxConnectionsPerServer = 16,
            };
            // ResponseHeadersRead 流式下载不能受 15s 总超时影响
            var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            ApplyBrowserHeaders(client);
            return client;
        }

        private static void ApplyBrowserHeaders(HttpClient client)
        {
            // 必须带浏览器 UA，否则会被站点拒绝
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.6");
        }

        /// <summary>官方分类 slug → 中文名。</summary>
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

        /// <summary>中文名 → 分类 slug（"全部"/"其他" 返回 null 表示全站）。</summary>
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

        /// <summary>
        /// 拉取第 1 页并解析全库总页数（页码导航里 data-last="N"）。
        /// 返回首页条目 + 总页数（解析失败时给保守值 1）。
        /// 走同一套页面缓存 —— 预热/切换分类来回点都能直接命中。
        /// </summary>
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

        /// <summary>
        /// 并发拉取 [fromPage, toPage] 页（信号量限流 6 并发，单页失败重试 1 次），
        /// 返回按页序拼接、按 Id 全局去重的条目列表。解析在线程池完成，不占 UI 线程。
        /// </summary>
        public static async Task<List<BeamNgModInfo>> FetchPageRangeAsync(string? categorySlug, int fromPage, int toPage, CancellationToken ct)
        {
            if (toPage < fromPage)
            {
                return new List<BeamNgModInfo>();
            }

            var pages = new Dictionary<int, List<BeamNgModInfo>>();
            var seen = new HashSet<long>();
            var gate = new SemaphoreSlim(6);
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
                        lock (pages)
                        {
                            var merged = new List<BeamNgModInfo>(pageItems.Count);
                            foreach (BeamNgModInfo item in pageItems)
                            {
                                if (seen.Add(item.Id))
                                {
                                    merged.Add(item);
                                }
                            }
                            pages[p] = merged;
                        }
                    }
                    finally
                    {
                        gate.Release();
                    }
                }, ct));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            var result = new List<BeamNgModInfo>();
            foreach (KeyValuePair<int, List<BeamNgModInfo>> kv in pages.OrderBy(kv => kv.Key))
            {
                result.AddRange(kv.Value);
            }
            return result;
        }

        private static async Task<string> FetchPageWithRetryAsync(string? categorySlug, int page, CancellationToken ct)
        {
            string url = PageUrl(categorySlug, page);
            if (TryGetCachedPage(url, out string cached))
            {
                return cached;
            }
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    string html = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
                    PutCachedPage(url, html);
                    return html;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch when (attempt < 1)
                {
                    await Task.Delay(400, ct).ConfigureAwait(false);
                }
            }
        }

        // ── 页面缓存 ──────────────────────────────────────────────────────────
        // 实测：同一批页重复拉取没有任何缓存（5 页 4.5s 又跑一遍）。
        // 切换分类来回点、点「刷新」都会重复命中同一批 URL，缓存能省掉绝大部分流量。
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
                // 粗暴但够用：超限就清掉一半最老的
                foreach (KeyValuePair<string, PageCacheEntry> kv in PageCache
                             .OrderBy(kv => kv.Value.Utc).Take(PageCacheMaxEntries / 2).ToList())
                {
                    PageCache.TryRemove(kv.Key, out _);
                }
            }
            PageCache[url] = new PageCacheEntry(html, DateTime.UtcNow);
        }

        /// <summary>清空列表页缓存（「刷新」按钮用，保证拿到最新数据）。</summary>
        public static void InvalidatePageCache() => PageCache.Clear();

        /// <summary>
        /// 预热：提前把第 1 页拉下来并建立 TLS 连接。
        /// 首屏那一次请求实测要 12.9s（冷连接 + TLS 握手），而后续页只要 ~0.7s；
        /// 在用户还没点「在线仓库」之前先跑掉这 12.9s，点进去就是现成的。
        /// </summary>
        public static async Task WarmUpAsync(string? categorySlug, CancellationToken ct)
        {
            try
            {
                await FetchPageWithRetryAsync(categorySlug, 1, ct).ConfigureAwait(false);
            }
            catch
            {
                // 预热失败无所谓，正式加载会再试
            }
        }

        /// <summary>解析资源详情页，返回 download?version=NNN 绝对直链（未登录可下载）。</summary>
        public static async Task<string?> ResolveDownloadUrlAsync(string pageUrl, CancellationToken ct)
        {
            BeamNgResourceDetail? d = await FetchResourceDetailAsync(pageUrl, ct).ConfigureAwait(false);
            return d?.DownloadUrl;
        }

        /// <summary>
        /// 拉取并解析模组详情页 —— 一次请求同时拿到：完整介绍、作者上传的图标、
        /// 该模组页面上的真实下载量、版本号、安装包体积、下载直链。
        ///
        /// 之所以合成一个方法：下载前本来就必须拉一次详情页才能拿到 download?version=NNN 直链，
        /// 顺手把介绍/图标/下载量一起解析出来，等于零额外请求。
        /// </summary>
        public static async Task<BeamNgResourceDetail?> FetchResourceDetailAsync(string pageUrl, CancellationToken ct)
        {
            string html;
            try
            {
                html = await Http.GetStringAsync(pageUrl, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
            return ParseDetailPage(html);
        }

        private static BeamNgResourceDetail ParseDetailPage(string html)
        {
            // 图标：<div class="resourceImage"><img src="data/resource_icons/13/13061.jpg?…" class="resourceIcon" />
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

            // 标题行：<h1>Gavril Vertex NA2 <span class="muted">3.7 Rework</span></h1>
            string version = "";
            Match h1 = DetailTitleRegex.Match(html);
            if (h1.Success)
            {
                version = Clean(h1.Groups[1].Value);
            }

            // 下载按钮块：<a href="resources/xxx.13061/download?version=72848" class="inner">Download Now
            //              <small class="minorText">237 MB .zip</small></a>
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

            // 下载量：<dt>Downloads:</dt><dd>2,839,510</dd>（详情页是 pairsJustified 布局，dt 前有空白）
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

        /// <summary>
        /// 取模组作者写的介绍正文。详情页正文是第一条 update 里的
        /// &lt;blockquote class="ugc baseHtml messageText"&gt;；转成纯文本时会丢掉
        /// 视频 iframe / 剧透按钮 / 图片，只保留可读文字。
        /// </summary>
        private static string ExtractDescription(string html)
        {
            Match m = DescriptionRegex.Match(html);
            if (!m.Success)
            {
                // 退化：用 meta description（就是那句 tagline，聊胜于无）
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
            // 折叠空白：保留段落换行，行内多余空格压成一个
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

        /// <summary>相对地址 → 绝对地址（详情页里 href/src 都是相对 "resources/..."）。</summary>
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

        /// <summary>
        /// 下载文件到 targetPath：探测 Accept-Ranges 后大文件分 4-8 段并行（实测 R2 支持 206）；
        /// 小文件 / 不支持 Range / 分段失败时回退 1MB 缓冲单流。progress 回调可能来自后台线程。
        ///
        /// 实测（家里千兆宽带 → Cloudflare R2）：
        ///   单流 424 KB/s（237MB 要 9 分钟）· 4 段 4.0 MB/s · 8 段 3.3 MB/s
        /// → 多段并行是唯一有效手段，所以阈值从 16MB 降到 8MB，让中型模组也走分段。
        /// </summary>
        /// <param name="maxSegments">分段上限；&lt;=0 表示按体积自动（设置页的「下载线程数」传进来）。</param>
        public static async Task DownloadToFileAsync(string downloadUrl, string targetPath, Action<BeamNgDownloadProgress>? progress, CancellationToken ct, int maxSegments = 0)
        {
            long? total = null;

            // 探测：总大小 + 是否支持 Range
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
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        // 分段失败（网络波动/Range 被拒）→ 落回单流完整下载
                        TryDelete(targetPath);
                    }
                }
            }

            // 单流下载（回退路径）
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

        /// <summary>
        /// 收尾校验：① 文件长度必须等于服务器声明的长度；② ETag 若正好是 32 位十六进制
        /// （R2 单段对象的 ETag 就是整包 MD5）再核一次 MD5。
        /// 校验不通过就抛异常 —— 宁可这次下载判失败（上层会回退单流或报错），
        /// 也不能把「长度正确、内容坏了」的包留给 BeamNG 去加载。
        /// </summary>
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
                return; // 多段上传的 ETag 形如 "<md5>-N"，不含整包摘要 → 只能靠长度把关
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

        /// <summary>分段并行下载：probe 响应的流作为第 0 段数据源复用，其余段各自带 Range 请求。</summary>
        private static async Task DownloadSegmentedAsync(string downloadUrl, string targetPath, HttpResponseMessage probe, long total, Action<BeamNgDownloadProgress>? progress, CancellationToken ct, int maxSegments)
        {
            int segCount = total >= 64L * 1024 * 1024 ? 8 : total >= 24L * 1024 * 1024 ? 6 : 4;
            if (maxSegments > 0)
            {
                segCount = Math.Min(segCount, Math.Max(1, maxSegments));
            }
            // 每段至少 4MB，否则小文件分段反而更慢（并发握手开销）
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

            // ⚠️ 所有段共用**同一个 SafeFileHandle**，并且只用 RandomAccess.WriteAsync(handle, buf, offset) 写。
            //    绝不能每段各开一个 FileStream + Seek + 自带 64KB 缓冲：那样多段并发时
            //    各句柄的文件指针/缓冲区会互相踩，写出来的包长度完全正确、内容却是坏的。
            //    实测（322.5MB / 8 段）：39/446 个条目 CRC 坏，坏块严格贴合分段边界
            //    （第 0 段自 ~50% 起全坏、第 4 段尾部坏，其余段完好）；
            //    对同一字节区间做 HTTP Range 取远端比对，确认远端干净 → 就是本地写入错位。
            //    RandomAccess 每次写都带显式偏移、不经任何内部缓冲，天然对并发安全。
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
                            // 显式偏移写：不受别的段影响，也不需要 Seek
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
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch when (attempt < 1 && reused == null)
                {
                    // 单段失败重试一次
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

        private static async Task<HttpResponseMessage> SendDownloadAsync(string downloadUrl, (long Start, long End)? range, CancellationToken ct)
        {
            using HttpRequestMessage req = new(HttpMethod.Get, downloadUrl);
            if (range.HasValue)
            {
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(range.Value.Start, range.Value.End);
            }
            // await 必须在 using 作用域内完成：302→R2 重定向跟随期间不能提前释放 request
            return await DownloadHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
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

                // 作者上传的模组图标。条目里有两张 img（先图标、后作者头像），
                // 判据取 data/resource_icons/ 前缀，不会误抓到头像。
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

        // 页码导航里最大的 page=N → 全库总页数。
        // 注意 XF 模板里链接是 &amp;page=N（HTML 实体），必须容忍 amp; 前缀；
        // data-last="N" 是 pageNav 的总页数属性，最可靠。
        private static readonly Regex TotalPageRegex = new(
            @"[?&](?:amp;)?page=(\d+)",
            RegexOptions.Compiled);

        private static readonly Regex PageNavLastRegex = new(
            @"data-last=""(\d+)""",
            RegexOptions.Compiled);

        // 列表条目：<li class="resourceListItem ..." id="resource-38879"> ... </li>（条目内无嵌套 li）
        private static readonly Regex ListItemRegex = new(
            @"<li[^>]*id=""resource-(\d+)""[^>]*>(.*?)</li>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // 作者上传的模组图标：<img src="data/resource_icons/1/1362.jpg?1473003845" alt="" />
        // ⚠️ 必须限定 data/resource_icons/ 前缀：同一条目里紧跟其后的是作者头像
        //    （data/avatars/... 或 styles/uix/...），宽松的 <img> 匹配会抓错。
        private static readonly Regex IconRegex = new(
            @"<img[^>]*src=""(data/resource_icons/[^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 条目内资源链接（slug.数字），排除分类/作者/更新等链接由位置保证（首个即标题链接所在 slug）
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

        // 详情页下载按钮：<a href="resources/xxx.29318/download?version=71479" ...>
        private static readonly Regex DownloadLinkRegex = new(
            @"href=""(resources/[^""]+\.(\d+)/download\?version=\d+)""",
            RegexOptions.Compiled);

        // 详情页图标容器：<div class="resourceImage"> ... <img src="data/resource_icons/13/13061.jpg?…" />
        private static readonly Regex ResourceImageRegex = new(
            @"<div class=""resourceImage"">\s*<img[^>]*src=""([^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 详情页标题行：<h1>Gavril Vertex NA2 <span class="muted">3.7 Rework</span></h1>
        private static readonly Regex DetailTitleRegex = new(
            @"<h1>[^<]*<span class=""muted"">([^<]*)</span>",
            RegexOptions.Compiled);

        // 下载按钮里的体积：<small class="minorText">237 MB .zip</small>
        private static readonly Regex DownloadSizeRegex = new(
            @"<small class=""minorText"">([^<]*)</small>",
            RegexOptions.Compiled);

        // 详情页下载量。实际标记（dt 带 title 属性、文案是 "Total Downloads:"，直接匹配文本会漏）：
        //   <dl class="downloadCount"><dt title="By unique downloaders">Total Downloads:</dt>
        //       <dd>2,839,510</dd></dl>
        // → 只认 class="downloadCount" 这个容器最稳。
        private static readonly Regex DetailDownloadsRegex = new(
            @"<dl class=""downloadCount"">.*?<dd[^>]*>([^<]+)</dd>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 详情页正文：<blockquote class="ugc baseHtml messageText"> … </blockquote>（第一条 = 资源介绍）
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
