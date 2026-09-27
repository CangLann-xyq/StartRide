using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
    /// <summary>一条线路的测速结果。</summary>
    public sealed class RelayProbeResult
    {
        public string Name { get; init; } = "";
        public string Endpoint { get; init; } = "";
        public bool Reachable { get; init; }
        public int LatencyMs { get; init; } = -1;
        public string Detail { get; init; } = "";

        public string LatencyText => Reachable
            ? (LatencyMs >= 0 ? LatencyMs + " ms" : "可达")
            : "不可达";
    }

    /// <summary>
    /// 联机中继连通性 / 延迟测试。
    ///
    /// 联机出问题时第一个要回答的问题是"我这台机器到中继到底通不通"。
    /// 这里分别测两条通道：
    ///   · WebSocket（80 端口）—— 主力通道，能穿过云安全组
    ///   · 直连 TCP（7777）—— 兜底通道，公网通常被安全组挡掉
    /// 顺带读一次 HTTP 响应头，确认对端确实是中继而不是随便一个监听端口。
    /// </summary>
    public sealed class RelayLatencyService
    {
        private readonly AppSettings _settings;

        public RelayLatencyService(AppSettings settings) => _settings = settings;

        private const int TimeoutMs = 5000;

        /// <summary>测全部通道 + 更新设置里记录的最近延迟。</summary>
        public async Task<System.Collections.Generic.List<RelayProbeResult>> ProbeAllAsync()
        {
            var app = _settings;
            var results = new System.Collections.Generic.List<RelayProbeResult>
            {
                await ProbeWebSocketAsync(app).ConfigureAwait(false),
                await ProbeTcpAsync(app).ConfigureAwait(false),
            };

            var best = results.Find(r => r.Reachable && r.LatencyMs >= 0)
                       ?? results.Find(r => r.Reachable);
            try
            {
                app.LastRelayLatencyMs = best != null ? best.LatencyMs : -1;
                app.Save();
            }
            catch { }

            return results;
        }

        /// <summary>WebSocket 通道：TCP 连接 + 发一次 HTTP GET 看对端有没有回响应。</summary>
        private static async Task<RelayProbeResult> ProbeWebSocketAsync(AppSettings app)
        {
            string host = app.RelayHost;
            int port = app.RelayWebSocketPort;
            string path = string.IsNullOrWhiteSpace(app.RelayWebSocketPath) ? "/" : app.RelayWebSocketPath;
            string endpoint = $"{host}:{port}{path}";

            var sw = Stopwatch.StartNew();
            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(TimeoutMs);

                await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
                int connectMs = (int)sw.ElapsedMilliseconds;

                string request =
                    "GET " + path + " HTTP/1.1\r\n" +
                    "Host: " + host + ":" + port + "\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    "Sec-WebSocket-Key: c3RhcnRyaWRlLXByb2JlLWtleQ==\r\n" +
                    "Sec-WebSocket-Version: 13\r\n\r\n";

                var bytes = Encoding.ASCII.GetBytes(request);
                using var stream = client.GetStream();
                await stream.WriteAsync(bytes, cts.Token).ConfigureAwait(false);

                var buffer = new byte[256];
                int n = await stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false);
                string header = n > 0 ? Encoding.ASCII.GetString(buffer, 0, n) : "";
                int totalMs = (int)sw.ElapsedMilliseconds;

                bool looksLikeRelay = header.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase);
                string firstLine = header.Split('\n')[0].Trim();

                return new RelayProbeResult
                {
                    Name = "WebSocket 通道（推荐）",
                    Endpoint = endpoint,
                    Reachable = true,
                    LatencyMs = connectMs,
                    Detail = looksLikeRelay
                        ? $"握手响应 {totalMs} ms · {firstLine}"
                        : $"端口通但响应异常：{(firstLine.Length > 0 ? firstLine : "无响应数据")}",
                };
            }
            catch (OperationCanceledException)
            {
                return new RelayProbeResult
                {
                    Name = "WebSocket 通道（推荐）",
                    Endpoint = endpoint,
                    Reachable = false,
                    Detail = $"连接超过 {TimeoutMs / 1000} 秒未响应",
                };
            }
            catch (Exception ex)
            {
                return new RelayProbeResult
                {
                    Name = "WebSocket 通道（推荐）",
                    Endpoint = endpoint,
                    Reachable = false,
                    Detail = ex.Message,
                };
            }
        }

        /// <summary>直连 TCP 通道。</summary>
        private static async Task<RelayProbeResult> ProbeTcpAsync(AppSettings app)
        {
            string host = app.RelayHost;
            int port = app.RelayTcpPort;
            string endpoint = $"{host}:{port}";

            var sw = Stopwatch.StartNew();
            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(TimeoutMs);
                await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
                return new RelayProbeResult
                {
                    Name = "直连 TCP 通道（兜底）",
                    Endpoint = endpoint,
                    Reachable = true,
                    LatencyMs = (int)sw.ElapsedMilliseconds,
                    Detail = "端口可达",
                };
            }
            catch (OperationCanceledException)
            {
                return new RelayProbeResult
                {
                    Name = "直连 TCP 通道（兜底）",
                    Endpoint = endpoint,
                    Reachable = false,
                    Detail = $"连接超过 {TimeoutMs / 1000} 秒未响应（云安全组通常不开这个端口，属正常）",
                };
            }
            catch (Exception ex)
            {
                return new RelayProbeResult
                {
                    Name = "直连 TCP 通道（兜底）",
                    Endpoint = endpoint,
                    Reachable = false,
                    Detail = ex.Message + "（云安全组通常不开这个端口，属正常）",
                };
            }
        }
    }
}
