using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StartRide.Core
{
    public sealed class StorageItem
    {
        public string Key { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Path { get; init; } = "";
        public long SizeBytes { get; set; }
        public int FileCount { get; set; }
        public bool CanClean { get; init; }

        public string SizeText => FileSizeFormatter.Format(SizeBytes);
        public string DetailText => FileCount + " 个文件 · " + SizeText;
        public bool Exists => FileCount > 0 || SizeBytes > 0;
    }

    public sealed class StorageCleanupResult
    {
        public int DeletedFiles { get; init; }
        public long FreedBytes { get; init; }
        public string Message { get; init; } = "";

        public string FreedText => FileSizeFormatter.Format(FreedBytes);
    }

    public sealed class StorageUsageService
    {
        private readonly AppSettings _settings;

        public StorageUsageService(AppSettings settings) => _settings = settings;

        private static readonly (string Key, string Name, string Relative)[] CleanableTargets =
        {
            ("temp", "临时文件 (temp)", "temp"),
            ("replays", "回放录像 (replays)", "replays"),
            ("screenshots", "截图 (screenshots)", "screenshots"),
        };

        private static readonly string[] GameInstallCleanTargets =
        {
            Path.Combine("Bin64", "temp"),
            "logs",
            "temp",
        };

        public List<StorageItem> Scan()
        {
            var items = new List<StorageItem>();
            string userPath = SafeUserPath();

            void Add(string key, string name, string dir, bool canClean)
            {
                var (size, count) = MeasureDirectory(dir);
                items.Add(new StorageItem
                {
                    Key = key,
                    DisplayName = name,
                    Path = dir,
                    SizeBytes = size,
                    FileCount = count,
                    CanClean = canClean,
                });
            }

            Add("settings", "游戏配置 (settings)", Path.Combine(userPath, "settings"), false);
            Add("vehicles", "车辆存档 (vehicles)", Path.Combine(userPath, "vehicles"), false);
            Add("mods", "模组 (mods)", Path.Combine(userPath, "mods"), false);
            Add("replays", "回放录像 (replays)", Path.Combine(userPath, "replays"), true);
            Add("screenshots", "截图 (screenshots)", Path.Combine(userPath, "screenshots"), true);
            Add("temp", "临时文件 (temp)", Path.Combine(userPath, "temp"), true);

            string gameDir = _settings.GameDirectory ?? "";
            if (AppSettings.IsBeamNgInstall(gameDir))
            {
                foreach (var rel in GameInstallCleanTargets)
                {
                    string dir = Path.Combine(gameDir, rel);
                    if (!Directory.Exists(dir)) continue;
                    var (size, count) = MeasureDirectory(dir);
                    items.Add(new StorageItem
                    {
                        Key = "gameinstall:" + rel,
                        DisplayName = "游戏缓存 (安装目录\\" + rel + ")",
                        Path = dir,
                        SizeBytes = size,
                        FileCount = count,
                        CanClean = true,
                    });
                }
            }

            var (logSize, logCount) = MeasureLogBackups(userPath);
            items.Add(new StorageItem
            {
                Key = "logbackups",
                DisplayName = "历史日志备份 (*.log.bak)",
                Path = userPath,
                SizeBytes = logSize,
                FileCount = logCount,
                CanClean = true,
            });

            return items;
        }

        private string SafeUserPath()
        {
            try { return _settings.ResolveUserDataRoot(); }
            catch { return AppSettings.UserDataDirectory; }
        }

        private static (long Size, int Count) MeasureDirectory(string dir)
        {
            long size = 0;
            int count = 0;
            try
            {
                if (!Directory.Exists(dir)) return (0, 0);
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        size += new FileInfo(f).Length;
                        count++;
                    }
                    catch { }
                }
            }
            catch { }
            return (size, count);
        }

        private static (long Size, int Count) MeasureLogBackups(string userPath)
        {
            long size = 0;
            int count = 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(userPath, "*.log.bak", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        size += new FileInfo(f).Length;
                        count++;
                    }
                    catch { }
                }
            }
            catch { }
            return (size, count);
        }

        public StorageCleanupResult Clean(string key)
        {
            string userPath = SafeUserPath();
            var scanned = Scan();
            var target = scanned.FirstOrDefault(i => i.Key == key);

            if (target == null)
            {
                return new StorageCleanupResult { Message = "未知的清理项：" + key };
            }
            string dir = target.Path ?? "";
            if (dir.Length == 0 || !Directory.Exists(dir))
            {
                return new StorageCleanupResult { Message = "目录不存在，无需清理" };
            }

            if (!IsInsideAllowedRoot(dir, userPath))
            {
                return new StorageCleanupResult { Message = "该目录不在允许清理的范围内，已阻止" };
            }

            long freed = 0;
            int deleted = 0;

            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToList())
            {
                try
                {
                    long len = new FileInfo(f).Length;
                    File.Delete(f);
                    freed += len;
                    deleted++;
                }
                catch
                {
                }
            }

            try
            {
                foreach (var sub in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories)
                             .OrderByDescending(d => d.Length))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(sub).Any()) Directory.Delete(sub);
                    }
                    catch { }
                }
            }
            catch { }

            return new StorageCleanupResult
            {
                DeletedFiles = deleted,
                FreedBytes = freed,
                Message = deleted == 0
                    ? "没有可删除的文件（可能正被游戏占用）"
                    : "已删除 " + deleted + " 个文件，释放 " + FileSizeFormatter.Format(freed),
            };
        }

        public StorageCleanupResult CleanLogBackups()
        {
            string userPath = SafeUserPath();
            long freed = 0;
            int deleted = 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(userPath, "*.log.bak", SearchOption.TopDirectoryOnly).ToList())
                {
                    try
                    {
                        long len = new FileInfo(f).Length;
                        File.Delete(f);
                        freed += len;
                        deleted++;
                    }
                    catch { }
                }
            }
            catch { }

            return new StorageCleanupResult
            {
                DeletedFiles = deleted,
                FreedBytes = freed,
                Message = deleted == 0
                    ? "没有历史日志备份"
                    : "已删除 " + deleted + " 个日志备份，释放 " + FileSizeFormatter.Format(freed),
            };
        }

        private bool IsInsideAllowedRoot(string dir, string userPath)
        {
            string full;
            try { full = Path.GetFullPath(dir); }
            catch { return false; }

            bool Under(string root)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(root)) return false;
                    string r = Path.GetFullPath(root).TrimEnd('\\') + "\\";
                    return full.StartsWith(r, StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            }

            if (Under(userPath)) return true;

            string gameDir = _settings.GameDirectory ?? "";
            if (AppSettings.IsBeamNgInstall(gameDir) && Under(gameDir))
            {
                foreach (var rel in GameInstallCleanTargets)
                {
                    if (Under(Path.Combine(gameDir, rel))) return true;
                }
            }
            return false;
        }
    }
}
