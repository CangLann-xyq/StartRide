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

        // ── 服务器只读中继 ────────────────────────────────────────────────────
        // 为什么必须有这一层（2026-09-25 加）：www.beamng.com 在国内**彻底不可达**。
        // 实测：本机直连 3/3 超时（16.7/17.0/17.0s）；本机 127.0.0.1:1786 代理 3/3 超时
        // （12.0/10.0/10.0s）；把本机 46 个监听端口逐个当代理试，**没有一条能拉到**。
        // 同一时刻腾讯云服务器 3.2s 就拿到 HTTP 200。
        // 所以网页（列表页/详情页/图标，单页 ~300KB，服务器还带 10 分钟缓存）由服务器代取；
        // 而几百 MB 的安装包**不过服务器** —— 详情页那条 download 直链 302 到 Cloudflare R2，
        // 实测本机直连 R2 是 HTTP 206 / 1.0s 通的，所以只请服务器把那次 302 的目标解出来
        // （几百字节）就够，下载仍由本机直连 CDN。服务器带宽不会被拖垮。
        private const string RelayBase = "https://windseek.cloud/api/startride/repo";

        // ── 本机可达性探测：**必须异步**，绝不能在静态初始化器里做 ──────────────
        // ⚠️ 2026-09-25 修「点开在线仓库，窗口卡住显示『程序未响应』几秒，然后自己又好了」：
        // 旧实现在静态字段初始化器里同步探测 —— `TcpReachable(…).Wait(2500)`（直连）
        // 加上 `ResolveUsableProxy()` 里的 `.Wait(1500)`（代理），于是**类第一次被触达的
        // 那条线程**要白等最多 4 秒。而第一次触达往往正是 UI 线程（点「刷新」的第一句
        // `InvalidatePageCache()`；或「在线仓库」按钮 setter 里同步跑的那段加载）。
        // 现在：探测丢进线程池，**探完之前通道里只有中继** —— 那本来也是国内唯一可靠的
        // 一条（见类顶部注释），探完再把代理/直连无缝加进来。任何调用点都不再等它。
        // 约束：静态字段初始化器仍按书写顺序执行，这里只允许放「纯分配、不联网」的初始化。

        /// <summary>beamng.com 是否本机可达（后台探一次；探到之前一律当作不可达）。</summary>
        private static volatile bool directBeamNgReachable;

        /// <summary>探测到的本机可用代理（null = 没探到 / 没有），只在探测任务里赋值。</summary>
        private static IWebProxy? usableProxy;

        private static int channelProbeStarted;

        /// <summary>探测完成后重建的通道表；为 null 表示探测还没跑完（此时只用中继）。</summary>
        private static volatile HttpClient[]? probedListChannels;
        private static volatile HttpClient[]? probedDownloadChannels;

        private static bool DirectBeamNgReachable => directBeamNgReachable;

        /// <summary>中继专用小客户端（访问自有服务器，强制直连、不走本机代理）。</summary>
        private static readonly HttpClient RelayHttp = CreateRelayHttp();

        // ── HTTP 通道：代理 + 直连 + 中继，逐次尝试交替使用 ────────────────────
        // ⚠️ 为什么必须这样：beamng.com 在国内直连极不稳（实测同一台机器：10:34 还能 200，
        // 10:45 就变成 WinError 10060 连接超时，整批 12 页全废、耗时 213s），而本机代理
        // （Clash 之类：环境变量 http_proxy=http://127.0.0.1:1786 / Windows 系统代理）往往能通。
        // 旧代码写死 `UseProxy = false`，等于把唯一可用的那条路砍掉 —— 用户看到的就是
        // "拉取失败 / 剩下的拉不出来"。现在多条通道轮流试，谁通用谁，并记住上次成功的。
        // 注意：中继只用于列表/详情页（网页），下载通道里没有它（见 BuildChannelSet）。
        // ⚠️ 下面这几个 HttpClient **只是分配对象、不联网**（连 DNS 都不查），
        // 放静态初始化器里是安全的；真正联网的探测见 EnsureChannelProbeStarted()。
        private static readonly HttpClient DirectListHttp = CreateListHttp(null, relay: false);
        private static readonly HttpClient DirectDownloadHttp = CreateDownloadHttp(null);
        private static readonly HttpClient RelayListHttp = CreateListHttp(null, relay: true);

        /// <summary>探测完成前的兜底：只有中继这一条（国内唯一稳定可达的通道）。</summary>
        private static readonly HttpClient[] RelayOnlyListChannels = { RelayListHttp };

        /// <summary>探测完成前的下载兜底：直连（下载只认本机网络，不经服务器）。</summary>
        private static readonly HttpClient[] DirectOnlyDownloadChannels = { DirectDownloadHttp };

        private static volatile int preferredListChannel;
        private static volatile int preferredDownloadChannel;

        /// <summary>列表/详情用的通道表（探测完成后自动换成含代理/直连的完整表）。</summary>
        private static HttpClient[] ListChannels => probedListChannels ?? RelayOnlyListChannels;

        /// <summary>下载用的通道表（**永远不含中继**：字节流不经服务器）。</summary>
        private static HttpClient[] DownloadChannels => probedDownloadChannels ?? DirectOnlyDownloadChannels;

        /// <summary>
        /// 启动一次后台探测（幂等），填好 probedListChannels / probedDownloadChannels。
        /// 它只做"起个线程池任务"这一件事，**绝不阻塞调用线程** ——
        /// 这正是"程序未响应"的根治点，别改回同步探测。
        /// </summary>
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
                    // 通道表换了，之前记住的下标可能指向另一条通道 → 归零重新学习
                    preferredListChannel = 0;
                    preferredDownloadChannel = 0;
                }
                catch
                {
                    // 极端情况下保持"只有中继/直连"也能用
                }
            });
        }

        /// <summary>
        /// 把发往 www.beamng.com 的请求改写到自有服务器的只读中继。
        /// 只改写 beamng.com；其它域（例如中继自己的地址）原样放行。
        /// </summary>
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

        /// <summary>
        /// 解析一个「端口确实连得上」的本机代理；没有可用代理时返回 null（表示直连）。
        /// 探测很重要：环境变量里常常留着已经关掉的代理地址，直接拿来用只会白等超时。
        /// </summary>
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

            // 兜底：Windows 系统代理（WinINET）。IsBypassed 为 true 说明系统代理没启用
            // 或对本站不生效 —— 那就等价于直连，不必再多开一条通道。
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

        /// <summary>
        /// 构建通道数组（**纯函数，不联网**）。顺序 = 优先尝试顺序，失败自动换下一条（见 ListChannelFor）。
        /// 只在 EnsureChannelProbeStarted() 的后台任务里调用，参数是探测结果。
        ///
        /// 列表/详情（list=true）：
        ///   [本机代理?] → [直连（仅当探测到 beamng.com 可达）] → [服务器中继]
        ///   ⚠️ 直连只有在"探测确实连得上 443"时才放进来 —— 否则每次请求都要白等
        ///   25 秒连接超时，88 页全量滚动会把时间全耗在等超时上。
        ///   ⚠️ 中继永远垫底：自己服务器能通就该用它，别去赌那条断掉的路。
        /// 下载（list=false）：
        ///   只有 [本机代理?] → [直连]。**不放中继** —— 字节流不经服务器，
        ///   下载前会先把 beamng 的 download 直链解析成 CDN 地址（见 ResolveDirectCdnUrlAsync）。
        /// </summary>
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
                // 中继通道**强制直连**（不借用本机代理）：windseek.cloud 是国内服务器，
                // 直连一定通；借道代理反而可能因为代理规则/失效把唯一可靠的通道弄没。
                channels.Add(RelayListHttp);
            }
            return channels.ToArray();
        }

        /// <summary>取第 attempt 次尝试要用的列表通道（首选通道优先）。</summary>
        private static HttpClient ListChannelFor(int attempt)
        {
            // ⚠️ 通道表是可以在运行中被后台探测换掉的（1 条 → 2~3 条），
            // 所以这里每次都重新取、并把下标夹到合法范围，别缓存 ch.Length。
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

        /// <summary>某次尝试成功了 → 记住这条通道，下次先用它。</summary>
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
                // ⚠️ 不能设太小：冷连接（DNS + TLS 握手）实测要 12.9s，之前设 10s 会让
                // **首次请求必然超时**（超时又抛 TaskCanceledException，被误当取消 → 首屏直接失败）。
                ConnectTimeout = TimeSpan.FromSeconds(25),
                // 首屏要并发拉多页；默认不限制，但显式给出更利于连接复用。
                MaxConnectionsPerServer = 16,
                // 站点走 Cloudflare，长连接复用能省掉 TLS 握手（实测首包 12.9s、复用后每页 ~0.7s）
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
                // 下载是原始 zip 字节流，不要自动解压（Range 场景也无 gzip）
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                UseProxy = proxy != null,
                Proxy = proxy,
                ConnectTimeout = TimeSpan.FromSeconds(25),
                // R2 单连接也很快；多路复用由分段请求天然达成
                MaxConnectionsPerServer = 16,
            };
            // ResponseHeadersRead 流式下载不能受 15s 总超时影响
            var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            ApplyBrowserHeaders(client);
            return client;
        }

        /// <summary>
        /// 把 www.beamng.com 上的 download 地址解析成最终 CDN 直链。
        ///
        /// 只有"本机确实连不上 beamng.com 443"时才走服务器解析 —— 本机能直连的话直接跟着
        /// 302 走（少一次往返）。解析失败就原样返回，让原来的 302 流程自己去试，
        /// 绝不因为中继的问题把下载搞死。
        /// </summary>
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
                // 中继没解出来：退回原地址，交给 302 流程
            }
            return url;
        }

        /// <summary>中继专用小客户端（访问自有服务器，强制直连、不走本机代理）。</summary>
        /// <remarks>中继客户端不进通道表（RelayListHttp 才是表里那个），这里只给 /resolve 用。</remarks>
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
        /// 拉取第 1 页，并顺带解析 pageNav 里的 data-last 当作「总页数」。
        ///
        /// ⚠️ 这个总页数**不可信**，只能当软上限：实测全库 data-last=1577（≈157700 条），
        /// 而第 89 页就已经返回 0 条，真实只有 88 页 / 8834 条。若把它当真，客户端会一直
        /// 往空页拉，状态栏永远显示"已加载 8834 / 约 157700"，用户看到的就是
        /// 「剩下的永远拉不出来」。所以真正的到底判定只能靠「某页解析出 0 条」
        /// （见 ListPageBatch.ReachedEnd），这里返回的值仅用于显示与粗略夹逼。
        ///
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
        /// 一批列表页的拉取结果。
        ///
        /// ⚠️ 必须逐页容错，绝不能让整批共用一个 Task.WhenAll：只要有一页请求彻底失败，
        /// 整批 10 页就全部作废、调用方的 nextPage 不推进 → 用户再滚到底又重拉同样 10 页、
        /// 又失败，表现就是「后面的一直拉不出来」。所以失败页单独记账、成功的照常返回。
        ///
        /// ⚠️ 与「真实末页」相关的两个字段：站点 pageNav 里的 data-last 完全不可信
        /// （实测全库 data-last=1577，而第 89 页就已经空了，真实只有 88 页 / 8834 条）。
        /// 唯一的真信号是「某一页解析出 0 条」——那就是越过了末页。
        /// </summary>
        public sealed class ListPageBatch
        {
            public List<BeamNgModInfo> Items { get; } = new();

            /// <summary>批内在线上真的解析到条目的最大页号；0 表示本批一页都没内容。</summary>
            public int LastNonEmptyPage { get; set; }

            /// <summary>批内第一个空页（= 已越过真实末页）；0 表示本批没有空页。</summary>
            public int FirstEmptyPage { get; set; }

            /// <summary>批内请求彻底失败的页号（已重试仍失败）。</summary>
            public List<int> FailedPages { get; } = new();

            public int RequestedFrom { get; set; }

            public int RequestedTo { get; set; }

            /// <summary>确认已经拉到全库末尾（有空页且没有失败页需要补拉）。</summary>
            public bool ReachedEnd => FirstEmptyPage > 0 && FailedPages.Count == 0;
        }

        /// <summary>列表页并发度。实测单页 1.8-2.5s，偶发 9s+ 慢页；4 并发比 6 并发更少触发慢页。</summary>
        private const int ListPageParallelism = 4;

        /// <summary>
        /// 并发拉取 [fromPage, toPage] 页，返回带「真实末页/失败页」信息的批次结果。
        /// 单页最多尝试 3 次（退避 400ms/800ms），仍失败只记入 FailedPages，不影响其它页。
        /// </summary>
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
                        // 只有"真的被取消"才往外抛
                        throw;
                    }
                    catch
                    {
                        // ⚠️ 必须能接住超时：HttpClient 的连接/请求超时抛的是 TaskCanceledException，
                        // 而它**继承自 OperationCanceledException**。之前写成 `catch (OperationCanceledException){throw;}`
                        // 就把超时当成用户取消抛了出去 → Task.WhenAll 整批炸 → 12 页全废、nextPage 不推进
                        // → 用户看到"剩下的永远拉不出来"。现在按普通失败记账，其它页照常返回。
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

            // 双保险：逐页 catch 已经兜住了常规异常，这里再拦一层，
            // 保证任何残留异常都不会让整批结果作废（只有真取消才往上抛）。
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
                // 忽略：失败页已记在 failedPages 里
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
                    // 每条通道各试一次：首选通道不通就换另一条（代理↔直连），这样无论
                    // 用户是"只能走代理"还是"代理已关只能直连"都能拉到数据。
                    // ⚠️ 这个 catch 必须能接住**超时**：HttpClient 的连接/请求超时抛的是
                    // TaskCanceledException（继承 OperationCanceledException）。若上面那行漏了
                    // `when (ct.IsCancellationRequested)`，超时会被当成用户取消直接抛出 ——
                    // 一次重试都不做，而冷连接实测要 12.9s，超时是常态不是异常路径。
                    await Task.Delay(400 * (attempt + 1), ct).ConfigureAwait(false);
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
                    // 换另一条通道再试（代理↔直连）
                    await Task.Delay(300 * (attempt + 1), ct).ConfigureAwait(false);
                }
                catch
                {
                    // ⚠️ 超时（HttpClient 抛的 TaskCanceledException）也会落到这里 → 返回 null，
                    // 界面降级成"读取失败，可稍后重试"，而不是被误当取消卡在"正在读取该模组的介绍…"。
                    return null;
                }
            }
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
            EnsureChannelProbeStarted();
            // beamng 的 download 地址会 302 到 Cloudflare R2。国内 www.beamng.com 不通但 R2 通
            // （实测本机 R2 HTTP 206 / 1.0s），所以先请服务器把跳转目标解出来（几百字节），
            // 几百 MB 的包仍由本机直连 CDN —— 服务器带宽不参与大流量。
            downloadUrl = await ResolveDirectCdnUrlAsync(downloadUrl, ct).ConfigureAwait(false);

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
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // 分段失败（网络波动/超时/Range 被拒）→ 落回单流完整下载
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
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch when (attempt < 1 && reused == null)
                {
                    // 单段失败重试一次（超时也算失败 —— 见上面 FetchPageWithRetryAsync 的说明）
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
            // await 必须在 using 作用域内完成：302→R2 重定向跟随期间不能提前释放 request
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
