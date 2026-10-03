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

        public string ResolveUserDataRoot() => ResolveGameUserPathRoot();

        public string ResolveSettingsDirectory() => Path.Combine(ResolveUserDataRoot(), "settings");

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
