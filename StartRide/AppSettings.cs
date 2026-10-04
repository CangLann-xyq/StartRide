using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StartRide.Core
{
    public sealed class AppSettings
    {
        public string GameDirectory { get; set; } = "";
        public string LauncherLogDirectory { get; set; } = "";
        public bool DiagnosticLog { get; set; }

        public bool AutoDetectGame { get; set; } = true;

        public bool AutoInstallMod { get; set; } = true;

        public bool RemoveModOnLeave { get; set; } = true;

        /// <summary>
        /// 联机时是否临时隔离第三方联机模组（BeamMP / BeamLink 等）。
        ///
        /// 开着时：进联机前把 mods/db.json 里非 StartRide 的模组 active 置 false，
        /// 退出游戏后按日志原样恢复 —— 只改 active 字段，绝不动模组文件。
        /// 关掉时：行为和以前一样，只由游戏内模组检测并弹窗提醒。
        /// </summary>
        public bool IsolateConflictingMods { get; set; } = true;

        /// <summary>
        /// 联机时是否在每个远端玩家的车顶显示悬浮名牌（昵称 + 距离）。
        ///
        /// 由 Lua 模组在游戏内绘制；启动器只负责把开关和显示距离写进
        /// <userpath>/startride/multiplayer.json，模组每帧读配置决定画不画。
        /// </summary>
        public bool NameTagEnabled { get; set; } = true;

        /// <summary>
        /// 悬浮名牌的最远显示距离（米）。超出后不再绘制，接近时逐渐淡出。
        /// </summary>
        public int NameTagMaxDistance { get; set; } = 300;

        // ------------------------------------------------------------------
        // 玩法规则（警匪追逐 / 德比 / 捉迷藏）。由 Lua 玩法模块读取，启动器只负责写入。
        // ------------------------------------------------------------------

        /// <summary>
        /// 警匪追逐：警察要贴住强盗多少毫秒才算抓住。
        /// </summary>
        public int CaptureHoldMs { get; set; } = 5000;

        /// <summary>
        /// 警匪追逐：强盗速度低于这个值（米/秒）才算「近乎静止」。
        /// 抓捕需要「警察贴近」+「强盗近乎静止」两个条件同时成立，
        /// 否则强盗高速路过警察身边也会涨抓捕进度。
        /// </summary>
        public int CaptureStillSpeed { get; set; } = 8;

        /// <summary>
        /// 德比：车辆累计损伤达到这个值就淘汰。
        /// </summary>
        public int DerbyDamageLimit { get; set; } = 8000;

        /// <summary>
        /// 德比：每局允许的原地复位次数。0 = 不允许复位。
        /// </summary>
        public int ResetLimit { get; set; } = 3;

        /// <summary>
        /// 德比：两次原地复位之间的冷却（毫秒）。
        /// </summary>
        public int ResetCooldownMs { get; set; } = 10000;

        /// <summary>
        /// 捉迷藏：躲藏期时长（秒）。这段时间搜索者被冻结，躲藏者四散藏好。
        /// </summary>
        public int HideSeconds { get; set; } = 30;

        /// <summary>
        /// 捉迷藏：搜索期时长（秒）。归零时还有人没被找到 → 躲藏方获胜。
        /// </summary>
        public int HideRoundSeconds { get; set; } = 240;

        /// <summary>
        /// 捉迷藏：发现半径（米）。搜索者的车进入躲藏者这个范围内开始计时。
        /// </summary>
        public int FindRadius { get; set; } = 12;

        /// <summary>
        /// 捉迷藏：搜索者要贴住躲藏者多少毫秒才算找到。
        /// </summary>
        public int FindHoldMs { get; set; } = 2000;

        [JsonIgnore]
        public bool PreInstallMod => AutoInstallMod && !RemoveModOnLeave;

        public bool AutoAllowFirewall { get; set; } = true;
        public bool MinimizeToTray { get; set; }
        public int MaxMemoryMB { get; set; } = 4096;

        public bool LimitGameMemory { get; set; } = true;
        public string ExtraLaunchArgs { get; set; } = "";
        public bool AutoCheckVehicleMods { get; set; } = true;
        public bool ForceHighPerformanceGpu { get; set; } = true;

        public bool AutoRepairGameConfig { get; set; } = true;

        public bool HighlightCaptureEnabled { get; set; } = true;

        public bool HighlightAutoRecord { get; set; } = true;

        public bool HighlightInSinglePlayer { get; set; }

        public int HighlightSegmentMinutes { get; set; } = 5;

        public bool CheckFilesBeforeLaunch { get; set; } = true;

        public string PreLaunchCommand { get; set; } = "";

        public bool WaitForPreLaunchCommand { get; set; }

        public string PostExitCommand { get; set; } = "";

        public string GameArguments { get; set; } = "";

        public bool LaunchFullScreen { get; set; }

        public string GraphicsBackend { get; set; } = "";

        public bool SkipLaunchMenu { get; set; }

        public int PhysicsFps { get; set; }

        public bool CloseToTray { get; set; }

        public string LastBackupPath { get; set; } = "";

        public int LastRelayLatencyMs { get; set; } = -1;

        public string PlayerName { get; set; } = "";
        public string LastAccount { get; set; } = "";
        public List<string> Accounts { get; set; } = new();

        public string ActiveInstanceId { get; set; } = "";

        public string AccentKey { get; set; } = "Blue";

        public int DownloadThreads { get; set; } = 8;
        public int DownloadSpeedLimitKbps { get; set; }
        public bool AutoUpdate { get; set; } = true;

        public string Language { get; set; } = "zh-CN";

        public const string DefaultRelayHost = "43.138.224.197";
        public const int DefaultRelayWsPort = 80;
        public const int DefaultRelayTcpPort = 7777;
        public const string DefaultRelayWsPath = "/relay-ws";

        public string RelayHost { get; set; } = DefaultRelayHost;
        public int RelayTcpPort { get; set; } = DefaultRelayTcpPort;
        public int RelayWebSocketPort { get; set; } = DefaultRelayWsPort;
        public string RelayWebSocketPath { get; set; } = DefaultRelayWsPath;

        public bool PreferWebSocket { get; set; } = true;

        public bool AutoLaunchGameOnLobby { get; set; } = true;

        /// <summary>
        /// 上一次在联机页选的关卡（-level 参数就是它）。
        /// 房主建房时它就是房间地图；加入者进房时会自动改成房主那张图。
        /// </summary>
        public string LobbyMapId { get; set; } = "west_coast_usa";

        /// <summary>
        /// 上一次选的出生点（scenetree 对象名，例如 spawns_industrial）。
        /// 房主和加入者各选各的 —— 同一张图，落点可以不同。
        /// </summary>
        public string LobbySpawnPoint { get; set; } = "";

        /// <summary>
        /// 房主建房时选的房间人数上限（含自己）。
        /// 只有房主这一份会被中继当作权威值；加入者带的值中继会忽略。
        /// 取值范围 2~16：下限 2 是「一个人不算联机」，上限 16 是中继单进程
        /// 扇形广播能撑住的规模（车包是 O(n²) 放大，再多会把中继出口压垮）。
        /// </summary>
        public int LobbyCapacity { get; set; } = 8;

        /// <summary>
        /// 房主选的玩法模式 id（见 LobbyGameModeCatalog）。
        /// 房主这一份随房间元数据发到中继并广播，加入者据此显示「本房玩法」。
        /// 认不出来的值一律由 LobbyGameModeCatalog.Normalize 退回默认玩法，不会炸。
        /// </summary>
        public string LobbyGameMode { get; set; } = LobbyGameModeCatalog.DefaultId;

        /// <summary>启动游戏前确保 Steam 在运行（决定这段游玩时长会不会被 Steam 记账）。</summary>
        public bool EnsureSteamBeforeLaunch { get; set; } = true;

        // ---- Steam 侧数据缓存 ----
        // 由服务器查 Steam Web API 得到（客户端直连 api.steampowered.com 在国内不通）。
        // 缓存到本地是为了链路抖动时仍能显示上次同步到的数值。
        // 注意：这些是「Steam 的账」，不覆盖本机的 TotalPlaytimeSeconds / LaunchCount。
        public long SteamPlaytimeMinutes { get; set; }
        public long SteamPlaytime2WeeksMinutes { get; set; }
        public long SteamLastPlayedUnix { get; set; }
        public long SteamMetersDriven { get; set; }
        public int SteamAchievementsUnlocked { get; set; }
        public int SteamAchievementsTotal { get; set; }
        public string SteamSyncedAt { get; set; } = "";

        /// <summary>成就列表的最近一次快照（JSON），冷启动时先拿它渲染，再后台刷新。</summary>
        public string SteamAchievementsJson { get; set; } = "";

        public long TotalPlaytimeSeconds { get; set; }

        public int LaunchCount { get; set; }

        public string LastLaunchAt { get; set; } = "";

        public int LastSessionSeconds { get; set; }

        public string LastSessionEndedAt { get; set; } = "";

        public string RunningSessionStartedAt { get; set; } = "";

        public string LastDiagnosticsBundlePath { get; set; } = "";

        [JsonIgnore]
        public static string ConfigDirectory => StartRidePaths.Root;

        [JsonIgnore]
        public static string ConfigPath => StartRidePaths.SettingsFile;

        private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

        public static AppSettings Current
        {
            get
            {
                lock (SyncRoot)
                {
                    return cached ?? (cached = LoadFromDisk());
                }
            }
        }

        private static readonly object SyncRoot = new object();
        private static AppSettings? cached;

        public static AppSettings Load() => Current;

        public static void Reload()
        {
            lock (SyncRoot)
            {
                cached = LoadFromDisk();
            }
        }

        private static AppSettings LoadFromDisk()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ConfigPath), Opts);
                    if (s != null)
                    {
                        if (string.IsNullOrWhiteSpace(s.GameDirectory)) s.GameDirectory = DetectGameDirectory();
                        if (string.IsNullOrWhiteSpace(s.LauncherLogDirectory))
                            s.LauncherLogDirectory = Path.Combine(ConfigDirectory, "Log");
                        if (string.IsNullOrWhiteSpace(s.PlayerName)) s.PlayerName = Environment.UserName;
                        s.EnsureAccounts();
                        return s;
                    }
                }
            }
            catch
            {
            }

            var fresh = new AppSettings
            {
                GameDirectory = DetectGameDirectory(),
                LauncherLogDirectory = Path.Combine(ConfigDirectory, "Log"),
                PlayerName = Environment.UserName,
            };
            fresh.EnsureAccounts();
            fresh.Save();
            return fresh;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, Opts));
            }
            catch
            {
            }
            Saved?.Invoke(this);
        }

        public static event Action<AppSettings>? Saved;

        public void EnsureAccounts()
        {
            Accounts ??= new List<string>();
            if (Accounts.Count == 0 && !string.IsNullOrWhiteSpace(PlayerName))
                Accounts.Add(PlayerName);
            if (PlayerName.Length == 0 && Accounts.Count > 0)
                PlayerName = Accounts[0];
        }

        public static string DetectGameDirectory()
        {
            var all = DetectGameDirectoryCandidates();
            return all.Count > 0 ? all[0] : "";
        }

        public static List<string> DetectGameDirectoryCandidates()
        {
            var found = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Push(string? dir)
            {
                if (string.IsNullOrWhiteSpace(dir)) return;
                string full;
                try { full = Path.GetFullPath(dir.Trim().Trim('"')); }
                catch { return; }
                if (!IsBeamNgInstall(full)) return;
                if (seen.Add(full)) found.Add(full);
            }

            try
            {
                foreach (var p in Process.GetProcessesByName("BeamNG.drive"))
                {
                    try { Push(Path.GetDirectoryName(p.MainModule?.FileName)); }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }

            foreach (var steamRoot in SteamRoots())
            {
                Push(Path.Combine(steamRoot, "steamapps", "common", "BeamNG.drive"));
                foreach (var lib in SteamLibraryFolders(steamRoot))
                    Push(Path.Combine(lib, "steamapps", "common", "BeamNG.drive"));
            }

            foreach (var loc in UninstallLocations())
                Push(loc);
            foreach (var loc in UninstallLocations())
                Push(Path.Combine(loc, "BeamNG.drive"));

            foreach (var drive in new[] { "C:", "D:", "E:", "F:", "G:" })
            {
                Push($@"{drive}\BeamNG.drive");
                Push($@"{drive}\Games\BeamNG.drive");
                Push($@"{drive}\SteamLibrary\steamapps\common\BeamNG.drive");
                Push($@"{drive}\Program Files (x86)\Steam\steamapps\common\BeamNG.drive");
                Push($@"{drive}\Program Files\Steam\steamapps\common\BeamNG.drive");
            }

            return found;
        }

        public static bool IsBeamNgInstall(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return false;
                if (Directory.Exists(Path.Combine(dir, "lua", "ge"))) return true;
                return File.Exists(Path.Combine(dir, "BeamNG.drive.exe"));
            }
            catch { return false; }
        }

        private static IEnumerable<string> SteamRoots()
        {
            var roots = new List<string>();
            void AddReg(string hive, string sub)
            {
                try
                {
                    using var key = hive == "HKCU"
                        ? Microsoft.Win32.Registry.CurrentUser.OpenSubKey(sub)
                        : Microsoft.Win32.Registry.LocalMachine.OpenSubKey(sub);
                    var v = key?.GetValue("SteamPath") as string ?? key?.GetValue("InstallPath") as string;
                    if (!string.IsNullOrWhiteSpace(v)) roots.Add(v.Replace('/', '\\'));
                }
                catch { }
            }
            AddReg("HKCU", @"Software\Valve\Steam");
            AddReg("HKLM", @"SOFTWARE\WOW6432Node\Valve\Steam");
            AddReg("HKLM", @"SOFTWARE\Valve\Steam");
            roots.Add(@"C:\Program Files (x86)\Steam");
            roots.Add(@"C:\Program Files\Steam");
            return roots;
        }

        private static IEnumerable<string> SteamLibraryFolders(string steamRoot)
        {
            var libs = new List<string>();
            try
            {
                string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) return libs;
                string text = File.ReadAllText(vdf);
                foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\""))
                {
                    string p = m.Groups[1].Value.Replace(@"\\", @"\");
                    if (!string.IsNullOrWhiteSpace(p)) libs.Add(p);
                }
            }
            catch { }
            return libs;
        }

        private static IEnumerable<string> UninstallLocations()
        {
            var list = new List<string>();
            string[] subs =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            };
            foreach (var sub in subs)
            {
                try
                {
                    using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(sub);
                    if (root == null) continue;
                    foreach (var name in root.GetSubKeyNames())
                    {
                        try
                        {
                            using var k = root.OpenSubKey(name);
                            if (k == null) continue;
                            var display = k.GetValue("DisplayName") as string ?? "";
                            if (display.IndexOf("BeamNG", StringComparison.OrdinalIgnoreCase) < 0) continue;
                            var loc = (k.GetValue("InstallLocation") as string ?? "").Trim('"');
                            if (!string.IsNullOrWhiteSpace(loc)) list.Add(loc);
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return list;
        }

        public static string UserDataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BeamNG.drive");

        /// <summary>
        /// 从 BeamNG 启动器自己的 ini 里读玩家设定的 userFolder 根目录。
        ///
        /// ⚠️ 这是**唯一权威**的来源。玩家在 BeamNG 启动器里改过 userpath 之后，
        /// 名字可以叫任何东西（线上就有人设成 D:\BeamNG，里面既没有 "AppData" 也没有 "current"），
        /// 靠目录名猜必然漏。所以先来这里问，猜只当兜底。
        ///
        /// 两个地方都会写：
        ///   · %LOCALAPPDATA%\BeamNG\BeamNG.drive.ini  的 userFolder（跨版本的主配置）
        ///   · &lt;游戏根&gt;\startup.ini 的 currentUserPath（启动器改完会同步一份）
        /// </summary>
        private static string? ReadConfiguredUserFolder()
        {
            // ① %LOCALAPPDATA%\BeamNG\BeamNG.drive.ini → userFolder
            try
            {
                string ini = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BeamNG", "BeamNG.drive.ini");
                if (File.Exists(ini))
                {
                    foreach (var raw in File.ReadAllLines(ini))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        if (!line.Substring(0, eq).Trim()
                                .Equals("userFolder", StringComparison.OrdinalIgnoreCase)) continue;
                        var val = line.Substring(eq + 1).Trim().Trim('"');
                        if (val.Length > 0 && Directory.Exists(val)) return val;
                    }
                }
            }
            catch { }

            // ② <游戏根>\startup.ini → currentUserPath（installPath 也在同一个 ini 里）
            try
            {
                string root = DetectGameDirectory();
                if (!string.IsNullOrWhiteSpace(root))
                {
                    string ini = Path.Combine(root, "startup.ini");
                    if (File.Exists(ini))
                    {
                        foreach (var raw in File.ReadAllLines(ini))
                        {
                            var line = raw.Trim();
                            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                            int eq = line.IndexOf('=');
                            if (eq <= 0) continue;
                            var key = line.Substring(0, eq).Trim();
                            if (!key.Equals("currentUserPath", StringComparison.OrdinalIgnoreCase) &&
                                !key.Equals("userFolder", StringComparison.OrdinalIgnoreCase)) continue;
                            var val = line.Substring(eq + 1).Trim().Trim('"');
                            if (val.Length > 0 && Directory.Exists(val)) return val;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 判定一个目录像不像 userpath 根：下面直接有 settings，或有子目录 current\settings。
        /// </summary>
        private static bool LooksLikeUserPathRoot(string dir, out string resolved)
        {
            resolved = "";
            try
            {
                if (Directory.Exists(Path.Combine(dir, "settings"))) { resolved = dir; return true; }
                var nested = Path.Combine(dir, "current");
                if (Directory.Exists(Path.Combine(nested, "settings"))) { resolved = nested; return true; }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 候选 userpath 根目录，**权威来源排在最前**。
        ///
        /// ⚠️ 目录名过滤只要求含 "BeamNG"（早先还要求含 "AppData"，把 D:\BeamNG 这类
        /// 合法 userpath 整个漏掉了，正是线上「未找到模组目录」的成因）。
        /// </summary>
        public static List<string> ResolveUserPathRoots()
        {
            var roots = new List<string>();

            void Push(string? p)
            {
                if (string.IsNullOrWhiteSpace(p)) return;
                p = p.TrimEnd('\\', '/');
                foreach (var e in roots)
                {
                    if (string.Equals(e, p, StringComparison.OrdinalIgnoreCase)) return;
                }
                roots.Add(p);
            }

            // ① 权威：玩家自己设的
            Push(ReadConfiguredUserFolder());

            // ② 默认位置（没改过 userpath 的老实人）
            Push(UserDataDirectory);

            // ③ 兜底扫描：目录名里含 BeamNG 就算候选，再靠 settings 子目录判定
            foreach (var drive in new[] { "C:", "D:", "E:", "F:", "G:" })
            {
                try
                {
                    foreach (var d in Directory.GetDirectories(drive + "\\"))
                    {
                        var name = Path.GetFileName(d);
                        if (name.IndexOf("BeamNG", StringComparison.OrdinalIgnoreCase) >= 0)
                            Push(d);
                    }
                }
                catch { }
            }
            return roots;
        }

        /// <summary>
        /// 游戏真正在用的 userpath 根目录（也就是游戏里 FS 的「/」）。
        ///
        /// ⚠️ 不能直接用 <see cref="UserDataDirectory"/>：那只是「没改过时的默认位置」。
        /// 玩家在 BeamNG 启动器里改过 userFolder 之后，游戏根本不看那里
        /// —— 本机就是 D:\BeamNG.Drive'sAppDataRoaming，真正读写的是它下面的 current\。
        /// 判据用「有没有 settings 目录」，和游戏自身的目录布局一致。
        /// </summary>
        public static string ResolveGameUserPathRoot()
        {
            foreach (var r in ResolveUserPathRoots())
            {
                if (LooksLikeUserPathRoot(r, out var resolved)) return resolved;
            }
            return UserDataDirectory;
        }

        public string ResolveUserDataRoot() => ResolveGameUserPathRoot();

        public string ResolveSettingsDirectory() => Path.Combine(ResolveUserDataRoot(), "settings");

        /// <summary>userpath 下某个子目录（mods / replays / screenshots …）的真实位置。</summary>
        private static string ResolveUserPathChild(string child)
        {
            // 先问权威的 userpath：它下面直接挂着 mods / replays
            string root = ResolveGameUserPathRoot();
            try
            {
                var direct = Path.Combine(root, child);
                if (Directory.Exists(direct)) return direct;
            }
            catch { }

            // 再退回「根/current/child」这种老布局
            foreach (var r in ResolveUserPathRoots())
            {
                try
                {
                    var direct = Path.Combine(r, child);
                    if (Directory.Exists(direct)) return direct;
                    var nested = Path.Combine(r, "current", child);
                    if (Directory.Exists(nested)) return nested;
                }
                catch { }
            }

            // 都没有：返回权威 userpath 下的路径（哪怕还没建出来，语义也是对的）
            return Path.Combine(root, child);
        }

        public string ResolveModsDirectory() => ResolveUserPathChild("mods");

        public string ResolveReplaysDirectory() => ResolveUserPathChild("replays");
    }
}
