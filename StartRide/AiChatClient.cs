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
                req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    result.Error = $"服务端返回 {(int)resp.StatusCode}：{Trim(body, 300)}";
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
