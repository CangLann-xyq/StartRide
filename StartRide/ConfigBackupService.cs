using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace StartRide.Core
{
    /// <summary>一份配置备份。</summary>
    public sealed class ConfigBackupEntry
    {
        public string Path { get; init; } = "";
        public string FileName { get; init; } = "";
        public long SizeBytes { get; init; }
        public DateTime CreatedAt { get; init; }
        public int FileCount { get; init; }

        public string DisplayName => FileName;
        public string SizeText => FileSizeFormatter.Format(SizeBytes);

        /// <summary>界面副标题：「12 个文件 · 2026-09-19 21:30」。</summary>
        public string DetailText => FileCount + " 个文件 · " + CreatedAt.ToString("yyyy-MM-dd HH:mm");
    }

    /// <summary>备份 / 还原的结果。</summary>
    public sealed class ConfigBackupResult
    {
        public bool Success { get; init; }
        public string Path { get; init; } = "";
        public int FileCount { get; init; }
        public long SizeBytes { get; init; }
        public string Message { get; init; } = "";
    }

    /// <summary>
    /// 游戏用户配置（&lt;userpath&gt;\settings）一键备份 / 还原。
    ///
    /// BeamNG 的画面、按键、UI 布局全都在这个目录里；调了很久的按键映射被游戏
    /// 更新或误操作冲掉是非常常见的事，所以做成一个 zip 快照。
    /// 还原前会先自动给"当前状态"再存一份，避免还原错了没法回头。
    /// </summary>
    public sealed class ConfigBackupService
    {
        private readonly AppSettings _settings;

        public ConfigBackupService(AppSettings settings) => _settings = settings;

        /// <summary>备份存放目录：%AppData%\StartRide\backups。</summary>
        public static string BackupDirectory => Path.Combine(AppSettings.ConfigDirectory, "backups");

        /// <summary>要备份的内容（相对 userpath 的路径）。</summary>
        private static readonly string[] IncludeDirs = { "settings", "vehicles" };
        private static readonly string[] IncludeFiles = { "knownFiles.json", "consoleHistory.json" };

        /// <summary>列出已有备份，新的在前。</summary>
        public List<ConfigBackupEntry> ListBackups()
        {
            var list = new List<ConfigBackupEntry>();
            try
            {
                if (!Directory.Exists(BackupDirectory)) return list;
                foreach (var f in Directory.GetFiles(BackupDirectory, "*.zip"))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        int count = 0;
                        using (var zip = ZipFile.OpenRead(f))
                        {
                            count = zip.Entries.Count;
                        }
                        list.Add(new ConfigBackupEntry
                        {
                            Path = f,
                            FileName = fi.Name,
                            SizeBytes = fi.Length,
                            CreatedAt = fi.LastWriteTime,
                            FileCount = count,
                        });
                    }
                    catch
                    {
                        // 单个 zip 坏了不影响其它备份的展示
                    }
                }
            }
            catch
            {
                // 目录不可读就当作没有备份
            }
            return list.OrderByDescending(b => b.CreatedAt).ToList();
        }

        /// <summary>
        /// 打包一份新备份。只收 config 类文件，不碰 mods 大文件（模组几十 GB，备份没意义）。
        /// </summary>
        public ConfigBackupResult CreateBackup(string? label = null)
        {
            try
            {
                string userPath = _settings.ResolveUserDataRoot();
                if (!Directory.Exists(userPath))
                {
                    return new ConfigBackupResult { Success = false, Message = "找不到游戏用户目录：" + userPath };
                }

                Directory.CreateDirectory(BackupDirectory);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string suffix = string.IsNullOrWhiteSpace(label) ? "" : "-" + Sanitize(label!);
                string zipPath = Path.Combine(BackupDirectory, $"StartRide-config-{stamp}{suffix}.zip");

                int count = 0;
                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    // 1) 目录：settings（画面/按键/UI 布局）、vehicles（车辆调校存档）
                    foreach (var dirName in IncludeDirs)
                    {
                        string dir = Path.Combine(userPath, dirName);
                        if (!Directory.Exists(dir)) continue;
                        count += AddDirectory(zip, dir, userPath);
                    }

                    // 2) 零散配置文件
                    foreach (var fileName in IncludeFiles)
                    {
                        string file = Path.Combine(userPath, fileName);
                        if (!File.Exists(file)) continue;
                        zip.CreateEntryFromFile(file, fileName, CompressionLevel.Optimal);
                        count++;
                    }

                    // 3) 备份说明（用户自己看 zip 内容时能知道从哪来的）
                    var readme = zip.CreateEntry("backup-info.txt", CompressionLevel.Optimal);
                    using var w = new StreamWriter(readme.Open());
                    w.WriteLine("StartRide 配置备份");
                    w.WriteLine("备份时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    w.WriteLine("游戏用户目录：" + userPath);
                    w.WriteLine("游戏版本：" + new GameLauncher(_settings).GameVersion);
                    w.WriteLine("文件数：" + count);
                }

                var info = new FileInfo(zipPath);
                var app = _settings;
                app.LastBackupPath = zipPath;
                app.Save();

                return new ConfigBackupResult
                {
                    Success = true,
                    Path = zipPath,
                    FileCount = count,
                    SizeBytes = info.Length,
                    Message = "已备份 " + count + " 个文件",
                };
            }
            catch (Exception ex)
            {
                return new ConfigBackupResult { Success = false, Message = ex.Message };
            }
        }

        private static int AddDirectory(ZipArchive zip, string directory, string userPath)
        {
            int count = 0;
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    string rel = Path.GetRelativePath(userPath, file).Replace('\\', '/');
                    zip.CreateEntryFromFile(file, rel, CompressionLevel.Optimal);
                    count++;
                }
                catch
                {
                    // 单个文件被游戏占用读不到就跳过，不整体失败
                }
            }
            return count;
        }

        /// <summary>
        /// 还原：先把当前状态自动备份一份（.before-restore），再解压覆盖。
        /// </summary>
        public ConfigBackupResult RestoreBackup(string zipPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                {
                    return new ConfigBackupResult { Success = false, Message = "备份文件不存在" };
                }

                string userPath = _settings.ResolveUserDataRoot();
                Directory.CreateDirectory(userPath);

                // 还原前先留存"当前状态"，还原错了还能退回来
                var safety = CreateBackup("before-restore");
                if (!safety.Success)
                {
                    return new ConfigBackupResult { Success = false, Message = "还原前自动备份失败：" + safety.Message };
                }

                int count = 0;
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue;   // 目录项
                        if (entry.FullName.Equals("backup-info.txt", StringComparison.OrdinalIgnoreCase)) continue;

                        string target = Path.Combine(userPath, entry.FullName.Replace('/', '\\'));

                        // 防目录穿越：解出来的路径必须还在 userpath 里
                        string full = Path.GetFullPath(target);
                        if (!full.StartsWith(Path.GetFullPath(userPath), StringComparison.OrdinalIgnoreCase)) continue;

                        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                        entry.ExtractToFile(full, overwrite: true);
                        count++;
                    }
                }

                return new ConfigBackupResult
                {
                    Success = true,
                    Path = zipPath,
                    FileCount = count,
                    Message = "已还原 " + count + " 个文件（还原前状态已存为 " + Path.GetFileName(safety.Path) + "）",
                };
            }
            catch (Exception ex)
            {
                return new ConfigBackupResult { Success = false, Message = ex.Message };
            }
        }

        /// <summary>删掉一份备份。</summary>
        public bool DeleteBackup(string zipPath)
        {
            try
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string Sanitize(string s)
        {
            var chars = s.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray();
            string clean = new string(chars).Trim();
            return clean.Length > 24 ? clean[..24] : clean;
        }
    }
}
