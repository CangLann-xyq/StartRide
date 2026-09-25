using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
    /// <summary>房间内的实时状态。</summary>
    public sealed class MultiplayerState
    {
        public bool Connected { get; set; }
        public bool GameConnected { get; set; }
        public string Transport { get; set; } = "";
        public string RoomId { get; set; } = "";
        public string RoomName { get; set; } = "";
        public string Host { get; set; } = "";
        public int PlayerCount { get; set; }
        public int Capacity { get; set; }
        public int RemoteVehicles { get; set; }
        public int VehiclePackets { get; set; }
        public string LastError { get; set; } = "";
        public List<string> Players { get; } = new();
    }

    /// <summary>
    /// 联机会话：把「游戏内模组 &lt;-&gt; 本地桥」和「中继」两头接起来。
    ///
    /// 数据流：
    ///   游戏(GE 扩展) --4444--> 本地桥 --> 中继(WS/7777) --> 别的玩家
    ///   别的玩家 --> 中继 --> 本地桥 --4444--> 游戏（生成远程车）
    ///
    /// 这里取代了旧的 MultiplayerManager —— 那个版本根本没接中继，
    /// 只用 HTTP 轮询房间和聊天，玩家数还是写死的，等于没有联机。
    /// </summary>
    public sealed class MultiplayerSession : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly LuaBridge _bridge;
        private readonly RelayClient _relay;

        public MultiplayerState State { get; } = new();

        public event Action<string>? Log;
        public event Action? StateChanged;
        /// <summary>需要在界面上冒泡提示的消息。</summary>
        public event Action<string, string>? Notice;   // (level, text)

        public MultiplayerSession(AppSettings settings)
        {
            _settings = settings;
            _bridge = new LuaBridge();
            _relay = new RelayClient(settings);

            _bridge.Log += m => Log?.Invoke(m);
            _relay.Log += m => Log?.Invoke(m);

            _bridge.GameReady += OnGameReady;
            _bridge.VehicleReceived += OnGameVehicle;
            _bridge.VehCfgReceived += p =>
            {
                Interlocked.Increment(ref _statGameIn);
                Interlocked.Increment(ref _statRelayOut);
                _relay.ForwardToRelay(p);
            };
            _bridge.ChatReceived += p =>
            {
                Interlocked.Increment(ref _statGameIn);
                Interlocked.Increment(ref _statRelayOut);
                _relay.ForwardToRelay(p);
            };
            _bridge.GameDisconnected += () =>
            {
                State.GameConnected = false;
                StateChanged?.Invoke();
            };

            _relay.ConnectionChanged += (ok, detail) =>
            {
                State.Connected = ok;
                State.Transport = _relay.Transport;
                State.LastError = ok ? "" : detail;
                PushRelayStateToGame();
                StateChanged?.Invoke();
            };
            _relay.VehicleReceived += OnRelayVehicle;
            _relay.VehCfgReceived += p => _bridge.SendJson(p.GetRawText());
            _relay.ChatReceived += p => _bridge.SendJson(p.GetRawText());
            _relay.SystemReceived += OnRelaySystem;
            _relay.RoomClosed += OnRoomClosed;
            _relay.PlayersReceived += OnPlayers;

            EnsureStatsTimer();
        }

        /// <summary>启动本地桥（进程启动时调用一次）。</summary>
        public void StartBridge() => _bridge.Start();

        public bool IsHost { get; private set; }

        // ==================== 会话控制 ====================

        public async Task<bool> CreateRoomAsync(Room room, string playerName)
        {
            IsHost = true;
            return await JoinCoreAsync(room, playerName);
        }

        public async Task<bool> JoinRoomAsync(Room room, string playerName)
        {
            IsHost = false;
            return await JoinCoreAsync(room, playerName);
        }

        private async Task<bool> JoinCoreAsync(Room room, string playerName)
        {
            string name = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName;

            var meta = new RoomMeta
            {
                Host = string.IsNullOrWhiteSpace(room.Host) ? name : room.Host,
                Capacity = room.Capacity,
                RoomName = room.Name,
                Map = room.Map,
            };

            // 只有真的连上中继才算进了房间。旧实现先把 RoomId 写上再连，
            // 结果连不上时界面照样显示"已在房间"，用户以为通了却看不到任何人。
            bool ok = await _relay.JoinAsync(room.Id, name, meta);

            State.Connected = ok;
            State.Transport = _relay.Transport;
            State.LastError = ok ? "" : _relay.LastError;

            if (ok)
            {
                State.RoomId = room.Id;
                State.RoomName = room.Name;
                State.Host = meta.Host;
                State.Capacity = room.Capacity;
                State.RemoteVehicles = 0;
                State.VehiclePackets = 0;
                _loggedFirstInboundVehicle = false;
                _loggedFirstOutboundVehicle = false;
                State.Players.Clear();
                State.Players.Add(name);
                State.PlayerCount = 1;
                PushRelayStateToGame();
                Log?.Invoke($"已加入房间 {room.Id}（{State.Transport}）");
            }
            else
            {
                State.RoomId = "";
                State.RoomName = "";
                State.Players.Clear();
                State.PlayerCount = 0;

                Notice?.Invoke("error",
                    "连不上联机中继（" + LastErrorText() + "）。请检查网络后重试。\n" +
                    "提示：中继走 80 端口的 WebSocket 隧道 " + _settings.RelayHost +
                    _settings.RelayWebSocketPath + "。");
            }

            StateChanged?.Invoke();
            return ok;
        }

        private string LastErrorText() =>
            string.IsNullOrWhiteSpace(State.LastError) ? "无响应" : State.LastError;

        public async Task LeaveRoomAsync()
        {
            await _relay.LeaveAsync();
            State.Connected = false;
            State.Players.Clear();
            State.PlayerCount = 0;
            State.RemoteVehicles = 0;
            State.RoomId = "";
            IsHost = false;
            PushRelayStateToGame();
            StateChanged?.Invoke();
        }

        // ==================== 游戏 -> 中继 ====================

        private void OnGameReady(string playerName, string version)
        {
            State.GameConnected = true;
            Log?.Invoke($"游戏内模组就绪：{playerName} (mod v{version})");

            PushRelayStateToGame();

            // 游戏侧重连后把远程车补发一遍，避免车辆凭空消失
            foreach (var v in _relay.GetCachedVehicles())
            {
                try { _bridge.SendJson(v.GetRawText()); } catch { }
            }
            StateChanged?.Invoke();
        }

        private void OnGameVehicle(JsonElement packet)
        {
            State.VehiclePackets++;
            Interlocked.Increment(ref _statGameIn);
            Interlocked.Increment(ref _statRelayOut);
            if (!_loggedFirstOutboundVehicle)
            {
                _loggedFirstOutboundVehicle = true;
                Log?.Invoke("本车数据已开始上发中继（首包）");
            }
            _relay.ForwardToRelay(packet);
            StateChanged?.Invoke();
        }

        private bool _loggedFirstOutboundVehicle;
        private bool _loggedFirstInboundVehicle;

        // ==================== 数据流统计 ====================
        //
        // 游戏里 F8 面板那一节「数据流（哪一项为 0 就是哪里断了）」就是靠这里推下去的。
        // 每个值是**最近一个统计周期的增量**，不是累计值——面板标题写的是「/10秒」。

        private const int StatsIntervalSeconds = 10;

        private long _statGameIn;          // 游戏 -> 本地桥
        private long _statRelayOut;        // 本地桥 -> 中继
        private long _statRelayVehicle;    // 中继 -> 本地桥 的车辆包
        private long _lastGameIn;
        private long _lastRelayOut;
        private long _lastRelayIn;
        private long _lastRelayVehicle;
        private Timer? _statsTimer;

        private void EnsureStatsTimer()
        {
            if (_statsTimer != null) return;
            _statsTimer = new Timer(_ => PushStats(), null,
                TimeSpan.FromSeconds(StatsIntervalSeconds),
                TimeSpan.FromSeconds(StatsIntervalSeconds));
        }

        private void PushStats()
        {
            try
            {
                long gameIn = Interlocked.Read(ref _statGameIn);
                long relayOut = Interlocked.Read(ref _statRelayOut);
                long relayIn = _relay.FramesIn;
                long relayVehicle = Interlocked.Read(ref _statRelayVehicle);

                long dGameIn = gameIn - _lastGameIn;
                long dRelayOut = relayOut - _lastRelayOut;
                long dRelayIn = relayIn - _lastRelayIn;
                long dRelayVehicle = relayVehicle - _lastRelayVehicle;

                _lastGameIn = gameIn;
                _lastRelayOut = relayOut;
                _lastRelayIn = relayIn;
                _lastRelayVehicle = relayVehicle;

                _bridge.Send(new
                {
                    type = "stats",
                    gameIn = (int)dGameIn,
                    relayOut = (int)dRelayOut,
                    relayIn = (int)dRelayIn,
                    relayVehicle = (int)dRelayVehicle,
                    relayConnected = _relay.IsConnected ? 1 : 0,
                });

                // 只有「人在房间里、游戏也在线」的那个会话才写日志。
                // AppState 里还有一个从不连游戏的空会话，不过滤就会每 10 秒刷一行全 0。
                if (State.RoomId.Length > 0 && _bridge.IsGameConnected)
                {
                    Log?.Invoke(
                        $"数据流/10s 本地上报={dGameIn} 转中继={dRelayOut} 中继下行={dRelayIn} 远程车包={dRelayVehicle} "
                        + $"| 累计 远程车包={relayVehicle} 远程车={State.RemoteVehicles} "
                        + $"桥={(State.GameConnected ? "已连接" : "未连接")} "
                        + $"中继={(_relay.IsConnected ? "已连接" : _relay.Transport)}");
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke("数据流统计失败：" + ex.Message);
            }
        }

        private void PushRelayStateToGame()
        {
            _bridge.Send(new
            {
                type = "relay-state",
                state = _relay.IsConnected ? "connected" : "disconnected",
                detail = _relay.LastError,
                roomId = _relay.CurrentRoomId,
            });
        }

        // ==================== 中继 -> 游戏 ====================

        private void OnRelayVehicle(JsonElement packet)
        {
            State.RemoteVehicles++;
            Interlocked.Increment(ref _statRelayVehicle);
            if (!_loggedFirstInboundVehicle)
            {
                _loggedFirstInboundVehicle = true;
                string who = packet.TryGetProperty("player", out var pn)
                    ? pn.GetString() ?? "?" : "?";
                Log?.Invoke($"已收到远程车辆数据（首个来自 {who}），远程车辆将同步给游戏");
            }
            _bridge.SendJson(packet.GetRawText());
            StateChanged?.Invoke();
        }

        private void OnRelaySystem(JsonElement packet)
        {
            string text = packet.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
            if (text.Length > 0) Notice?.Invoke("info", text);
            _bridge.SendJson(packet.GetRawText());
        }

        private void OnRoomClosed(JsonElement packet)
        {
            string text = packet.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
            Notice?.Invoke("warn", string.IsNullOrEmpty(text) ? "房主已关闭房间。" : text);
            _bridge.SendJson(packet.GetRawText());
            _ = LeaveRoomAsync();
        }

        private void OnPlayers(JsonElement packet)
        {
            State.Players.Clear();
            if (packet.TryGetProperty("players", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in arr.EnumerateArray())
                {
                    if (p.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } nm)
                        State.Players.Add(nm);
                }
            }
            if (packet.TryGetProperty("count", out var c) && c.TryGetInt32(out int cnt))
                State.PlayerCount = cnt;
            else
                State.PlayerCount = State.Players.Count;

            if (packet.TryGetProperty("capacity", out var cap) && cap.TryGetInt32(out int cp))
                State.Capacity = cp;
            if (packet.TryGetProperty("host", out var h) && h.GetString() is { Length: > 0 } hv)
                State.Host = hv;
            if (packet.TryGetProperty("roomName", out var rn) && rn.GetString() is { Length: > 0 } rv)
                State.RoomName = rv;

            _bridge.SendJson(packet.GetRawText());
            StateChanged?.Invoke();
        }

        public void Dispose()
        {
            try { _statsTimer?.Dispose(); } catch { }
            _bridge.Dispose();
            _relay.Dispose();
        }
    }
}
