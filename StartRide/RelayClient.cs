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

    public sealed class RelayClient : IDisposable
    {
        private readonly AppSettings _settings;

        private TcpClient? _tcp;
        private NetworkStream? _stream;
        private WebSocketTransport? _ws;
        private CancellationTokenSource? _cts;

        private readonly object _sendLock = new();
        private bool _stopping;

        private string _joinLine = "";
        private volatile bool _roomClosed;
        private int _reconnectRunning;

        private long _lastInboundTicks;
        private Timer? _watchdog;
        private int _reconnectAttempts;

        private readonly Dictionary<string, JsonElement> _vehicleCache = new();
        private readonly object _cacheLock = new();

        public bool IsConnected { get; private set; }
        public string Transport { get; private set; } = "";
        public string CurrentRoomId { get; private set; } = "";
        public string LastError { get; private set; } = "";

        public string PlayerId { get; private set; } = "";

        public string PlayerName { get; private set; } = "";

        public event Action<string>? Reconnected;

        public long FramesIn;

        public event Action<string>? Log;
        public event Action<bool, string>? ConnectionChanged;
        public event Action<JsonElement>? VehicleReceived;
        public event Action<JsonElement>? VehCfgReceived;
        public event Action<JsonElement>? ChatReceived;
        public event Action<JsonElement>? PlayersReceived;
        public event Action<JsonElement>? SystemReceived;
        public event Action<JsonElement>? RoomClosed;

        public RelayClient(AppSettings settings) => _settings = settings;

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

            // ⚠️ 这里必须**每个进程唯一**，不能只用昵称派生。
            // StartRidePlayerId.Create 是「昵称的确定性哈希」，昵称一样 → ID 一样。
            // 两个都用默认昵称（或碰巧重名）的玩家会拿到相同的 ID，
            // 而游戏内模组是用 id 来过滤「自己发的包」的 → 双方互相丢对方的包，
            // 表现就是「联机时看不见对方 / 远程车包=0」。线上内测正是这个症状。
            // 所以：昵称只作为可读前缀，另叠一个本机安装+本会话都唯一的短尾巴。
            PlayerId = BuildUniquePlayerId(safeName);

            _joinLine = JsonSerializer.Serialize(new
            {
                type = "join",
                roomId,
                playerName = safeName,
                playerId = PlayerId,
                host = meta.Host,
                capacity = meta.Capacity,
                roomName = meta.RoomName,

                version = BuildInfo.Version,
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

        // 本机安装标识：第一次用到时生成一次并持久化，之后固定。
        // 它不是「账号」，只用来区分「同一台机器之外的另一个人」，重名玩家不会撞车。
        private static readonly object _installKeyLock = new();
        private static string? _installKey;

        private static string InstallKey()
        {
            lock (_installKeyLock)
            {
                if (_installKey != null) return _installKey;
                try
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StartRide");
                    Directory.CreateDirectory(dir);
                    string file = Path.Combine(dir, "machine.key");
                    if (File.Exists(file))
                    {
                        string existing = File.ReadAllText(file).Trim();
                        if (existing.Length >= 8) { _installKey = existing; return existing; }
                    }
                    string fresh = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
                    File.WriteAllText(file, fresh);
                    _installKey = fresh;
                    return fresh;
                }
                catch
                {
                    // 落盘失败也得唯一：退化成进程级随机，至少本次不撞车
                    _installKey = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
                    return _installKey;
                }
            }
        }

        // 每次进房一个随机 nonce：同一台机器连开两局也不会被当成同一个人。
        private static string NewSessionNonce() =>
            Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();

        /// <summary>
        /// 生成「人可读 + 机器唯一」的玩家 ID：
        ///   SR-&lt;昵称哈希&gt;-&lt;本机key&gt;-&lt;本次会话nonce&gt;
        /// 昵称哈希保住可读性与「同一个人认得出自己」；后两段保证任何情况下都不会撞。
        /// </summary>
        private static string BuildUniquePlayerId(string displayName)
        {
            string baseId = StartRidePlayerId.Create(displayName);
            // SR-XXXX-XXXX-XXXX → 取中间三段拼成紧凑体
            string body = baseId.StartsWith(StartRidePlayerId.Prefix, StringComparison.Ordinal)
                ? baseId.Substring(StartRidePlayerId.Prefix.Length)
                : baseId;
            return StartRidePlayerId.Prefix + body + "-" + InstallKey() + "-" + NewSessionNonce();
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

        public bool CloseRoom(string reason = "")
        {
            if (!IsConnected) return false;

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

        private void WatchdogTick()
        {
            if (_stopping || _roomClosed || !IsConnected) return;

            long idle = Environment.TickCount64 - Interlocked.Read(ref _lastInboundTicks);

            // WebSocket 隧道另有一份「收到任何字节」的时间戳（含控制帧 pong）。
            // 服务端每 25s 发一次文本 ping，正常时这份时间戳总是很新；
            // 一旦隧道被 nginx 静默半开（读不到 EOF、写不报错），两份都不再更新 ——
            // 这时要尽快判定死并重连，而不是憋到 75s 让玩家以为"还连着"却互看不见。
            long wsIdle = long.MaxValue;
            try
            {
                var ws = _ws;
                if (ws is { IsOpen: true })
                {
                    long ticks = ws.LastInboundUtcTicks;
                    if (ticks > 0)
                    {
                        long wsIdleMs = (long)(DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalMilliseconds;
                        if (wsIdleMs >= 0) wsIdle = wsIdleMs;
                    }
                }
            }
            catch { }

            long worst = Math.Min(idle, wsIdle);

            // 35s：比服务端 25s 保活留一次丢包余量，又远小于旧版的 75s。
            if (worst <= 35000) return;

            Log?.Invoke($"中继 {worst / 1000} 秒无任何下行，判定链路已死，开始重连");
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
                IsConnected = false;
                BeginReconnect();
            }
        }

        public void ForwardToRelay(JsonElement gamePacket) => SendLine(gamePacket.GetRawText());

        public List<JsonElement> GetCachedVehicles()
        {
            lock (_cacheLock) return new List<JsonElement>(_vehicleCache.Values);
        }

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

        private static int IndexOfNewline(StringBuilder sb)
        {
            for (int i = 0; i < sb.Length; i++)
            {
                if (sb[i] == '\n') return i;
            }
            return -1;
        }

        private void OnRelayLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
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

                if (typeName is { Length: > 0 } && _seenTypes.Add(typeName))
                    Log?.Invoke($"中继首包：{typeName}");

                if (typeName != "ping") Interlocked.Increment(ref FramesIn);

                switch (typeName)
                {
                    case "ping":
                        return;

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
                        _roomClosed = true;
                        RoomClosed?.Invoke(d.Clone());
                        break;
                }
            }
            catch
            {
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
