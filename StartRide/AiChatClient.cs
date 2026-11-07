using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
    /// <summary>一次工具调用（服务端返回的 tool_calls，参数是流式拼起来的 JSON 字符串）。</summary>
    public sealed class AiToolCall
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "";
    }

    /// <summary>一轮对话的结果：正文 + 思考过程 + 需要执行的工具调用。</summary>
    public sealed class AiTurnResult
    {
        public string Content { get; set; } = "";
        public string Reasoning { get; set; } = "";
        public List<AiToolCall> ToolCalls { get; } = new();

        /// <summary>出错原因（连接失败 / 服务端报错 / 上游提前断开）。</summary>
        public string? Error { get; set; }

        /// <summary>
        /// 用户主动点了停止。
        ///
        /// 必须和 Error 分开：以前取消被吞成 Error「已取消」，
        /// UI 就只能走「出错」分支，显示一句红字警告 —— 明明是用户自己停的。
        /// </summary>
        public bool Cancelled { get; set; }

        /// <summary>
        /// 上游没给结束标记就断了（收到部分正文后就 silent）。
        ///
        /// 这种「半截」最坑：内容非空 → Ok == true → UI 当成正常完成，
        /// 用户看到的就是「跑到一半就没输出了」。必须显式标出来。
        /// </summary>
        public bool StreamBroken { get; set; }

        public bool Ok => string.IsNullOrEmpty(Error) && !Cancelled;
    }

    /// <summary>
    /// AI 助手的网络层：只跟自己的服务端说话（/api/startride/ai），
    /// **不持有 DeepSeek 的密钥** —— 密钥在服务器 .env 里，客户端拿不到。
    ///
    /// 为什么走服务端而不是直连 api.deepseek.com：
    ///   安装包是 .NET 程序集，写进代码或配置的 key 用 strings/ildasm 几分钟就能扒出来；
    ///   一旦泄露就是按量计费的损失，而且换 key 还得发版。
    ///
    /// 流式：一边收 SSE 一边回调 onReasoning / onContent，UI 才能逐字显示
    /// 「思考过程」和正文，而不是等整段返回。
    /// </summary>
    /// <summary>
    /// 本周 AI 额度快照（服务端按账号计费）。
    ///
    /// 单位是服务端自定义的「点」：1 元 = PointsPerYuan 点（默认 10000），
    /// 每周上限由服务端 SR_AI_WEEKLY_POINTS 决定，以 /quota 返回的 LimitYuan 为准，
    /// 客户端不写死金额——运营侧调整额度无需重新发版。
    /// 客户端只负责显示和提前劝阻，**真正的拦截在服务端**——前端算得再准也能被绕过。
    /// </summary>
    public sealed class AiQuota
    {
        public string Username { get; set; } = "";
        public string WeekKey { get; set; } = "";
        public double PointsPerYuan { get; set; } = 10000;
        public double Limit { get; set; }
        public double Used { get; set; }
        public double Left { get; set; }
        public double LimitYuan { get; set; }
        public double LeftYuan { get; set; }
        public double Percent { get; set; }
        public string? Error { get; set; }

        public bool Exhausted => Limit > 0 && Left <= 0;

        /// <summary>侧栏一行的文案：优先显示「还剩多少钱」，钱才是最直观的量。</summary>
        public string Summary =>
            Error != null
                ? Error
                : $"本周还剩 ¥{LeftYuan:0.00} / ¥{LimitYuan:0.00}";

        public string Detail =>
            Error != null
                ? ""
                : $"已用 {Percent:0.#}%　{Used:0.#} / {Limit:0.#} 点　重置于下周一 00:00";
    }

    public sealed class AiChatClient
    {
        /// <summary>
        /// 主域 + 备用域。两个域名指向同一台服务器、同一套 nginx 路由。
        ///
        /// 为什么必须有备用：实测存在「同一个 IP、同一个 443，换 SNI 就握手失败」的网络
        /// （本机就是：SNI=cloudfurhub.cn 直接被重置，SNI=windseek.cloud 正常；服务器
        /// 侧同 SNI 自测 200、证书到 2027 年 —— 说明是链路上按域名做了手脚，不是服务端问题）。
        /// 用户端我们控制不了链路，只能自己兜底。
        /// </summary>
        private static readonly string[] BaseCandidates =
        {
            "https://cloudfurhub.cn/api/startride/ai",
            "https://windseek.cloud/api/startride/ai",
        };

        private static int baseIndex;

        private static string CurrentBase => BaseCandidates[baseIndex];

        private static void AdvanceBase() => baseIndex = (baseIndex + 1) % BaseCandidates.Length;

        /// <summary>连接层失败（TLS/连接被拒/超时）统一用这个前缀，方便换域名重试。</summary>
        private const string TransportPrefix = "连接失败：";

        /// <summary>服务端用它挡掉随手 curl（不是强认证，只是抬高门槛）。</summary>
        private const string ClientTag = "startride-launcher";

        /// <summary>
        /// 读当前登录账号的 token（cloud-auth.json，和 CloudSyncService 同一份）。
        ///
        /// 为什么要带：AI 额度是按账号算的，服务端要靠 token 认出是谁；
        /// 不带就只会得到 401，界面上表现为「AI 用不了」。
        /// 这里刻意不缓存 —— AI 对话一次往返几秒，读一个小文件的开销可以忽略，
        /// 而缓存反而会在「切号/退出登录」后继续用旧 token，报出难懂的错。
        /// </summary>
        private static string? ReadAccountToken()
        {
            try
            {
                var p = Path.Combine(AppSettings.ConfigDirectory, "cloud-auth.json");
                if (!File.Exists(p)) return null;
                using var doc = JsonDocument.Parse(File.ReadAllText(p));
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;
                foreach (var name in new[] { "Token", "token" })
                {
                    if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
                    {
                        var s = el.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                }
            }
            catch
            {
                // 文件损坏/被占用都不该让 AI 直接崩，交给服务端 401 去兜
            }
            return null;
        }

        /// <summary>
        /// 多久收不到一帧就算断流。
        /// deepseek-reasoner 在最难的题上首字也可能憋 30~40 秒，
        /// 所以阈值要比「正常最慢」再宽一点，避免把思考中的模型误判成断线。
        /// </summary>
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(75);

        private static readonly HttpClient Http = CreateHttp();

        private static HttpClient CreateHttp()
        {
            var handler = new SocketsHttpHandler
            {
                // 和 ApiService 一致：不走系统代理，避免本机代理残留导致全超时
                UseProxy = false,
                ConnectTimeout = TimeSpan.FromSeconds(15),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            };
            // 整体不设超时：思考型模型可能几十秒才吐第一个字，用 CancellationToken 控制
            return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        }

        /// <summary>
        /// 查本周 AI 额度。失败不抛——侧栏显示一行提示就行，不能因为查不到额度就让整个页面报错。
        /// </summary>
        public static async Task<AiQuota> GetQuotaAsync(CancellationToken ct)
        {
            var q = new AiQuota();
            var token = ReadAccountToken();
            if (string.IsNullOrEmpty(token))
            {
                q.Error = "未登录，AI 助手需要账户登录后使用";
                return q;
            }

            for (var attempt = 0; attempt < BaseCandidates.Length; attempt++)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(8));
                    using var req = new HttpRequestMessage(HttpMethod.Get, CurrentBase + "/quota");
                    req.Headers.TryAddWithoutValidation("x-sr-client", ClientTag);
                    req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                    using var resp = await Http.SendAsync(req, cts.Token);
                    var text = await resp.Content.ReadAsStringAsync(cts.Token);

                    if (!resp.IsSuccessStatusCode)
                    {
                        q.Error = (int)resp.StatusCode == 401
                            ? "登录已过期，请到账户页重新登录"
                            : $"额度查询失败（{(int)resp.StatusCode}）";
                        return q;
                    }

                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("data", out var d))
                    {
                        q.Error = null;
                        FillQuota(d, q);
                        return q;
                    }
                    q.Error = "额度数据格式异常";
                    return q;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    q.Error = "额度查询超时";
                    return q;
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException)
                {
                    AdvanceBase();      // 换备用域再试一次
                }
                catch (Exception ex)
                {
                    q.Error = "额度查询失败：" + ex.Message;
                    return q;
                }
            }
            q.Error = "额度查询失败：网络不通";
            return q;
        }

        private static double Num(JsonElement d, string name, double dflt = 0)
        {
            if (d.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number)
            {
                return el.GetDouble();
            }
            return dflt;
        }

        private static void FillQuota(JsonElement d, AiQuota q)
        {
            if (d.TryGetProperty("username", out var u) && u.ValueKind == JsonValueKind.String)
                q.Username = u.GetString() ?? "";
            if (d.TryGetProperty("weekKey", out var w) && w.ValueKind == JsonValueKind.String)
                q.WeekKey = w.GetString() ?? "";
            q.PointsPerYuan = Num(d, "pointsPerYuan", 10000);
            q.Limit = Num(d, "limit");
            q.Used = Num(d, "used");
            q.Left = Num(d, "left");
            q.LimitYuan = Num(d, "limitYuan");
            q.LeftYuan = Num(d, "leftYuan");
            q.Percent = Num(d, "percent");
        }

        public static string DefaultModel => "deepseek-chat";
        public static string ReasoningModel => "deepseek-reasoner";

        /// <summary>服务端健康检查（不暴露密钥，只告诉你 key 配好没有）。</summary>
        public async Task<string> CheckHealthAsync(CancellationToken ct)
        {
            try
            {
                var errors = new List<string>();
                for (var i = 0; i < BaseCandidates.Length; i++)
                {
                    var url = BaseCandidates[baseIndex] + "/health";
                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Get, url);
                        req.Headers.TryAddWithoutValidation("x-sr-client", ClientTag);
                        using var resp = await Http.SendAsync(req, ct);
                        var text = await resp.Content.ReadAsStringAsync(ct);
                        return BaseCandidates[baseIndex] + " → " + (int)resp.StatusCode + " " + text;
                    }
                    catch (Exception ex)
                    {
                        errors.Add(BaseCandidates[baseIndex] + "：" + ex.Message);
                        AdvanceBase();
                    }
                }
                return string.Join("；", errors);
            }
            catch (Exception ex)
            {
                return TransportPrefix + ex.Message;
            }
        }

        /// <summary>
        /// 发一轮请求并流式回调。
        /// messages / tools 用 Dictionary 而不是强类型：agent 每轮拼的结构不一样，
        /// 强类型会写出一堆只出现一次的 record。
        /// </summary>
        public async Task<AiTurnResult> StreamAsync(
            string model,
            List<Dictionary<string, object?>> messages,
            List<Dictionary<string, object?>>? tools,
            Action<string>? onReasoning,
            Action<string>? onContent,
            CancellationToken ct)
        {
            var payload = new Dictionary<string, object?>
            {
                ["model"] = string.IsNullOrWhiteSpace(model) ? DefaultModel : model,
                ["messages"] = messages,
                ["max_tokens"] = 4096,
            };
            if (tools is { Count: > 0 })
            {
                payload["tools"] = tools;
                payload["tool_choice"] = "auto";
            }

            // 连接层失败时换备用域名重试（每个域名只试一次，避免死循环）
            Exception? lastTransport = null;
            for (var attempt = 0; attempt < BaseCandidates.Length; attempt++)
            {
                var r = await StreamFromAsync(CurrentBase, model, payload, onReasoning, onContent, ct);
                if (r.Cancelled) return r;
                if (r.Error != null && r.Error.StartsWith(TransportPrefix, StringComparison.Ordinal))
                {
                    lastTransport = new HttpRequestException(r.Error);
                    AdvanceBase();
                    continue;
                }
                return r;
            }
            return new AiTurnResult
            {
                Error = TransportPrefix + "主域与备用域都不通，请检查网络。" + (lastTransport?.Message ?? ""),
            };
        }

        private async Task<AiTurnResult> StreamFromAsync(
            string baseUrl,
            string model,
            Dictionary<string, object?> payload,
            Action<string>? onReasoning,
            Action<string>? onContent,
            CancellationToken ct)
        {
            var result = new AiTurnResult();
            HttpResponseMessage? resp = null;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/chat");
                req.Headers.TryAddWithoutValidation("x-sr-client", ClientTag);
                var token = ReadAccountToken();
                if (!string.IsNullOrEmpty(token))
                {
                    req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                }
                req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    var code = (int)resp.StatusCode;

                    // 401：没登录 / token 过期。这不是「服务坏了」，得说清楚该去做什么。
                    if (code == 401)
                    {
                        result.Error = "未登录或登录已过期。请先到「账户」页登录，AI 助手按账号计费。";
                        return result;
                    }

                    // 502：上游（DeepSeek）瞬时故障，服务端已自动重试仍未成功。
                    // 不把「上游返回 500」这种技术黑话丢给玩家；失败请求不计费，要说清楚。
                    // 注意只拦 502 —— 429 里有「额度用完」的专用提示，不能被覆盖。
                    if (code == 502)
                    {
                        result.Error = "AI 服务暂时繁忙，请稍等几秒再发一次（本次未扣除额度）。";
                        return result;
                    }

                    // 服务端把额度用完定义成 4291，msg 里已经写好了人话（含金额与重置时间），直接透给用户
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("msg", out var m) && m.ValueKind == JsonValueKind.String)
                        {
                            var msg = m.GetString();
                            if (!string.IsNullOrWhiteSpace(msg))
                            {
                                result.Error = msg;
                                return result;
                            }
                        }
                    }
                    catch
                    {
                        // 不是 JSON 就走下面的通用文案
                    }

                    result.Error = $"服务端返回 {code}：{Trim(body, 300)}";
                    return result;
                }

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                var calls = new SortedDictionary<int, AiToolCall>();
                var contentBuf = new StringBuilder();
                var reasonBuf = new StringBuilder();

                // 正常收尾的两种标记：`data: [DONE]`，或某一帧带 finish_reason。
                // 两个都没见到就断流 = 上游把连接掐了（nginx 超时 / 网关重置 / 服务端崩）。
                var sawDone = false;
                var sawFinishReason = false;

                while (true)
                {
                    // ── 空闲超时 ─────────────────────────────────────────────
                    // 不设这个的话，链路被掐断但 TCP 没 FIN 时 ReadLineAsync 会一直挂着：
                    // 界面就是「正在输出…」永远不动，停止按钮还因为状态没回收点不动。
                    string? line;
                    using (var idle = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        idle.CancelAfter(IdleTimeout);
                        try
                        {
                            line = await reader.ReadLineAsync(idle.Token);
                        }
                        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                        {
                            result.StreamBroken = true;
                            result.Error = $"上游 {IdleTimeout.TotalSeconds:0} 秒没有任何数据，连接已中断。";
                            break;
                        }
                    }

                    if (line == null) break;          // 流结束（可能正常，也可能断流，下面统一判）
                    if (line.Length == 0) continue;
                    if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

                    var json = line.Substring(5).Trim();
                    if (json.Length == 0) continue;
                    if (json == "[DONE]")
                    {
                        sawDone = true;
                        break;
                    }

                    try
                    {
                        if (ParseChunk(json, contentBuf, reasonBuf, calls, onReasoning, onContent)
                            == ParseOutcome.Finished)
                        {
                            sawFinishReason = true;
                        }
                    }
                    catch
                    {
                        // 单帧坏掉不该中断整轮：跳过这一帧，继续读
                    }
                }

                result.Content = contentBuf.ToString();
                result.Reasoning = reasonBuf.ToString();
                result.ToolCalls.AddRange(calls.Values);

                var gotAnything = result.Content.Length > 0
                                  || result.Reasoning.Length > 0
                                  || result.ToolCalls.Count > 0;

                if (result.Error == null && !sawDone && !sawFinishReason && gotAnything)
                {
                    // 有内容但没结束标记 —— 典型的「跑到一半没了」
                    result.StreamBroken = true;
                    result.Error = "输出被中断（上游没有给出结束标记）。已收到的内容保留在上面，可以再发一次。";
                }
                else if (result.Error == null && !gotAnything)
                {
                    result.Error = "模型没有返回任何内容（可能是额度或上游问题）。";
                }
                return result;
            }
            catch (OperationCanceledException)
            {
                // 用户点了停止：不是错误，别用红字吓人
                result.Cancelled = true;
                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                // TLS / 连接被重置 / 传输中断 —— 交给外层换域名
                result.Error = TransportPrefix + ex.Message;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                resp?.Dispose();
            }
        }

        private enum ParseOutcome
        {
            Continue = 0,
            Finished = 1,
        }

        private static ParseOutcome ParseChunk(
            string json,
            StringBuilder contentBuf,
            StringBuilder reasonBuf,
            SortedDictionary<int, AiToolCall> calls,
            Action<string>? onReasoning,
            Action<string>? onContent)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("choices", out var choices)) return ParseOutcome.Continue;
            if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) return ParseOutcome.Continue;

            var choice = choices[0];

            // finish_reason 非空 = 这一帧之后就没有内容了（stop / tool_calls / length）
            var finished = choice.TryGetProperty("finish_reason", out var fr)
                           && fr.ValueKind == JsonValueKind.String
                           && !string.IsNullOrEmpty(fr.GetString());

            if (!choice.TryGetProperty("delta", out var d))
                return finished ? ParseOutcome.Finished : ParseOutcome.Continue;

            // ① 思考过程（deepseek-reasoner 专有）
            if (d.TryGetProperty("reasoning_content", out var rc) && rc.ValueKind == JsonValueKind.String)
            {
                var piece = rc.GetString();
                if (!string.IsNullOrEmpty(piece))
                {
                    reasonBuf.Append(piece);
                    onReasoning?.Invoke(piece);
                }
            }

            // ② 正文
            if (d.TryGetProperty("content", out var cs) && cs.ValueKind == JsonValueKind.String)
            {
                var piece = cs.GetString();
                if (!string.IsNullOrEmpty(piece))
                {
                    contentBuf.Append(piece);
                    onContent?.Invoke(piece);
                }
            }

            // ③ 工具调用：分片到达，按 index 累加 name/arguments
            if (d.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in tcs.EnumerateArray())
                {
                    var index = item.TryGetProperty("index", out var ix) && ix.ValueKind == JsonValueKind.Number
                        ? ix.GetInt32()
                        : 0;
                    if (!calls.TryGetValue(index, out var call))
                    {
                        call = new AiToolCall();
                        calls[index] = call;
                    }
                    if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    {
                        var s = id.GetString();
                        if (!string.IsNullOrEmpty(s)) call.Id = s;
                    }
                    if (item.TryGetProperty("function", out var fn) && fn.ValueKind == JsonValueKind.Object)
                    {
                        if (fn.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String)
                        {
                            var s = nm.GetString();
                            if (!string.IsNullOrEmpty(s)) call.Name = s;
                        }
                        if (fn.TryGetProperty("arguments", out var ag) && ag.ValueKind == JsonValueKind.String)
                        {
                            call.Arguments += ag.GetString() ?? "";
                        }
                    }
                }
            }

            return finished ? ParseOutcome.Finished : ParseOutcome.Continue;
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
