using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
    /// <summary>
    /// 中继客户端。对应原版 Electron 的 main.js 中 dialRelay / receiveRelayData 一段。
    ///
    /// 连接策略（这是联机能不能通的要害）：
    ///   1. 优先 WebSocket：ws://{host}:80/relay-ws —— 搭在 80 端口上、
    ///      经 nginx 升级转发到中继的 127.0.0.1:7788/ws，能穿过云安全组。
    ///   2. 失败再回退直连 {host}:7777。
    ///
    /// 旧实现只做第 2 步，而腾讯云安全组并未放行 7777，所以外网永远连不上，
    /// 表现就是"进得去房间但看不到彼此的车"。
    ///
    /// 传输协议：换行分隔的 JSON（每条消息后跟 '\n'）。
    /// </summary>
    public sealed class RelayClient : IDisposable
    {
        private readonly AppSettings _settings;

        private TcpClient? _tcp;
        private NetworkStream? _stream;
        private WebSocketTransport? _ws;
        private CancellationTokenSource? _cts;

        private readonly object _sendLock = new();
        private bool _stopping;

        // ── 2.10.0 自动重连相关 ──────────────────────────────────────────────
        // _joinLine：join 报文原样存下来。断线重连只需把它重发一遍，中继就知道
        // 还是同一个玩家回到了同一个房间（房间码、playerId 都不变）。
        private string _joinLine = "";
        // 房主关房后不该再重连 —— 否则房间明明关了，我们还在后台不停敲门。
        private volatile bool _roomClosed;
        // 0/1 闸门，保证同时只有一条重连循环在跑。
        private int _reconnectRunning;
        // 最近一次收到下行的时间（TickCount64）。中继每 25 秒推一次 ping，
        // 所以"静默超时"是比 TCP 报错更早、更可靠的断线判据。
        private long _lastInboundTicks;
        private Timer? _watchdog;
        private int _reconnectAttempts;

        /// <summary>远程车辆缓存：游戏侧重连时补发，避免车辆凭空消失。</summary>
        private readonly Dictionary<string, JsonElement> _vehicleCache = new();
        private readonly object _cacheLock = new();

        public bool IsConnected { get; private set; }
        public string Transport { get; private set; } = "";
        public string CurrentRoomId { get; private set; } = "";
        public string LastError { get; private set; } = "";

        /// <summary>
        /// 本机联机 ID（<c>SR-XXXX-XXXX-XXXX</c>）。join 时按昵称算一次，重连沿用它 ——
        /// 重连如果换了 ID，在别人眼里就等于"老车消失、新车入场"，白抖一下。
        /// 游戏侧也拿它当车辆标识（见 relay-state 下发）。
        /// </summary>
        public string PlayerId { get; private set; } = "";

        /// <summary>本机昵称（join 时确定，重连沿用）。</summary>
        public string PlayerName { get; private set; } = "";

        /// <summary>自动重连成功后触发。与 <see cref="ConnectionChanged"/> 的区别：
        /// 后者只说"连接状态变了"，这个说"房间身份已经恢复" ——
        /// 上层收到后应该把房间状态与远程车缓存重新推给游戏，否则游戏里别人的车会一直空着。</summary>
        public event Action<string>? Reconnected;

        /// <summary>
        /// 中继下行帧数（**不含心跳**）。给游戏内 F8 面板的「中继下行 /10秒」
        /// 和启动器日志用——把心跳算进去的话，"这一栏是 0" 就永远不成立，
        /// 那条判据也就废了。
        /// </summary>
        public long FramesIn;

        public event Action<string>? Log;
        public event Action<bool, string>? ConnectionChanged;    // (connected, detail)
        public event Action<JsonElement>? VehicleReceived;
        public event Action<JsonElement>? VehCfgReceived;
        public event Action<JsonElement>? ChatReceived;
        public event Action<JsonElement>? PlayersReceived;
        public event Action<JsonElement>? SystemReceived;
        public event Action<JsonElement>? RoomClosed;

        public RelayClient(AppSettings settings) => _settings = settings;

        // ==================== 对外 API ====================

        /// <summary>加入房间。成功返回 true；失败会依次尝试 WS 与直连，都失败才返回 false。</summary>
        public async Task<bool> JoinAsync(string roomId, string playerName, RoomMeta meta, string token = "")
        {
            await LeaveAsync();

            _stopping = false;
            _roomClosed = false;
            _cts = new CancellationTokenSource();
            CurrentRoomId = roomId;
            _seenTypes.Clear();
            _reconnectAttempts = 0;

            string safeName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
            PlayerName = safeName;

            // ⚠️ 这里原来写的是 playerId = 昵称。后果不是"看起来难看"，是真丢车：
            // 游戏内模组用 `id == playerName` 丢自己的包，而玩家在游戏里多半没配昵称
            // （默认全是 'Player'），于是两个人的包互相被当成"自己的"丢掉 ——
            // 「进得去房间但看不见对方的车」就是这么来的，而且是**看运气**的：
            // 谁改过昵称谁正常，两个都没改就互相看不见。
            // 现在改成昵称哈希出的稳定 ID：与昵称解耦，同名不冲突，改名不换身份。
            PlayerId = StartRidePlayerId.Create(safeName);

            _joinLine = JsonSerializer.Serialize(new
            {
                type = "join",
                roomId,
                playerName = safeName,
                playerId = PlayerId,
                host = meta.Host,
                capacity = meta.Capacity,
                roomName = meta.RoomName,
                token,
            });

            var attempts = BuildAttempts();

            foreach (var attempt in attempts)
            {
                try
                {
                    await attempt();
                    if (!IsConnected) continue;

                    Log?.Invoke($"中继已连接（{Transport}）");
                    LastError = "";
                    StartWatchdog();
                    ConnectionChanged?.Invoke(true, "");
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    Log?.Invoke($"中继连接尝试失败（{Transport}）：{ex.Message}");
                    CleanupTransport();
                }
            }

            LastError = string.IsNullOrEmpty(LastError) ? "中继不可用" : LastError;
            Log?.Invoke("中继连接失败：" + LastError);
            ConnectionChanged?.Invoke(false, LastError);
            return false;
        }

        private async Task ConnectWebSocketAsync(string joinLine)
        {
            Transport = "websocket";
            var ws = new WebSocketTransport();
            _ws = ws;

            ws.Log += m => Log?.Invoke(m);
            ws.MessageReceived += OnRelayLine;
            ws.Closed += reason =>
            {
                // ⚠️ 必须确认"关掉的还是当前这条"。重连时会 CleanupTransport() 掉旧 socket，
                // 旧 socket 的 Closed 若延迟到达，会把刚建好的新连接又标成断线 —— 死循环。
                if (!ReferenceEquals(_ws, ws)) return;
                if (_stopping) return;
                IsConnected = false;
                ConnectionChanged?.Invoke(false, reason);
                BeginReconnect();
            };

            await ws.ConnectAsync(_settings.RelayHost, _settings.RelayWebSocketPort,
                                  _settings.RelayWebSocketPath, TimeSpan.FromSeconds(9));

            ws.SendText(joinLine + "\n");
            IsConnected = true;
        }

        private async Task ConnectTcpAsync(string joinLine)
        {
            Transport = "tcp";
            var tcp = new TcpClient { NoDelay = true };
            _tcp = tcp;

            var task = tcp.ConnectAsync(_settings.RelayHost, _settings.RelayTcpPort);
            var done = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(8)));
            if (done != task || !tcp.Connected)
                throw new TimeoutException($"连接 {_settings.RelayHost}:{_settings.RelayTcpPort} 超时");

            _stream = tcp.GetStream();
            var bytes = Encoding.UTF8.GetBytes(joinLine + "\n");
            await _stream.WriteAsync(bytes, 0, bytes.Length);
            await _stream.FlushAsync();

            IsConnected = true;
            _ = Task.Run(() => TcpReceiveLoopAsync(_cts!.Token));
        }

        /// <summary>
        /// 房主关房：请中继把整个房间真正关掉 —— 同房其他人会收到 <c>room-closed</c>，
        /// 连接随后被中继断开。
        ///
        /// ⚠️ 为什么不能只关本地：以前「关闭房间」只调了后端 HTTP（把房间从大厅列表里摘掉），
        /// 中继完全不知情 —— 中继里那个房间还在、别人的 socket 还挂着。房主一走，
        /// 其他人那边只会显示「房间里就剩我自己」，没有任何提示说要退房。
        ///
        /// 中继的关房契约是 <c>{"type":"close-room","by":房主昵称,"token":房间令牌}</c>，
        /// 且 <c>by</c> 必须与建房时那一个房主一致才认（relay-server.js 的 closeRoom）。
        /// 注意报文类型是 <c>close-room</c>（带连字符）—— 发成 <c>close</c> 中继不认识，
        /// 会被当成普通数据广播给同房的人，什么都不会发生。
        /// </summary>
        public bool CloseRoom(string reason = "")
        {
            if (!IsConnected) return false;

            // ⚠️ 必须先立 _roomClosed 再发：中继收到 close-room 之后会把房里**所有人**
            // 的 socket 直接 destroy（包括我们自己），Closed 事件马上就到。
            // 不先置这个标志的话，房主会立刻触发自动重连 —— 又把自己 join 回一个
            // 刚被关掉的房间，白折腾一圈。
            _roomClosed = true;

            try
            {
                SendLine(JsonSerializer.Serialize(new
                {
                    type = "close-room",
                    by = PlayerName,
                    token = "",
                    reason,
                }));
                Log?.Invoke("已通知中继关闭房间");
                return true;
            }
            catch (Exception ex)
            {
                Log?.Invoke("通知中继关房失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>离开房间并断开。</summary>
        public async Task LeaveAsync()
        {
            _stopping = true;
            StopWatchdog();
            var cts = _cts;
            try { cts?.Cancel(); } catch { }

            try
            {
                if (IsConnected)
                    SendLine(JsonSerializer.Serialize(new { type = "leave", roomId = CurrentRoomId }));
            }
            catch { }

            CleanupTransport();
            IsConnected = false;
            CurrentRoomId = "";
            _joinLine = "";
            cts?.Dispose();
            _cts = null;
            await Task.CompletedTask;
        }

        // ==================== 重连与存活看门狗 ====================

        private List<Func<Task>> BuildAttempts()
        {
            var attempts = new List<Func<Task>>();
            if (_settings.PreferWebSocket)
                attempts.Add(() => ConnectWebSocketAsync(_joinLine));
            attempts.Add(() => ConnectTcpAsync(_joinLine));
            if (!_settings.PreferWebSocket)
                attempts.Add(() => ConnectWebSocketAsync(_joinLine));
            return attempts;
        }

        private void StartWatchdog()
        {
            _lastInboundTicks = Environment.TickCount64;
            _watchdog ??= new Timer(_ => WatchdogTick(), null, Timeout.Infinite, Timeout.Infinite);
            try { _watchdog.Change(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20)); } catch { }
        }

        private void StopWatchdog()
        {
            try { _watchdog?.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
        }

        /// <summary>
        /// 存活看门狗。中继每 25 秒主动推一次 <c>ping</c>，正常情况下行不可能静默 75 秒。
        /// 拔网线、笔记本合盖、NAT 表项过期这些场景 TCP 不会立刻报错，SendLine 也照样
        /// "成功"（直到内核缓冲区写满），只有"发得出、收不到"这个特征能提前判死。
        /// </summary>
        private void WatchdogTick()
        {
            if (_stopping || _roomClosed || !IsConnected) return;

            long idle = Environment.TickCount64 - Interlocked.Read(ref _lastInboundTicks);
            if (idle <= 75000) return;

            Log?.Invoke($"中继 {idle / 1000} 秒无任何下行，判定链路已死，开始重连");
            IsConnected = false;
            CleanupTransport();
            ConnectionChanged?.Invoke(false, "中继无响应");
            BeginReconnect();
        }

        private void BeginReconnect()
        {
            if (_stopping || _roomClosed) return;
            if (string.IsNullOrEmpty(_joinLine) || _cts == null) return;
            if (Interlocked.CompareExchange(ref _reconnectRunning, 1, 0) != 0) return;
            _ = Task.Run(ReconnectLoopAsync);
        }

        /// <summary>
        /// 自动重连。退避 1→2→4→8→10 秒封顶，一直试到成功、用户退房或房间被关。
        /// 重连成功必须**重发 join**：中继是收到 join 才把这条 socket 挂进房间的，
        /// 光连上不 join 等于连了个寂寞（能发不能收，正是最坑的那种"看起来通了"）。
        /// </summary>
        private async Task ReconnectLoopAsync()
        {
            try
            {
                var cts = _cts;
                if (cts == null) return;

                int delayMs = 1000;
                while (!_stopping && !_roomClosed && !cts.IsCancellationRequested && !IsConnected)
                {
                    _reconnectAttempts++;
                    try { await Task.Delay(delayMs, cts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    if (_stopping || _roomClosed) return;

                    foreach (var attempt in BuildAttempts())
                    {
                        if (_stopping || _roomClosed) return;
                        try
                        {
                            CleanupTransport();
                            await attempt().ConfigureAwait(false);
                            if (!IsConnected) continue;

                            _reconnectAttempts = 0;
                            LastError = "";
                            StartWatchdog();
                            Log?.Invoke($"中继已自动重连（{Transport}）");
                            ConnectionChanged?.Invoke(true, "连接已恢复");
                            Reconnected?.Invoke(Transport);
                            return;
                        }
                        catch (Exception ex)
                        {
                            LastError = ex.Message;
                            CleanupTransport();
                        }
                    }

                    Log?.Invoke($"中继重连失败（第 {_reconnectAttempts} 次），{delayMs / 1000} 秒后重试");
                    delayMs = Math.Min(delayMs * 2, 10000);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _reconnectRunning, 0);
            }
        }

        private void CleanupTransport()
        {
            try { _ws?.Dispose(); } catch { }
            _ws = null;
            try { _stream?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
            _stream = null;
            _tcp = null;
            IsConnected = false;
        }

        /// <summary>发送一条已序列化的 JSON（自动补换行）。</summary>
        public void SendLine(string json)
        {
            if (string.IsNullOrEmpty(json) || !IsConnected) return;
            string line = json + "\n";
            try
            {
                if (_ws is { IsOpen: true })
                {
                    _ws.SendText(line);
                }
                else
                {
                    lock (_sendLock)
                    {
                        if (_stream == null) return;
                        var bytes = Encoding.UTF8.GetBytes(line);
                        _stream.Write(bytes, 0, bytes.Length);
                        _stream.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke("中继发送失败：" + ex.Message);
                // 写失败基本等于链路已死。这里直接交给重连，不用等看门狗到期。
                IsConnected = false;
                BeginReconnect();
            }
        }

        /// <summary>把游戏侧上来的整包原样转发给中继。</summary>
        public void ForwardToRelay(JsonElement gamePacket) => SendLine(gamePacket.GetRawText());

        /// <summary>取远程车缓存（游戏重连后补发）。</summary>
        public List<JsonElement> GetCachedVehicles()
        {
            lock (_cacheLock) return new List<JsonElement>(_vehicleCache.Values);
        }

        // ==================== 收 ====================

        private async Task TcpReceiveLoopAsync(CancellationToken token)
        {
            var buffer = new byte[65536];
            var pending = new StringBuilder();

            try
            {
                while (!token.IsCancellationRequested && _stream != null)
                {
                    int n = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (n == 0) break;
                    pending.Append(Encoding.UTF8.GetString(buffer, 0, n));

                    // ⚠️ 原来是 pending.ToString().IndexOf('\n')：每切一行就把整个缓冲
                    // 复制成一个新字符串。车辆包每秒几十条、缓冲上千字节时这叫 O(n^2)，
                    // 收包线程会被自己的分配拖住，表现出来就是"同步一卡一卡"。
                    int idx;
                    while ((idx = IndexOfNewline(pending)) >= 0)
                    {
                        string line = pending.ToString(0, idx).Trim();
                        pending.Remove(0, idx + 1);
                        if (line.Length > 0) OnRelayLine(line);
                    }

                    if (pending.Length > 1 << 20) pending.Clear();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!_stopping) Log?.Invoke("中继接收异常：" + ex.Message);
            }
            finally
            {
                if (!_stopping && !_roomClosed)
                {
                    IsConnected = false;
                    ConnectionChanged?.Invoke(false, "中继连接断开");
                    BeginReconnect();
                }
            }
        }

        /// <summary>StringBuilder 版查换行，避免每行一次整串复制。</summary>
        private static int IndexOfNewline(StringBuilder sb)
        {
            for (int i = 0; i < sb.Length; i++)
            {
                if (sb[i] == '\n') return i;
            }
            return -1;
        }

        /// <summary>
        /// 传输层送来的一批数据。中继按行分隔 JSON，但不同传输（WS 帧 / TCP 段）
        /// 可能把多条消息合并到一次回调里，所以这里再按行切一遍，逐行处理。
        /// </summary>
        private void OnRelayLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            // 任何一条下行（含中继的 ping）都说明链路是活的。
            Interlocked.Exchange(ref _lastInboundTicks, Environment.TickCount64);
            foreach (var chunk in line.Split('\n'))
            {
                var one = chunk.Trim();
                if (one.Length == 0) continue;
                HandleRelayMessage(one);
            }
        }

        private readonly HashSet<string> _seenTypes = new();

        private void HandleRelayMessage(string line)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var d = doc.RootElement;
                if (d.ValueKind != JsonValueKind.Object) return;
                if (!d.TryGetProperty("type", out var t)) return;

                string? typeName = t.GetString();

                // 首包诊断：每个类型第一次到达时记一条，排障时能看清链路到底通了哪些。
                if (typeName is { Length: > 0 } && _seenTypes.Add(typeName))
                    Log?.Invoke($"中继首包：{typeName}");

                if (typeName != "ping") Interlocked.Increment(ref FramesIn);

                switch (typeName)
                {
                    case "ping":
                        return;   // 心跳，忽略

                    case "vehicle":
                        if (d.TryGetProperty("id", out var idProp))
                        {
                            string id = idProp.GetString() ?? "";
                            if (id.Length > 0)
                            {
                                lock (_cacheLock) _vehicleCache[id] = d.Clone();
                            }
                        }
                        VehicleReceived?.Invoke(d.Clone());
                        break;

                    case "vehcfg":
                        VehCfgReceived?.Invoke(d.Clone());
                        break;

                    case "chat":
                        ChatReceived?.Invoke(d.Clone());
                        break;

                    case "players":
                        PlayersReceived?.Invoke(d.Clone());
                        break;

                    case "system":
                        SystemReceived?.Invoke(d.Clone());
                        break;

                    case "room-closed":
                        // 房主关房是"正常终局"，不是掉线 —— 不要再去重连把房间敲回来。
                        _roomClosed = true;
                        RoomClosed?.Invoke(d.Clone());
                        break;
                }
            }
            catch
            {
                // 单条脏数据不打断整条流
            }
        }

        public void Dispose()
        {
            _stopping = true;
            StopWatchdog();
            try { _cts?.Cancel(); } catch { }
            CleanupTransport();
            try { _watchdog?.Dispose(); } catch { }
            _watchdog = null;
            _cts?.Dispose();
        }
    }

    public sealed class RoomMeta
    {
        public string Host { get; set; } = "";
        public int Capacity { get; set; } = 8;
        public string RoomName { get; set; } = "";
        public string Map { get; set; } = "";
    }
}
