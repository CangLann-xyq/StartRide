using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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

        public string PageUrl => $"https://www.beamng.com/resources/{Slug}.{Id}/";
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
        /// 拉取第 1 页并解析全库总页数（页码导航里最大的 page=N）。
        /// 返回首页条目 + 总页数（解析失败时给保守值 1）。
        /// </summary>
        public static async Task<(List<BeamNgModInfo> Items, int TotalPages)> FetchFirstPageAsync(string? categorySlug, CancellationToken ct)
        {
            string html = await Http.GetStringAsync(PageUrl(categorySlug, 1), ct).ConfigureAwait(false);
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
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return await Http.GetStringAsync(url, ct).ConfigureAwait(false);
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

        /// <summary>解析资源详情页，返回 download?version=NNN 绝对直链（未登录可下载）。</summary>
        public static async Task<string?> ResolveDownloadUrlAsync(string pageUrl, CancellationToken ct)
        {
            string html = await Http.GetStringAsync(pageUrl, ct).ConfigureAwait(false);
            Match m = DownloadLinkRegex.Match(html);
            if (!m.Success)
            {
                return null;
            }
            string href = WebUtility.HtmlDecode(m.Groups[1].Value);
            return href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : "https://www.beamng.com/" + href.TrimStart('/');
        }

        /// <summary>
        /// 下载文件到 targetPath：探测 Accept-Ranges 后大文件分 4-8 段并行（实测 R2 支持 206），
        /// 小文件 / 不支持 Range / 分段失败时回退 1MB 缓冲单流。progress 回调可能来自后台线程。
        /// </summary>
        public static async Task DownloadToFileAsync(string downloadUrl, string targetPath, Action<BeamNgDownloadProgress>? progress, CancellationToken ct)
        {
            long? total = null;

            // 探测：总大小 + 是否支持 Range
            using (HttpResponseMessage probe = await SendDownloadAsync(downloadUrl, null, ct).ConfigureAwait(false))
            {
                probe.EnsureSuccessStatusCode();
                total = probe.Content.Headers.ContentLength;
                bool acceptRanges = probe.Headers.AcceptRanges.Contains("bytes") || probe.StatusCode == HttpStatusCode.PartialContent;
                if (acceptRanges && total.HasValue && total.Value >= 16L * 1024 * 1024)
                {
                    long reportedTotal = total.Value;
                    try
                    {
                        await DownloadSegmentedAsync(downloadUrl, targetPath, probe, reportedTotal, progress, ct).ConfigureAwait(false);
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
            using (HttpResponseMessage resp = await SendDownloadAsync(downloadUrl, null, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                total = resp.Content.Headers.ContentLength;
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
        }

        /// <summary>分段并行下载：probe 响应的流作为第 0 段数据源复用，其余段各自带 Range 请求。</summary>
        private static async Task DownloadSegmentedAsync(string downloadUrl, string targetPath, HttpResponseMessage probe, long total, Action<BeamNgDownloadProgress>? progress, CancellationToken ct)
        {
            int segCount = total >= 64L * 1024 * 1024 ? 8 : total >= 24L * 1024 * 1024 ? 6 : 4;
            long segLen = total / segCount;
            if (segLen <= 0)
            {
                throw new IOException("文件过小，无需分段");
            }

            // 预分配文件长度，各段各自持句柄写自己的区间
            using (FileStream prealloc = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                prealloc.SetLength(total);
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

            Stream firstStream = await probe.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var tasks = new List<Task>(segCount);
            try
            {
                for (int i = 0; i < segCount; i++)
                {
                    long start = i * segLen;
                    long end = (i == segCount - 1) ? total - 1 : start + segLen - 1;
                    Stream? reuse = (i == 0) ? firstStream : null;
                    tasks.Add(Task.Run(() => CopySegmentAsync(downloadUrl, targetPath, start, end, reuse, sharedDone, Report, ct), ct));
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                // 第 0 段的流归 CopySegmentAsync 管理；这里兜底释放
            }
        }

        private static async Task CopySegmentAsync(string downloadUrl, string targetPath, long start, long end, Stream? reused, long[] sharedDone, Action report, CancellationToken ct)
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
                    await using (FileStream dst = new(targetPath, FileMode.Open, FileAccess.Write, FileShare.Write, 64 * 1024, useAsync: true))
                    {
                        dst.Seek(start, SeekOrigin.Begin);
                        byte[] buffer = new byte[1024 * 1024];
                        long pos = 0;
                        int n;
                        while (pos < segLen && (n = await src.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, segLen - pos)), ct).ConfigureAwait(false)) > 0)
                        {
                            await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
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
    }
}
