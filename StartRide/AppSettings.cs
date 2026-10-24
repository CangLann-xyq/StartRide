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
        /// 警匪 / 德比：单回合时限（秒）。到时强盗没被抓就算撑满全场获胜，
        /// 德比则判给幸存者里车损最低的那位。0 = 不限时。
        /// </summary>
        public int RoundSeconds { get; set; } = 300;

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

        // ------------------------------------------------------------------
        // 模式准入限制（警匪追逐 / 德比等特殊模式进入游戏后的硬约束）。
        // 由 startrideMode.lua 的 readConfig 白名单读取，启动器只负责写入。
        // ------------------------------------------------------------------

        /// <summary>
        /// 活动范围半径（米）。以本人出生点为圆心，超圈按下面两项处置。0 = 不限制。
        /// </summary>
        /// <summary>
        /// 活动区域半径（米）。0 = 不限；默认 1200 米 —— 警匪 / 德比进局即生效，
        /// 玩家跑出这个圈会被宽限倒计时，超时按 <see cref="ModeZoneEliminate"/> 处理。
        /// </summary>
        public int ModeZoneRadius { get; set; } = 1200;

        /// <summary>
        /// 出圈宽限（秒）。出圈后留给玩家开回圈内的时间。
        /// </summary>
        public int ModeZoneGraceSeconds { get; set; } = 5;

        /// <summary>
        /// 宽限结束仍未回圈时：true = 直接淘汰，false = 拉回圈内边缘。
        /// </summary>
        public bool ModeZoneEliminate { get; set; }

        /// <summary>
        /// 指定车型白名单（车辆 JBeam 文件名关键字，逗号分隔）。留空 = 不限制。
        /// </summary>
        public string ModeVehicleTypes { get; set; } = string.Empty;

        /// <summary>
        /// 开局强制把不在白名单内的车换成名单车型。false 时只警告不换车。
        /// </summary>
        public bool ModeVehicleEnforce { get; set; } = true;

        /// <summary>
        /// 回合内额外屏蔽的输入组（逗号分隔）：fun / vehicle / ai / reset / teleport / menu。
        /// </summary>
        public string ModeBlockGroups { get; set; } = string.Empty;

        /// <summary>
        /// 回合内放行的输入组（逗号分隔）。优先级低于 ModeBlockGroups；
        /// 警匪局的原地复位（reset）始终不可放行 —— 那是逃脱手段。
        /// </summary>
        public string ModeAllowGroups { get; set; } = string.Empty;

        // ------------------------------------------------------------------
        // 细粒度玩法规则（三态：null = 跟随模式默认，0 = 强制关闭，1 = 强制开启）
        //
        // 之所以做三态而不是开关：警匪和德比对「能不能手动复位」的答案是相反的
        // （警匪禁、德比留着翻车自救），写死成 true 会把德比也一起禁掉。
        // null 写进 multiplayer.json 就是 JSON null，Lua 侧 tonumber 拿不到值，
        // 自然回落到各模式的默认值。
        // ------------------------------------------------------------------

        /// <summary>起跑前空档锁定 + 起跑后自动挂一档。</summary>
        /// <summary>
        /// 严格度预设：空串 = 跟随各玩法自己的口径；loose / standard / strict / off = 房主统一指定。
        /// 只替换「预设」这一层，德比「允许复位」这类模式语义例外不会被它撬掉。
        /// </summary>
        public string ModeRulePreset { get; set; } = "";

        /// <summary>
        /// 规则逐条覆写：逗号分隔的 key=1 / key=0（1 = 开限制，0 = 关限制）。
        /// 优先级最高：先按严格度预设算，再被这里逐条盖掉。
        /// 键名白名单见 ModeRuleCatalog.Keys（与游戏内 RULE_DEFS 一一对应）。
        /// </summary>
        public string ModeRuleOverrides { get; set; } = "";

        public int? ModeNeutralLock { get; set; }

        /// <summary>局内禁止手动复位车辆。</summary>
        public int? ModeNoManualReset { get; set; }

        /// <summary>局内禁止换车 / 换零件。</summary>
        public int? ModeNoVehicleSwap { get; set; }

        // ------------------------------------------------------------------
        // AI 助手
        //
        // 密钥不在本地：客户端只连自己的服务端（cloudfurhub.cn/api/startride/ai），
        // DeepSeek 的 key 只存在于服务器 .env。这里是「能用它做什么」的授权，
        // 不是「能不能连」——连不上是网络问题，不是权限问题。
        // ------------------------------------------------------------------

        /// <summary>允许 AI 读取文件 / 列目录（旧口径，由策略派生，保留只为读老配置）。</summary>
        public bool AiAllowRead { get; set; } = true;

        /// <summary>允许 AI 写入或覆盖文件（旧口径，由策略派生）。</summary>
        public bool AiAllowWrite { get; set; } = false;

        /// <summary>允许 AI 执行命令行（旧口径，由策略派生）。</summary>
        public bool AiAllowExecute { get; set; } = false;

        /// <summary>
        /// 三态授权：0 = 禁止，1 = 每次问我，2 = 直接允许。
        ///
        /// 为什么不能只有一个 bool：真正要防的不是「AI 能不能写文件」，
        /// 而是「这一次写的到底是哪个文件」。开关式授权一开就是长期放行，
        /// 中间 AI 想覆盖什么都不会再问；反过来全关又等于 Agent 模式半残。
        /// 默认：读=允许（只读不会造成损失），写/执行=每次问我（能看清它要干什么再点头）。
        ///
        /// null = 老配置，没有这三个字段，此时从上面的 AiAllow* 推导，
        /// 老用户升级后行为不变（原来开着还是直接允许）。
        /// </summary>
        // 默认值（老配置里没有这几个键时也用这套）：
        //   读 → 2 直接允许（只读不会有损失，每次都问会很烦）
        //   写 / 执行 → 1 每次问我（这两项才是真正会改东西的）
        // 不给 1 的话，从没动过设置的用户一进 Agent 只会收到「权限不足」，
        // 等于这个模式对他们根本不存在。
        public int? AiPolicyRead { get; set; } = 2;

        public int? AiPolicyWrite { get; set; } = 1;

        public int? AiPolicyExecute { get; set; } = 1;

        /// <summary>
        /// AI 能访问的根目录。所有读写路径都会被解析到这个目录下，
        /// 越界直接拒绝（防止模型去翻 C:\Windows）。留空 = 用户目录。
        /// </summary>
        public string AiWorkspaceRoot { get; set; } = "";

        /// <summary>对话模式用的模型：deepseek-chat / deepseek-reasoner。</summary>
        public string AiModel { get; set; } = "deepseek-chat";

        /// <summary>上次用的模式：true = Agent（能动手），false = 对话。</summary>
        public bool AiAgentMode { get; set; } = false;

        // ------------------------------------------------------------------

        [JsonIgnore]
        public bool PreInstallMod => AutoInstallMod && !RemoveModOnLeave;

        public bool AutoAllowFirewall { get; set; } = true;
        public bool MinimizeToTray { get; set; }
        // 0 = 跟随系统（不设上限）。原来默认 4096 会让 BeamNG 进图时 OOM 自杀。
        public int MaxMemoryMB { get; set; } = 0;

        // 默认关闭：BeamNG 自己会管内存，硬上限只会让它进图时崩。
        public bool LimitGameMemory { get; set; } = false;
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
