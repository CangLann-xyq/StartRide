using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
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

    public sealed class MultiplayerSession : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly LuaBridge _bridge;
        private readonly RelayClient _relay;

        public MultiplayerState State { get; } = new();

        public event Action<string>? Log;
        public event Action? StateChanged;
        public event Action<string, string>? Notice;

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

            _relay.Reconnected += _ =>
            {
                Log?.Invoke("中继已自动重连，正在把房间状态与远程车辆同步回游戏");
                State.Connected = true;
                State.LastError = "";
                PushRelayStateToGame();
                PushRoomConfigToGame();
                foreach (var v in _relay.GetCachedVehicles())
                {
                    try { _bridge.SendJson(v.GetRawText()); } catch { }
                }
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

        public void StartBridge() => _bridge.Start();

        public bool IsHost { get; private set; }

        /// <summary>本房要进的关卡 id（房主 = 自己选的，加入者 = 房主那张图）。</summary>
        public string MapId { get; private set; } = "";

        /// <summary>本机的出生点对象名。房主和加入者各选各的，所以它不进中继、只发给本地模组。</summary>
        public string SpawnPoint { get; private set; } = "";

        /// <summary>
        /// 设定本房的地图与本人出生点，并立刻推给游戏侧模组。
        /// 为什么走启动器↔模组这条桥、而不走中继：出生点是「每个人自己的」，
        /// 房主落哪跟加入者无关，塞进中继/数据库只会多一次迁移。
        /// </summary>
        public void ConfigureRoom(string? mapId, string? spawnPoint, bool push = true)
        {
            MapId = (mapId ?? "").Trim();
            SpawnPoint = (spawnPoint ?? "").Trim();
            if (push) PushRoomConfigToGame();
        }

        private void PushRoomConfigToGame()
        {
            if (MapId.Length == 0 && SpawnPoint.Length == 0) return;

            _bridge.Send(new
            {
                type = "room-config",
                map = MapId,
                spawnPoint = SpawnPoint,
                roomId = State.RoomId,
                roomName = State.RoomName,
            });
        }

        /// <summary>
        /// 把本房的地图/出生点写到游戏 userpath 下的 startride/multiplayer.json。
        /// 用途：游戏还没连上本地桥、扩展刚加载的那一瞬间也能读到，少一次时序博弈。
        /// （模组 onExtensionLoaded 里读的就是这个文件；桥消息负责后续刷新。）
        /// </summary>
        public static void WriteGameBootstrap(string playerName, string roomId, string roomName,
            string mapId, string spawnPoint)
        {
            try
            {
                // 必须落在游戏真正读的那个 userpath 上（见 AppSettings.ResolveGameUserPathRoot），
                // 否则模组 jsonReadFile('startride/multiplayer.json') 永远读不到。
                string dir = Path.Combine(AppSettings.ResolveGameUserPathRoot(), "startride");
                Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(new
                {
                    playerName = playerName ?? "",
                    roomId = roomId ?? "",
                    roomName = roomName ?? "",
                    map = mapId ?? "",
                    spawnPoint = spawnPoint ?? "",
                    writtenAt = DateTimeOffset.Now.ToString("o"),
                }, new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(Path.Combine(dir, "multiplayer.json"), json);
            }
            catch
            {
            }
        }

        /// <summary>退出房间后别再让模组以为还在房里。</summary>
        public static void ClearGameBootstrap()
        {
            try
            {
                string file = Path.Combine(AppSettings.ResolveGameUserPathRoot(), "startride", "multiplayer.json");
                if (File.Exists(file)) File.Delete(file);
            }
            catch
            {
            }
        }

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
                Host = string.IsNullOrWhiteSpace(room.Host)
                    ? (IsHost ? name : "")
                    : room.Host,
                Capacity = room.Capacity,
                RoomName = room.Name,
                Map = room.Map,
            };

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
                PushRoomConfigToGame();
                WriteGameBootstrap(name, State.RoomId, State.RoomName, MapId, SpawnPoint);
                Log?.Invoke($"已加入房间 {room.Id}（{State.Transport}）"
                    + (MapId.Length > 0 ? $"，地图 {MapId}" : "")
                    + (SpawnPoint.Length > 0 ? $"，出生点 {SpawnPoint}" : ""));
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

        public void CloseRoomOnRelay(string reason = "房主已关闭房间") => _relay.CloseRoom(reason);

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
            ClearGameBootstrap();
            StateChanged?.Invoke();
        }

        private void OnGameReady(string playerName, string version)
        {
            State.GameConnected = true;
            Log?.Invoke($"游戏内模组就绪：{playerName} (mod v{version})");

            PushRelayStateToGame();
            PushRoomConfigToGame();

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

        private const int StatsIntervalSeconds = 10;

        private long _statGameIn;
        private long _statRelayOut;
        private long _statRelayVehicle;
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

                playerId = _relay.PlayerId,
                playerName = _relay.PlayerName,
            });
        }

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
