using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{

    public sealed class LuaBridge : IDisposable
    {
        public const int Port = 4444;
        private const int MaxPayload = 1048576;

        private TcpListener? _listener;
        private TcpClient? _game;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private readonly object _lock = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        // ⚠️ 每个游戏侧连接一条独立的接收链路，且**必须能被单独取消**。
        // 内测诊断包实证：模组因连接误判会高频重连，每次 Accept 都新起一个 ReceiveLoopAsync，
        // 而旧的既没被取消也没退出 → 同一毫秒出现两条「游戏内模组已连接到启动器」，
        // 两个 loop 各自往自己的 pending 里拼字节、各自 Dispatch，缓冲错位后就把
        // 单字符当成 JSON 根，于是刷出成片的 'A'/'Z'/'P'/'B'/'N' is an invalid start of a value。
        // 所以连接一换，旧 loop 必须立刻停。
        private CancellationTokenSource? _gameCts;

        public bool IsGameConnected
        {
            get { lock (_lock) return _game is { Connected: true }; }
        }

        public event Action<string>? Log;
        public event Action<string, string>? GameReady;
        public event Action<JsonElement>? VehicleReceived;
        public event Action<JsonElement>? VehCfgReceived;
        public event Action<JsonElement>? ChatReceived;
        public event Action? GameDisconnected;

        public bool IsStarted
        {
            get { lock (_lock) return _listener != null; }
        }

        /// <summary>
        /// 测试专用故障注入：让下一次 AcceptTcpClientAsync **立刻抛一次异常**。
        /// 为什么需要它：AcceptLoop 里「异常要 continue 不能 return」这条规则，
        /// 靠真实网络是极难稳定复现的 —— 对端 RST、句柄抖动这类时序在环回网卡上几乎撞不到。
        /// 探针（SrHarness bridgeaccept）靠它把那个 catch 分支真正走一遍：
        /// 注入一次异常后，桥必须仍然能接受新连接；如果这里退化成 return，探针立刻挂。
        /// 生产路径从不设置它（默认 0），只影响测试进程。
        /// </summary>
        public static int FaultAcceptOnce;

        public void Start()
        {
            lock (_lock)
            {
                if (_listener != null) return;
                _cts = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
            }
            Log?.Invoke($"本地桥已监听 127.0.0.1:{Port}，等待游戏内模组连接");
            _ = Task.Run(() => AcceptLoopAsync(_cts!.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    // 测试故障注入：见 FaultAcceptOnce 注释。生产路径恒为 0。
                    if (Interlocked.Exchange(ref FaultAcceptOnce, 0) != 0)
                        throw new IOException("测试注入：模拟 Accept 瞬时失败");

                    client = await _listener!.AcceptTcpClientAsync(token);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    // ⚠️ 这里**必须 continue 而不是 return**。
                    // AcceptTcpClientAsync 在监听未真正关闭时也可能因瞬时资源忙/句柄抖动抛错；
                    // 一旦 return，AcceptLoop 就永久退出了 —— 而 _listener 仍然非 null
                    // （IsStarted 仍为 true），表面上"本地桥还在"，实际上再也不接受任何连接。
                    // 玩家侧的症状就是：联机一切看似正常，但游戏内模组怎么也连不上桥，
                    // 远程车全空白、收包恒为 0，而且日志里几乎无声（这才是最难查的一种）。
                    // 所以：记一笔，歇一下，继续听，绝不退出循环。
                    Log?.Invoke("本地桥 Accept 异常（继续监听）：" + ex.Message);
                    try { await Task.Delay(200, token); } catch (OperationCanceledException) { return; }
                    continue;
                }

                // 拿到连接后的整个处理段单独兜底：GetStream() 在 socket 刚被对端 RST
                // 的极端时序下会抛，绝不能让它把外层 AcceptLoop 带走。
                try
                {
                    NetworkStream stream;
                    // 旧连接必须先停干净：先取消它的接收链路，再关 socket。
                    // 顺序反过来的话，旧 loop 可能已经读到一半的字节再往它自己的 pending 里写，
                    // 而那份 pending 与本次新连接无关，只会白跑一轮 Dispatch。
                    CancellationTokenSource? oldGameCts;
                    lock (_lock)
                    {
                        oldGameCts = _gameCts;
                        _gameCts = null;
                    }
                    try { oldGameCts?.Cancel(); } catch { }
                    try { oldGameCts?.Dispose(); } catch { }

                    var gameCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    lock (_lock)
                    {
                        CloseGameLocked();
                        _game = client;
                        _stream = stream = client.GetStream();
                        _gameCts = gameCts;
                    }
                    client.NoDelay = true;
                    Log?.Invoke("游戏内模组已连接到启动器");
                    _ = Task.Run(() => ReceiveLoopAsync(client, stream, gameCts));
                }
                catch (Exception ex)
                {
                    // 单条连接交接失败不该影响后续连接；把它记下来并继续接受下一个。
                    Log?.Invoke("本地桥连接交接失败（继续监听）：" + ex.Message);
                    try { client.Close(); } catch { }
                }
            }
        }

        private async Task ReceiveLoopAsync(TcpClient client, NetworkStream stream, CancellationTokenSource gameCts)
        {
            CancellationToken token = gameCts.Token;
            var buffer = new byte[65536];
            var pending = new MemoryStream();

            try
            {
                while (!token.IsCancellationRequested && client.Connected)
                {
                    // ⚠️ 只认「当前这条」连接。连接一换，本 loop 立刻退场，
                    // 绝不继续 Dispatch —— 这是 'A'/'Z'/'P'/'B'/'N' 那批报错的根因。
                    lock (_lock)
                    {
                        if (!ReferenceEquals(_game, client)) return;
                    }

                    int n = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (n == 0) break;
                    pending.Write(buffer, 0, n);

                    while (true)
                    {
                        var data = pending.ToArray();
                        if (data.Length < 4) break;

                        int len = data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24);
                        if (len <= 0 || len > MaxPayload)
                        {
                            Log?.Invoke($"模组数据长度非法（{len}），丢弃缓冲");
                            pending.SetLength(0);
                            break;
                        }
                        if (data.Length < 4 + len) break;

                        string json = Encoding.UTF8.GetString(data, 4, len);
                        var rest = new byte[data.Length - 4 - len];
                        Array.Copy(data, 4 + len, rest, 0, rest.Length);
                        pending.SetLength(0);
                        pending.Write(rest, 0, rest.Length);

                        Dispatch(json);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                // 被新连接顶掉的旧 loop 不该报错，那是正常交接。
                bool stillCurrent;
                lock (_lock) stillCurrent = ReferenceEquals(_game, client);
                if (stillCurrent) Log?.Invoke("模组连接出错：" + ex.Message);
            }
            finally
            {
                bool stillCurrent;
                lock (_lock)
                {
                    stillCurrent = ReferenceEquals(_game, client);
                    if (stillCurrent)
                    {
                        CloseGameLocked();
                        _gameCts = null;
                    }
                }
                try { client.Close(); } catch { }
                // 只有「自己仍是当前连接」才算真的断开；被顶掉的那条只是交接。
                if (stillCurrent)
                {
                    Log?.Invoke("模组与启动器断开");
                    GameDisconnected?.Invoke();
                }
                try { gameCts.Dispose(); } catch { }
            }
        }

        private void Dispatch(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return;
                if (!root.TryGetProperty("type", out var t)) return;

                switch (t.GetString())
                {
                    case "ready":
                        string name = root.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                        string ver = root.TryGetProperty("version", out var vp) ? vp.GetString() ?? "" : "";
                        Log?.Invoke($"模组就绪 name={name} version={ver}");
                        GameReady?.Invoke(name, ver);
                        break;

                    case "vehicle":
                        if (root.TryGetProperty("id", out _)) VehicleReceived?.Invoke(root.Clone());
                        break;

                    case "vehcfg":
                        VehCfgReceived?.Invoke(root.Clone());
                        break;

                    case "chat":
                        ChatReceived?.Invoke(root.Clone());
                        break;

                    case "ping":
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke("解析模组消息失败：" + ex.Message);
            }
        }

        public void SendJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            SendRaw(Encoding.UTF8.GetBytes(json));
        }

        public async Task SendAsync(object payload)
        {
            string json = JsonSerializer.Serialize(payload);
            await SendRawAsync(Encoding.UTF8.GetBytes(json));
        }

        public void Send(object payload)
        {
            try { SendJson(JsonSerializer.Serialize(payload)); }
            catch (Exception ex) { Log?.Invoke("发送到游戏失败：" + ex.Message); }
        }

        private void SendRaw(byte[] body)
        {
            _ = SendRawAsync(body);
        }

        private async Task SendRawAsync(byte[] body)
        {
            await _sendLock.WaitAsync();
            try
            {
                NetworkStream? s;
                lock (_lock) s = _stream;
                if (s == null || body.Length > MaxPayload) return;

                var frame = new byte[4 + body.Length];
                int len = body.Length;
                frame[0] = (byte)(len & 0xFF);
                frame[1] = (byte)((len >> 8) & 0xFF);
                frame[2] = (byte)((len >> 16) & 0xFF);
                frame[3] = (byte)((len >> 24) & 0xFF);
                Array.Copy(body, 0, frame, 4, body.Length);

                await s.WriteAsync(frame, 0, frame.Length);
                await s.FlushAsync();
            }
            catch (Exception ex)
            {
                Log?.Invoke("发送到游戏失败：" + ex.Message);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private void CloseGameLocked()
        {
            try { _stream?.Close(); } catch { }
            try { _game?.Close(); } catch { }
            _stream = null;
            _game = null;
            _gameCts = null;
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }

            CancellationTokenSource? gameCts;
            lock (_lock)
            {
                gameCts = _gameCts;
                _gameCts = null;
                CloseGameLocked();
                try { _listener?.Stop(); } catch { }
                _listener = null;
            }
            // 取消放在锁外：Cancel 会同步跑回调，持锁会让回调卡住。
            try { gameCts?.Cancel(); } catch { }
            try { gameCts?.Dispose(); } catch { }
        }

        public void Dispose() => Stop();
    }
}
