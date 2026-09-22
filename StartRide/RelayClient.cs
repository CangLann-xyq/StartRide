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

        /// <summary>远程车辆缓存：游戏侧重连时补发，避免车辆凭空消失。</summary>
        private readonly Dictionary<string, JsonElement> _vehicleCache = new();
        private readonly object _cacheLock = new();

        public bool IsConnected { get; private set; }
        public string Transport { get; private set; } = "";
        public string CurrentRoomId { get; private set; } = "";
        public string LastError { get; private set; } = "";

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
            _cts = new CancellationTokenSource();
            CurrentRoomId = roomId;
            _seenTypes.Clear();

            string join = JsonSerializer.Serialize(new
            {
                type = "join",
                roomId,
                playerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName,
                playerId = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName,
                host = meta.Host,
                capacity = meta.Capacity,
                roomName = meta.RoomName,
                token,
            });

            var attempts = new List<Func<Task>>();

            if (_settings.PreferWebSocket)
                attempts.Add(() => ConnectWebSocketAsync(join));
            attempts.Add(() => ConnectTcpAsync(join));
            if (!_settings.PreferWebSocket)
                attempts.Add(() => ConnectWebSocketAsync(join));

            foreach (var attempt in attempts)
            {
                try
                {
                    await attempt();
                    if (!IsConnected) continue;

                    Log?.Invoke($"中继已连接（{Transport}）");
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
                if (_stopping) return;
                IsConnected = false;
                ConnectionChanged?.Invoke(false, reason);
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

        /// <summary>离开房间并断开。</summary>
        public async Task LeaveAsync()
        {
            _stopping = true;
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
            cts?.Dispose();
            _cts = null;
            await Task.CompletedTask;
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

                    int idx;
                    while ((idx = pending.ToString().IndexOf('\n')) >= 0)
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
                if (!_stopping)
                {
                    IsConnected = false;
                    ConnectionChanged?.Invoke(false, "中继连接断开");
                }
            }
        }

        /// <summary>
        /// 传输层送来的一批数据。中继按行分隔 JSON，但不同传输（WS 帧 / TCP 段）
        /// 可能把多条消息合并到一次回调里，所以这里再按行切一遍，逐行处理。
        /// </summary>
        private void OnRelayLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
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
            try { _cts?.Cancel(); } catch { }
            CleanupTransport();
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
