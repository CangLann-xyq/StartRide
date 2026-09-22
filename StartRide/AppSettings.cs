using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StartRide.Core
{
    /// <summary>启动器设置，持久化到 %AppData%\StartRide\settings.json。</summary>
    public sealed class AppSettings
    {
        // ---- 通用 ----
        public string GameDirectory { get; set; } = "";
        public string LauncherLogDirectory { get; set; } = "";
        public bool DiagnosticLog { get; set; }

        // ---- 启动 ----
        public bool AutoDetectGame { get; set; } = true;
        public bool AutoInstallMod { get; set; } = true;
        public bool AutoAllowFirewall { get; set; } = true;
        public bool MinimizeToTray { get; set; }
        public int MaxMemoryMB { get; set; } = 4096;

        /// <summary>
        /// 把「内存分配」真的作用到游戏进程上：启动时用 Windows 作业对象（Job Object）
        /// 给 BeamNG.drive.exe 设一个内存上限，超了就分配失败，而不是只记个数。
        /// </summary>
        public bool LimitGameMemory { get; set; } = true;
        public string ExtraLaunchArgs { get; set; } = "";
        public bool AutoCheckVehicleMods { get; set; } = true;
        public bool ForceHighPerformanceGpu { get; set; } = true;

        /// <summary>
        /// 启动游戏前自动检查必需配置文件，缺失/损坏就从云端模板补全。
        /// </summary>
        public bool AutoRepairGameConfig { get; set; } = true;

        // ---- 启动行为（这些以前只存在 LauncherSettings 里、没有任何代码读，等于摆设；
        //      现在统一落到 StartRide 自己的配置，并且在 GameLauncher / 启动服务里真的执行）----

        /// <summary>启动前做一次游戏文件体检（缺什么先告诉用户，别等进游戏才发现）。</summary>
        public bool CheckFilesBeforeLaunch { get; set; } = true;

        /// <summary>启动游戏之前执行的命令（可空）。</summary>
        public string PreLaunchCommand { get; set; } = "";

        /// <summary>是否等启动前命令执行完再拉起游戏（关掉则并行，不等它结束）。</summary>
        public bool WaitForPreLaunchCommand { get; set; }

        /// <summary>游戏退出之后执行的命令（可空）。常用于重启启动器、关机等收尾动作。</summary>
        public string PostExitCommand { get; set; } = "";

        /// <summary>追加给 BeamNG.drive.exe 的游戏参数（与 ExtraLaunchArgs 合并生效）。</summary>
        public string GameArguments { get; set; } = "";

        /// <summary>以全屏方式启动游戏（追加 -fullscreen）。</summary>
        public bool LaunchFullScreen { get; set; }

        /// <summary>
        /// 渲染后端：""=跟随游戏默认，"dx11" / "d3d12" / "vk"。
        /// 对应游戏真实支持的 -gfx 参数（d3d12 自 0.39 起、vk 为 beta）。
        /// </summary>
        public string GraphicsBackend { get; set; } = "";

        /// <summary>跳过游戏启动菜单（追加 -noninteractive），直接进驾驶界面，省一次点击。</summary>
        public bool SkipLaunchMenu { get; set; }

        /// <summary>物理步进频率（-physicsfps N）；0=游戏默认（2000）。调低会掉精度，调高更吃 CPU。</summary>
        public int PhysicsFps { get; set; }

        /// <summary>点关闭按钮时收进托盘而不是退出（托盘菜单里可以真退出）。</summary>
        public bool CloseToTray { get; set; }

        /// <summary>最近一次配置备份的 zip 路径。</summary>
        public string LastBackupPath { get; set; } = "";

        /// <summary>最近一次中继延迟测试结果（毫秒）；-1 表示没测过/不可达。</summary>
        public int LastRelayLatencyMs { get; set; } = -1;

        // ---- 账户 ----
        public string PlayerName { get; set; } = "";
        public string LastAccount { get; set; } = "";
        /// <summary>离线账户名列表（BeamNG 联机不需要正版账号，这里只存昵称）。</summary>
        public List<string> Accounts { get; set; } = new();

        // ---- 游戏实例 ----
        /// <summary>当前选中的游戏实例 Id（对应 instances.json 里的条目）。</summary>
        public string ActiveInstanceId { get; set; } = "";

        // ---- 外观 ----
        public string AccentKey { get; set; } = "Blue";

        // ---- 下载 ----
        public int DownloadThreads { get; set; } = 8;
        /// <summary>0 表示不限速。</summary>
        public int DownloadSpeedLimitKbps { get; set; }
        public bool AutoUpdate { get; set; } = true;

        // ---- 语言 ----
        public string Language { get; set; } = "zh-CN";

        // ---- 联机（中继） ----
        public const string DefaultRelayHost = "43.138.224.197";
        public const int DefaultRelayWsPort = 80;
        public const int DefaultRelayTcpPort = 7777;
        public const string DefaultRelayWsPath = "/relay-ws";

        public string RelayHost { get; set; } = DefaultRelayHost;
        public int RelayTcpPort { get; set; } = DefaultRelayTcpPort;
        public int RelayWebSocketPort { get; set; } = DefaultRelayWsPort;
        public string RelayWebSocketPath { get; set; } = DefaultRelayWsPath;

        /// <summary>
        /// 优先走 WebSocket 隧道（80 端口，能穿过云安全组）；
        /// 失败再回退到直连 RelayTcpPort。腾讯云安全组只放行 80/443/8443，
        /// 所以直连 7777 在公网通常不可达，WebSocket 才是主通道。
        /// </summary>
        public bool PreferWebSocket { get; set; } = true;

        /// <summary>
        /// 一站式联机：创建 / 加入房间后自动完成「检测游戏 → 安装联机模组 → 连接中继 → 启动游戏」，
        /// 关掉则只建房间不自动拉起游戏（方便调试或先改车辆再进）。
        /// </summary>
        public bool AutoLaunchGameOnLobby { get; set; } = true;

        // ---------------- 游玩统计 ----------------
        // 由 PlaytimeTracker 维护：启动时开一个"会话"，游戏退出时结算成累计时长。
        // 时间一律存 ISO-8601 字符串（JsonSerializer 直接写 DateTimeOffset 也可以，
        // 但字符串便于用户自己看/改，也避免旧配置反序列化出奇怪值）。

        /// <summary>累计游玩秒数。</summary>
        public long TotalPlaytimeSeconds { get; set; }

        /// <summary>累计启动次数。</summary>
        public int LaunchCount { get; set; }

        /// <summary>最近一次启动时间（ISO-8601，本地时区）。</summary>
        public string LastLaunchAt { get; set; } = "";

        /// <summary>最近一次会话时长（秒）。</summary>
        public int LastSessionSeconds { get; set; }

        /// <summary>最近一次会话结束时间（ISO-8601）。</summary>
        public string LastSessionEndedAt { get; set; } = "";

        /// <summary>正在进行的会话开始时间；空串=当前没有会话（游戏已退出或还没启动）。</summary>
        public string RunningSessionStartedAt { get; set; } = "";

        /// <summary>诊断包最近一次导出的完整路径。</summary>
        public string LastDiagnosticsBundlePath { get; set; } = "";

        // ---------------- 持久化 ----------------

        [JsonIgnore]
        public static string ConfigDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StartRide");

        [JsonIgnore]
        public static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");

        private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

        /// <summary>
        /// 全局唯一设置实例。以前每个服务各自 Load() 一份，设置页改了游戏目录
        /// 别的页面还拿着旧值；改成同一个实例后，任何一处 Save() 全应用立即可见。
        /// </summary>
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

        /// <summary>丢弃缓存重新读盘（外部改过 settings.json 时用）。</summary>
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
                // 配置损坏时退回默认值，不阻断启动
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
                // 写盘失败不影响运行
            }
            Saved?.Invoke(this);
        }

        /// <summary>设置保存后触发（云同步服务订阅，实时 push 到云端）。</summary>
        public static event Action<AppSettings>? Saved;

        /// <summary>保证账户列表里至少有当前玩家名。</summary>
        public void EnsureAccounts()
        {
            Accounts ??= new List<string>();
            if (Accounts.Count == 0 && !string.IsNullOrWhiteSpace(PlayerName))
                Accounts.Add(PlayerName);
            if (PlayerName.Length == 0 && Accounts.Count > 0)
                PlayerName = Accounts[0];
        }

        /// <summary>
        /// 在常见位置探测 BeamNG.drive 安装目录（含 lua/ge 的那一层）。
        /// 保留旧签名，内部走多策略探测，取第一个命中。
        /// </summary>
        public static string DetectGameDirectory()
        {
            var all = DetectGameDirectoryCandidates();
            return all.Count > 0 ? all[0] : "";
        }

        /// <summary>
        /// 自动识别 BeamNG.drive 安装目录（多策略，按可信度排序去重）：
        ///   1. 正在运行的 BeamNG.drive.exe 所在目录（最准）
        ///   2. Steam 客户端注册表 → 所有游戏库（libraryfolders.vdf）里的 BeamNG.drive
        ///   3. 卸载表里的 BeamNG 安装位置
        ///   4. 各盘常见路径
        /// 任何一步失败都跳过，不会抛异常。
        /// </summary>
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

            // 1) 正在跑的游戏进程
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

            // 2) Steam：注册表拿安装根，再解析每个游戏库
            foreach (var steamRoot in SteamRoots())
            {
                Push(Path.Combine(steamRoot, "steamapps", "common", "BeamNG.drive"));
                foreach (var lib in SteamLibraryFolders(steamRoot))
                    Push(Path.Combine(lib, "steamapps", "common", "BeamNG.drive"));
            }

            // 3) 卸载表（非 Steam 安装 / Humble / 绿色版）
            foreach (var loc in UninstallLocations())
                Push(loc);
            foreach (var loc in UninstallLocations())
                Push(Path.Combine(loc, "BeamNG.drive"));

            // 4) 常见路径兜底
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

        /// <summary>这个目录是不是一份可用的 BeamNG.drive 安装。</summary>
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

        /// <summary>Steam 安装根目录（注册表 + 常见位置）。</summary>
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

        /// <summary>解析 steamapps\libraryfolders.vdf，拿到所有游戏库路径。</summary>
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

        /// <summary>从卸载表里找 BeamNG 相关安装目录。</summary>
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

        /// <summary>BeamNG 用户数据根目录（mods 所在处；可用 -userpath 重定向）。</summary>
        public static string UserDataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BeamNG.drive");

        /// <summary>
        /// 可能作为 userpath 的根目录列表：系统默认 AppData 目录，
        /// 加上各盘形如 "D:\BeamNG.Drive'sAppDataRoaming" 的重定向目录。
        /// </summary>
        public static List<string> ResolveUserPathRoots()
        {
            var roots = new List<string> { UserDataDirectory };
            foreach (var drive in new[] { "C:", "D:", "E:", "F:", "G:" })
            {
                try
                {
                    foreach (var d in Directory.GetDirectories(drive + "\\"))
                    {
                        var name = Path.GetFileName(d);
                        if (name.Contains("BeamNG", StringComparison.OrdinalIgnoreCase) &&
                            name.Contains("AppData", StringComparison.OrdinalIgnoreCase))
                        {
                            roots.Add(d);
                        }
                    }
                }
                catch { }
            }
            return roots;
        }

        /// <summary>
        /// 真正在用的 userpath（settings/ 的父目录）。BeamNG 常把 userpath
        /// 重定向成 "&lt;root&gt;\current"，这里两种都认。
        /// </summary>
        public string ResolveUserDataRoot()
        {
            foreach (var r in ResolveUserPathRoots())
            {
                try
                {
                    if (Directory.Exists(Path.Combine(r, "settings"))) return r;
                    var nested = Path.Combine(r, "current");
                    if (Directory.Exists(Path.Combine(nested, "settings"))) return nested;
                }
                catch { }
            }
            return UserDataDirectory;
        }

        /// <summary>游戏必需配置文件所在的 settings 目录（不存在则返回理论路径）。</summary>
        public string ResolveSettingsDirectory() => Path.Combine(ResolveUserDataRoot(), "settings");

        /// <summary>
        /// 定位真正在用的 mods 目录。BeamNG 的 userpath 可能被重定向到
        /// 形如 "D:\BeamNG.Drive'sAppDataRoaming" 这样的目录（含 current 子目录）。
        /// </summary>
        public string ResolveModsDirectory()
        {
            var roots = new List<string> { UserDataDirectory };
            foreach (var drive in new[] { "C:", "D:", "E:", "F:", "G:" })
            {
                try
                {
                    foreach (var d in Directory.GetDirectories(drive + "\\"))
                    {
                        var name = Path.GetFileName(d);
                        if (name.Contains("BeamNG", StringComparison.OrdinalIgnoreCase) &&
                            name.Contains("AppData", StringComparison.OrdinalIgnoreCase))
                        {
                            roots.Add(d);
                        }
                    }
                }
                catch { }
            }

            foreach (var r in roots)
            {
                try
                {
                    var direct = Path.Combine(r, "mods");
                    if (Directory.Exists(direct)) return direct;
                    var nested = Path.Combine(r, "current", "mods");
                    if (Directory.Exists(nested)) return nested;
                }
                catch { }
            }
            return Path.Combine(UserDataDirectory, "mods");
        }

        /// <summary>
        /// 定位 BeamNG 回放目录（replays）。userpath 可能被重定向，
        /// 和 mods 一样遍历常见根目录的 current/replays 或 replays。
        /// </summary>
        public string ResolveReplaysDirectory()
        {
            var roots = new List<string> { UserDataDirectory };
            foreach (var drive in new[] { "C:", "D:", "E:", "F:", "G:" })
            {
                try
                {
                    foreach (var d in Directory.GetDirectories(drive + "\\"))
                    {
                        var name = Path.GetFileName(d);
                        if (name.Contains("BeamNG", StringComparison.OrdinalIgnoreCase) &&
                            name.Contains("AppData", StringComparison.OrdinalIgnoreCase))
                        {
                            roots.Add(d);
                        }
                    }
                }
                catch { }
            }

            foreach (var r in roots)
            {
                try
                {
                    var direct = Path.Combine(r, "replays");
                    if (Directory.Exists(direct)) return direct;
                    var nested = Path.Combine(r, "current", "replays");
                    if (Directory.Exists(nested)) return nested;
                }
                catch { }
            }
            return Path.Combine(UserDataDirectory, "replays");
        }
    }
}
