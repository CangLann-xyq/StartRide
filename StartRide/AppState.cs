using System;
using System.Collections.Generic;
using System.IO;
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
            Launcher.Log += Log;
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
