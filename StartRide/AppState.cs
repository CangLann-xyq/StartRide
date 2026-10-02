using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

namespace StartRide.Core
{

    public sealed class AppState
    {
        public static AppState Current { get; } = new();

        public AppSettings Settings { get; }
        public ApiService Api { get; } = new();
        public CloudSyncService CloudSync { get; }
        public MultiplayerSession Multiplayer { get; }
        public GameLauncher Launcher { get; }
        public ModInstaller ModInstaller { get; }
        public InstanceStore Instances { get; }
        public DownloadManager Downloads { get; }

        public event Action<string>? LogEmitted;

        private readonly object _logLock = new();

        public void Log(string message)
        {
            try
            {
                string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                string dir = string.IsNullOrWhiteSpace(Settings.LauncherLogDirectory)
                    ? Path.Combine(AppSettings.ConfigDirectory, "Log")
                    : Settings.LauncherLogDirectory;
                Directory.CreateDirectory(dir);
                lock (_logLock)
                {
                    File.AppendAllText(Path.Combine(dir, $"launcher-{DateTime.Now:yyyyMMdd}.log"),
                        line + Environment.NewLine);
                }
            }
            catch
            {
            }

            LogEmitted?.Invoke(message);
        }

        public event Action<string>? NavigateRequested;
        public void Navigate(string page) => NavigateRequested?.Invoke(page);

        public Action<string>? Toast { get; set; }

        public void Notify(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            try
            {
                if (Toast != null)
                {
                    Toast(message);
                    return;
                }
            }
            catch
            {
            }
            Log(message);
        }

        private AppState()
        {
            Settings = AppSettings.Load();
            Settings.EnsureAccounts();
            CloudSync = new CloudSyncService(Api, Settings);
            Multiplayer = new MultiplayerSession(Settings);
            Launcher = new GameLauncher(Settings);
            ModInstaller = new ModInstaller(Settings);
            Instances = new InstanceStore(Settings);
            Downloads = new DownloadManager(Settings);

            Multiplayer.Log += Log;
            // 故意用静态事件而不是 Launcher.Log / Launcher.RunningChanged：
            // 主页/实例/回放/托盘会各自 new 一个 GameLauncher（要指向不同实例目录），
            // 只挂 AppState.Launcher 会漏掉这四条入口 —— 2026-10-02 实测踩到。
            GameLauncher.AnyLog -= Log;
            GameLauncher.AnyLog += Log;
            GameLauncher.AnyGameExited -= QueueSteamPlaytimeRefresh;
            GameLauncher.AnyGameExited += QueueSteamPlaytimeRefresh;
            ModInstaller.Log += Log;
            Instances.Log += Log;
            Downloads.Log += Log;

            Launcher.RunningChanged += running =>
            {
                if (!running)
                {
                    try { ModInstaller.ApplyPendingRemoval(); } catch { }
                }
            };
            try { ModInstaller.ApplyPendingRemoval(); } catch { }

            try { ModInstaller.RemoveStaleOnStartup(); } catch { }
        }

        /// <summary>
        /// 游戏退出后，隔一会儿把 Steam 侧时长读回来。
        /// 为什么能这么做：这一局是启动器直接起 exe 的（不经 Steam 启动），但 Steam 客户端一直在跑，
        /// 所以 Steam 会把它记进自己的账本 —— 公开 Web API 没有「写时长」的能力，时长只能由 Steam 自己掐表。
        /// 为什么等一会儿：Steam 在进程退出后几秒才把这局结算进 playtime_forever，立刻读会拿到旧值。
        /// </summary>
        private void QueueSteamPlaytimeRefresh()
        {
            if (string.IsNullOrWhiteSpace(Api.Token)) return;
            if (string.IsNullOrWhiteSpace(Settings.SteamSyncedAt) &&
                Settings.SteamPlaytimeMinutes <= 0) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(12));
                    // forceFresh：跳过服务端 5 分钟缓存，否则会命中启动时写下的那份，
                    // 刚打完的这几分钟就看不到。
                    SteamProfileInfo? profile = await Api.GetSteamProfileAsync(forceFresh: true);
                    if (profile == null || !profile.Bound || profile.Fresh == false) return;

                    Settings.SteamPlaytimeMinutes = profile.PlaytimeMinutes;
                    Settings.SteamPlaytime2WeeksMinutes = profile.Playtime2WeeksMinutes;
                    Settings.SteamLastPlayedUnix = profile.LastPlayedUnix;
                    if (profile.AchievementsTotal > 0)
                    {
                        Settings.SteamAchievementsUnlocked = profile.AchievementsUnlocked;
                        Settings.SteamAchievementsTotal = profile.AchievementsTotal;
                    }
                    Settings.SteamSyncedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                    Settings.Save();
                    Log("Steam 时长已回读：" + profile.PlaytimeMinutes + " 分钟");
                }
                catch (Exception ex)
                {
                    Log("退出后回读 Steam 时长失败：" + ex.Message);
                }
            });
        }

        public static readonly IReadOnlyList<(string Key, Color Color, string Display)> AccentOptions =
            new List<(string, Color, string)>
            {
                ("Blue",    Color.FromRgb(0x21, 0x96, 0xF3), "蓝色"),
                ("Cyan",    Color.FromRgb(0x00, 0xBC, 0xD4), "青色"),
                ("Green",   Color.FromRgb(0x22, 0xC5, 0x5E), "绿色"),
                ("Emerald", Color.FromRgb(0x10, 0xB9, 0x81), "翠绿"),
                ("Purple",  Color.FromRgb(0x8B, 0x5C, 0xF6), "紫色"),
                ("Pink",    Color.FromRgb(0xEC, 0x48, 0x99), "粉色"),
                ("Orange",  Color.FromRgb(0xF9, 0x73, 0x16), "橙色"),
                ("Amber",   Color.FromRgb(0xF5, 0x9E, 0x0B), "琥珀"),
            };

        public void ApplyAccent(string key)
        {
            Color c = Color.FromRgb(0x08, 0x91, 0xFE);
            foreach (var (k, col, _) in AccentOptions)
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) { c = col; break; }

            var res = System.Windows.Application.Current?.Resources;
            if (res == null) return;

            res["Brush.Accent.Primary"] = new SolidColorBrush(c);
            res["Brush.Accent.Hover"] = new SolidColorBrush(Shift(c, 0.86));
            res["Brush.Accent.Pressed"] = new SolidColorBrush(Shift(c, 0.72));
            res["Brush.Accent.Selection"] = new SolidColorBrush(c) { Opacity = 0.40 };
            res["Brush.Accent.Border"] = new SolidColorBrush(Lighten(c, 0.35)) { Opacity = 0.62 };

            Settings.AccentKey = key;
            Settings.Save();
        }

        private static Color Shift(Color c, double f) =>
            Color.FromRgb((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));

        private static Color Lighten(Color c, double t) =>
            Color.FromRgb(
                (byte)(c.R + (255 - c.R) * t),
                (byte)(c.G + (255 - c.G) * t),
                (byte)(c.B + (255 - c.B) * t));
    }
}
