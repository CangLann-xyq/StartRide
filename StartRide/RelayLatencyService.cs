using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
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

    public sealed class RelayLatencyService
    {
        private readonly AppSettings _settings;

        public RelayLatencyService(AppSettings settings) => _settings = settings;

        private const int TimeoutMs = 5000;

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

        private static async Task<RelayProbeResult> ProbeWebSocketAsync(AppSettings app)
        {
            string host = app.RelayHost;
            int port = app.RelayWebSocketPort;
            string path = string.IsNullOrWhiteSpace(app.RelayWebSocketPath) ? "/" : app.RelayWebSocketPath;
            string endpoint = $"{host}:{port}{path}";

            var sw = Stopwatch.StartNew();
            // 这里刻意**不用 using**：TcpClient.ConnectAsync 的 CancellationToken 只能取消“等待”，
            // 并不中断底层 connect；超时后若靠 using 隐式 Dispose，socket 会以 SocketError 995
            // （“已中止 I/O 操作”）在连接完成回调里抛异常，而此时 await 已经返回，
            // 异常无人接管 → 变成 UnobservedTaskException（终结器线程重抛，污染日志）。
            // 所以改为 finally 里显式关闭，并在关闭前后吞掉中止类异常。
            var client = new TcpClient();
            try
            {
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
                    Detail = DescribeSocketFailure(ex),
                };
            }
            finally
            {
                DisposeQuietly(client);
            }
        }

        /// <summary>
        /// 关掉探测用的 TcpClient，并吞掉“连接被中止”这类必然伴随关闭而来的异常。
        /// 关键点：把 Dispose 的异常吃掉，否则它会以 UnobservedTaskException 形式
        /// 从终结器线程重抛出去（日志里那条 SocketException 995 就是它）。
        /// </summary>
        private static void DisposeQuietly(TcpClient client)
        {
            try { client.Dispose(); }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            catch (Exception) { }
        }

        /// <summary>
        /// SocketError 995（OperationAborted）是“我们自己主动中止连接”的正常表现，
        /// 不该原样抛给用户看，翻成人话。
        /// </summary>
        private static string DescribeSocketFailure(Exception ex)
        {
            if (ex is SocketException se && se.SocketErrorCode == SocketError.OperationAborted)
                return $"连接超过 {TimeoutMs / 1000} 秒未响应";
            return ex.Message;
        }

        private static async Task<RelayProbeResult> ProbeTcpAsync(AppSettings app)
        {
            string host = app.RelayHost;
            int port = app.RelayTcpPort;
            string endpoint = $"{host}:{port}";

            var sw = Stopwatch.StartNew();
            // 同 ProbeWebSocketAsync：不用 using，避免超时后 995 异常变成 unobserved。
            var client = new TcpClient();
            try
            {
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
                    Detail = DescribeSocketFailure(ex) + "（云安全组通常不开这个端口，属正常）",
                };
            }
            finally
            {
                DisposeQuietly(client);
            }
        }
    }
}
