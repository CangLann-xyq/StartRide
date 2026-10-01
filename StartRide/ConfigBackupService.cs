using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace StartRide.Core
{
    public sealed class ConfigBackupEntry
    {
        public string Path { get; init; } = "";
        public string FileName { get; init; } = "";
        public long SizeBytes { get; init; }
        public DateTime CreatedAt { get; init; }
        public int FileCount { get; init; }

        public string DisplayName => FileName;
        public string SizeText => FileSizeFormatter.Format(SizeBytes);

        public string DetailText => FileCount + " 个文件 · " + CreatedAt.ToString("yyyy-MM-dd HH:mm");
    }

    public sealed class ConfigBackupResult
    {
        public bool Success { get; init; }
        public string Path { get; init; } = "";
        public int FileCount { get; init; }
        public long SizeBytes { get; init; }
        public string Message { get; init; } = "";
    }

    public sealed class ConfigBackupService
    {
        private readonly AppSettings _settings;

        public ConfigBackupService(AppSettings settings) => _settings = settings;

        public static string BackupDirectory => Path.Combine(AppSettings.ConfigDirectory, "backups");

        private static readonly string[] IncludeDirs = { "settings", "vehicles" };
        private static readonly string[] IncludeFiles = { "knownFiles.json", "consoleHistory.json" };

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
                    }
                }
            }
            catch
            {
            }
            return list.OrderByDescending(b => b.CreatedAt).ToList();
        }

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
                    foreach (var dirName in IncludeDirs)
                    {
                        string dir = Path.Combine(userPath, dirName);
                        if (!Directory.Exists(dir)) continue;
                        count += AddDirectory(zip, dir, userPath);
                    }

                    foreach (var fileName in IncludeFiles)
                    {
                        string file = Path.Combine(userPath, fileName);
                        if (!File.Exists(file)) continue;
                        zip.CreateEntryFromFile(file, fileName, CompressionLevel.Optimal);
                        count++;
                    }

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
                }
            }
            return count;
        }

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
                        if (string.IsNullOrEmpty(entry.Name)) continue;
                        if (entry.FullName.Equals("backup-info.txt", StringComparison.OrdinalIgnoreCase)) continue;

                        string target = Path.Combine(userPath, entry.FullName.Replace('/', '\\'));

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
