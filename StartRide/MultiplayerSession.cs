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

        /// <summary>本房玩法模式 id（房主那份权威，加入者来自中继快照）。</summary>
        public string GameMode { get; set; } = "";

        /// <summary>玩法规则版本号，用来发现对端是旧版规则。</summary>
        public int GameModeRevision { get; set; } = 1;

        public string LastError { get; set; } = "";
        public List<string> Players { get; } = new();
    }

    public sealed class MultiplayerSession : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly LuaBridge _bridge;
        private readonly RelayClient _relay;

        public MultiplayerState State { get; } = new();

        // ── State.Players 的并发保护 ────────────────────────────────────────────
        // 它是整个 MultiplayerState 里**唯一**会被「中继接收线程原地改写、
        // 同时又被别的线程读」的集合。List<T> 不支持这种用法：
        //   写侧 Clear()/Add() 与读侧 new List<string>(...)、JsonSerializer 序列化
        //   撞在一起会抛 ArgumentException（源数组不够长）或
        //   InvalidOperationException（集合已被修改）。
        // 读侧统一走 CopyPlayers()，写侧统一走 ReplacePlayers()。
        private readonly object _playersLock = new();

        /// <summary>玩家名单的线程安全快照。UI / 落盘一律用这个，别直接枚举 State.Players。</summary>
        public List<string> CopyPlayers()
        {
            lock (_playersLock) return new List<string>(State.Players);
        }

        private void ReplacePlayers(IEnumerable<string>? names)
        {
            lock (_playersLock)
            {
                State.Players.Clear();
                if (names != null) State.Players.AddRange(names);
            }
        }

        public event Action<string>? Log;
        public event Action? StateChanged;
        public event Action<string, string>? Notice;

        /// <summary>
        /// 房主解散了房间（中继广播 room-closed）。
        ///
        /// 与 <see cref="Notice"/> 分开是**故意**的：提示只是给人看的一句话，
        /// 而这是启动器必须立刻收尾的**明确结束信号** —— 要退出房间、
        /// 并把隔离掉的第三方模组还给玩家，不能等断线宽限那 30 秒
        /// （宽限期间中继恢复一次就会被取消，房间永远不结束）。
        /// </summary>
        public event Action? RoomClosed;

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

            // 玩法同步包（mode-phase / mode-sync / derby-damage / hide-seek-* …）
            // 走这条通用出口。以前它们死在 LuaBridge 的 default 分支里，
            // 跨机器联机只有车位置能通 —— 见 LuaBridge.GamePacket 的注释。
            _bridge.GamePacket += p =>
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

            // 玩法同步包（mode-phase / mode-sync / derby-damage / hide-seek-* …）
            // 走这条通用出口送进本地桥。以前它们死在 RelayClient 的 switch 里
            // （没有 default 分支），跨机器联机只有车位置能通 ——
            // 见 RelayClient.GamePacket 的注释。
            _relay.GamePacket += p =>
            {
                Interlocked.Increment(ref _statGameOut);
                try { _bridge.SendJson(p.GetRawText()); } catch { }
            };

            EnsureStatsTimer();
        }

        public void StartBridge() => _bridge.Start();

        public bool IsHost { get; private set; }

        /// <summary>
        /// 房主让人数上限生效：走已有隧道发一条 set-capacity，由中继改房间元数据并广播新快照。
        /// 为什么不用 HTTP 重开会话：改上限要作用在**当前这条**房间连接上，
        /// 重新 join 会换掉 playerId，把房主自己的车踢出房间。
        /// </summary>
        public bool SetCapacity(int capacity)
        {
            if (!IsHost || !_relay.IsConnected) return false;

            int clamped = Math.Max(2, Math.Min(16, capacity));
            try
            {
                _relay.SendLine(JsonSerializer.Serialize(new
                {
                    type = "set-capacity",
                    capacity = clamped,
                }));
                State.Capacity = clamped;
                StateChanged?.Invoke();
                Log?.Invoke($"已请求把房间人数上限改为 {clamped}");
                return true;
            }
            catch (Exception ex)
            {
                Log?.Invoke("修改人数上限失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>本房要进的关卡 id（房主 = 自己选的，加入者 = 房主那张图）。</summary>
        public string MapId { get; private set; } = "";

        /// <summary>
        /// 本房玩法模式 id。
        /// 房主建房时定下并随房间元数据广播；加入者从中继快照读回，
        /// 所以两边拿到的永远是同一个值（而不是各自本机记住的那个）。
        /// </summary>
        public string GameModeId { get; private set; } = "";

        /// <summary>
        /// 房主改玩法。走已有隧道发 set-game-mode，由中继改房间元数据并广播新快照 ——
        /// 和人数上限同一套路数：不改连接、不重开房间、也不用把玩家踢出房。
        /// </summary>
        public bool SetGameMode(string modeId)
        {
            if (!IsHost || !_relay.IsConnected) return false;

            string normalized = LobbyGameModeCatalog.Normalize(modeId).Id;
            int revision = LobbyGameModeCatalog.Normalize(modeId).Revision;

            try
            {
                _relay.SendLine(JsonSerializer.Serialize(new
                {
                    type = "set-game-mode",
                    gameMode = normalized,
                    gameModeRevision = revision,
                }));
                State.GameMode = normalized;
                State.GameModeRevision = revision;
                GameModeId = normalized;
                PushGameModeToGame();
                StateChanged?.Invoke();
                Log?.Invoke($"已请求把房间玩法改为「{LobbyGameModeCatalog.DisplayName(normalized)}」");
                return true;
            }
            catch (Exception ex)
            {
                Log?.Invoke("修改玩法失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>本机的出生点对象名。房主和加入者各选各的，所以它不进中继、只发给本地模组。</summary>
        public string SpawnPoint { get; private set; } = "";

        /// <summary>
        /// 设定本房的地图与本人出生点，并立刻推给游戏侧模组。
        /// 为什么走启动器↔模组这条桥、而不走中继：出生点是「每个人自己的」，
        /// 房主落哪跟加入者无关，塞进中继/数据库只会多一次迁移。
        /// 玩法不一样：它是**全房共享**的，所以既进房间元数据、也推给本地模组。
        /// </summary>
        public void ConfigureRoom(string? mapId, string? spawnPoint, string? gameMode = null, bool push = true)
        {
            MapId = (mapId ?? "").Trim();
            SpawnPoint = (spawnPoint ?? "").Trim();
            if (gameMode != null) GameModeId = LobbyGameModeCatalog.Normalize(gameMode).Id;
            if (push) PushRoomConfigToGame();
        }

        private void PushRoomConfigToGame()
        {
            if (MapId.Length == 0 && SpawnPoint.Length == 0 && GameModeId.Length == 0) return;

            _bridge.Send(new
            {
                type = "room-config",
                map = MapId,
                spawnPoint = SpawnPoint,
                gameMode = GameModeId,
                roomId = State.RoomId,
                roomName = State.RoomName,
            });
        }

        /// <summary>
        /// 玩法变了单独推一条，不必连带重发地图/出生点
        /// （重发 room-config 会让模组把出生点计划重置一遍，玩家被重复传送一次）。
        /// </summary>
        private void PushGameModeToGame()
        {
            _bridge.Send(new
            {
                type = "game-mode",
                gameMode = GameModeId,
            });
        }

        /// <summary>
        /// 把本房的地图/出生点写到游戏 userpath 下的 startride/multiplayer.json。
        /// 用途：游戏还没连上本地桥、扩展刚加载的那一瞬间也能读到，少一次时序博弈。
        /// （模组 onExtensionLoaded 里读的就是这个文件；桥消息负责后续刷新。）
        /// </summary>
        public static void WriteGameBootstrap(string playerName, string roomId, string roomName,
            string mapId, string spawnPoint, string gameMode = "")
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
                    gameMode = LobbyGameModeCatalog.Normalize(gameMode).Id,
                    gameModeRevision = LobbyGameModeCatalog.Normalize(gameMode).Revision,
                    // 车顶悬浮名牌（昵称 + 距离）：模组每帧读这两项决定画不画、画多远。
                    nameTagEnabled = AppSettings.Current.NameTagEnabled,
                    nameTagMaxDistance = AppSettings.Current.NameTagMaxDistance,
                    // 玩法规则（警匪 / 德比 / 捉迷藏）：模组 startrideMode.lua 读前两项、
                    // startrideHideSeek.lua 读后四项。
                    modeRules = new
                    {
                        captureHoldMs = AppSettings.Current.CaptureHoldMs,
                        captureStillSpeed = AppSettings.Current.CaptureStillSpeed,
                        derbyDamageLimit = AppSettings.Current.DerbyDamageLimit,
                        resetLimit = AppSettings.Current.ResetLimit,
                        resetCooldownMs = AppSettings.Current.ResetCooldownMs,
                        roundSeconds = AppSettings.Current.RoundSeconds,
                        hideSeconds = AppSettings.Current.HideSeconds,
                        hideRoundSeconds = AppSettings.Current.HideRoundSeconds,
                        findRadius = AppSettings.Current.FindRadius,
                        findHoldMs = AppSettings.Current.FindHoldMs,
                    },
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

            // 人数上限只由房主那一份决定。加入者必须原样带上房主的容量 ——
            // 中继只在「这条连接被认作房主」时才接受 capacity 覆盖，
            // 所以加入者带上它不会污染房主的设置，反而能让自己的 UI 立刻显示正确的 x/y。
            int capacity = room.Capacity > 0 ? room.Capacity : 8;

            var meta = new RoomMeta
            {
                Host = string.IsNullOrWhiteSpace(room.Host)
                    ? (IsHost ? name : "")
                    : room.Host,
                Capacity = capacity,
                RoomName = room.Name,
                Map = room.Map,

                // 房主：带上自己选的那份玩法，中继会把首次出现的那份当作权威值。
                // 加入者：带上从大厅读到的房主玩法，只为了让 UI 立刻显示正确的值 ——
                // 中继只在房主那条连接上接受覆盖，所以不会改写房主的设定。
                GameMode = LobbyGameModeCatalog.Normalize(room.GameMode).Id,
                GameModeRevision = LobbyGameModeCatalog.Normalize(room.GameMode).Revision,
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
                State.Capacity = capacity;
                State.GameMode = meta.GameMode;
                State.GameModeRevision = meta.GameModeRevision;
                GameModeId = meta.GameMode;
                State.RemoteVehicles = 0;
                State.VehiclePackets = 0;
                _loggedFirstInboundVehicle = false;
                _loggedFirstOutboundVehicle = false;
                ReplacePlayers(new[] { name });
                State.PlayerCount = 1;
                PushRelayStateToGame();
                PushRoomConfigToGame();
                WriteGameBootstrap(name, State.RoomId, State.RoomName, MapId, SpawnPoint, GameModeId);
                Log?.Invoke($"已加入房间 {room.Id}（{State.Transport}）"
                    + (MapId.Length > 0 ? $"，地图 {MapId}" : "")
                    + (GameModeId.Length > 0 ? $"，玩法 {LobbyGameModeCatalog.DisplayName(GameModeId)}" : "")
                    + (SpawnPoint.Length > 0 ? $"，出生点 {SpawnPoint}" : ""));
            }
            else
            {
                State.RoomId = "";
                State.RoomName = "";
                ReplacePlayers(null);
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
            ReplacePlayers(null);
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
        private long _statGameOut;
        private long _lastGameIn;
        private long _lastRelayOut;
        private long _lastRelayIn;
        private long _lastRelayVehicle;
        private long _lastGameOut;
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
                long gameOut = Interlocked.Read(ref _statGameOut);

                long dGameIn = gameIn - _lastGameIn;
                long dRelayOut = relayOut - _lastRelayOut;
                long dRelayIn = relayIn - _lastRelayIn;
                long dRelayVehicle = relayVehicle - _lastRelayVehicle;
                long dGameOut = gameOut - _lastGameOut;

                _lastGameIn = gameIn;
                _lastRelayOut = relayOut;
                _lastRelayIn = relayIn;
                _lastRelayVehicle = relayVehicle;
                _lastGameOut = gameOut;

                _bridge.Send(new
                {
                    type = "stats",
                    gameIn = (int)dGameIn,
                    relayOut = (int)dRelayOut,
                    relayIn = (int)dRelayIn,
                    relayVehicle = (int)dRelayVehicle,
                    gameOut = (int)dGameOut,
                    relayConnected = _relay.IsConnected ? 1 : 0,
                });

                if (State.RoomId.Length > 0 && _bridge.IsGameConnected)
                {
                    Log?.Invoke(
                        $"数据流/10s 本地上报={dGameIn} 转中继={dRelayOut} 中继下行={dRelayIn} 远程车包={dRelayVehicle} 玩法下行={dGameOut} "
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
            // 先通知启动器「房间被解散了」，让它立刻退出并恢复模组；
            // 再离开桥。顺序不能反：LeaveRoomAsync 之后状态就断了，
            // 启动器那边只会看到一次普通断线，判不出是「房主解散」。
            RoomClosed?.Invoke();
            _ = LeaveRoomAsync();
        }

        private void OnPlayers(JsonElement packet)
        {
            // ⚠️ 先在本线程攒好，再锁内一次性换掉。
            //    原来这里是「直接 State.Players.Clear() 然后逐个 Add」—— 而别的线程
            //    （UI 线程点「改人数上限」、启动器桥线程掉线）会同时走
            //    OnSessionStateChanged → Publish/WriteStateFile 去**枚举**这个 List。
            //    List<T> 并发枚举会抛 ArgumentException / InvalidOperationException，
            //    表现为大厅面板偶发崩，且难复现。
            var names = new List<string>();
            if (packet.TryGetProperty("players", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in arr.EnumerateArray())
                {
                    if (p.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } nm)
                        names.Add(nm);
                }
            }
            ReplacePlayers(names);

            if (packet.TryGetProperty("count", out var c) && c.TryGetInt32(out int cnt))
                State.PlayerCount = cnt;
            else
                State.PlayerCount = names.Count;

            if (packet.TryGetProperty("capacity", out var cap) && cap.TryGetInt32(out int cp))
                State.Capacity = cp;

            // 玩法以中继快照为准：房主改完，全房这一份都会跟着变。
            // ⚠️ 只在拿到非空值时覆盖本机值 —— 老中继不认 gameMode 字段就不会回传它，
            //    这时必须保留本机已定的玩法，否则加入者会被"清空"成默认玩法。
            if (packet.TryGetProperty("gameMode", out var gm) && gm.GetString() is { Length: > 0 } gmv)
            {
                string normalized = LobbyGameModeCatalog.Normalize(gmv).Id;
                if (!string.Equals(State.GameMode, normalized, StringComparison.Ordinal))
                {
                    State.GameMode = normalized;
                    GameModeId = normalized;
                    PushGameModeToGame();
                }
            }
            if (packet.TryGetProperty("gameModeRevision", out var gr) && gr.TryGetInt32(out int grv)
                && grv > 0)
            {
                State.GameModeRevision = grv;
            }

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
