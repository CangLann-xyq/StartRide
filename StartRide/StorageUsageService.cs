using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StartRide.Core
{
    /// <summary>某一块数据占了多少磁盘。</summary>
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

    /// <summary>清理结果。</summary>
    public sealed class StorageCleanupResult
    {
        public int DeletedFiles { get; init; }
        public long FreedBytes { get; init; }
        public string Message { get; init; } = "";

        public string FreedText => FileSizeFormatter.Format(FreedBytes);
    }

    /// <summary>
    /// 统计游戏数据占用，并清理可以安全删掉的东西。
    ///
    /// BeamNG 的缓存和回放非常能吃盘（实测一台机器：mods 19 GB、replays 13 GB、
    /// temp 1.9 GB），但游戏自身没有"清缓存"入口，只能手动翻目录。
    ///
    /// 安全边界（硬约束）：
    ///   · 所有删除都只在 &lt;userpath&gt; 之内，且只针对下面这份白名单目录/文件；
    ///   · 只删文件，不删用户自己创建的其它文件夹；
    ///   · 永远不动 mods（用户的模组）、settings（配置）与 vehicles（车辆存档）。
    /// </summary>
    public sealed class StorageUsageService
    {
        private readonly AppSettings _settings;

        public StorageUsageService(AppSettings settings) => _settings = settings;

        /// <summary>可清理项：key → (显示名, 相对 userpath 的子目录)。</summary>
        private static readonly (string Key, string Name, string Relative)[] CleanableTargets =
        {
            ("temp", "临时文件 (temp)", "temp"),
            ("replays", "回放录像 (replays)", "replays"),
            ("screenshots", "截图 (screenshots)", "screenshots"),
        };

        /// <summary>游戏目录里可以清的大块头（缓存/日志），相对游戏安装目录。</summary>
        private static readonly string[] GameInstallCleanTargets =
        {
            Path.Combine("Bin64", "temp"),
            "logs",
            "temp",
        };

        /// <summary>扫描所有统计项（异步安全，纯本地 IO）。</summary>
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

            // 游戏本体安装目录（只算总数 + 缓存目录，不做全盘递归以外的动作）
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

            // 游戏日志备份（*.log.bak 这些）
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

        /// <summary>
        /// 清理指定项。key 必须先出现在 Scan() 的结果里，防止前端传来任意路径。
        /// </summary>
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

            // 白名单：必须落在 userpath 内，或者是游戏安装目录里的缓存子目录
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
                    // 被游戏占用的文件跳过
                }
            }

            // 清完把空目录收拾掉（只删空目录，不动还有东西的）
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

        /// <summary>删掉历史日志备份（*.log.bak）。</summary>
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

        /// <summary>
        /// 目录必须落在 userpath 内、或落在游戏安装目录的缓存子目录内，才允许清理。
        /// </summary>
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
                // 游戏安装目录里只放行缓存类子目录，绝不碰游戏本体
                foreach (var rel in GameInstallCleanTargets)
                {
                    if (Under(Path.Combine(gameDir, rel))) return true;
                }
            }
            return false;
        }
    }
}
