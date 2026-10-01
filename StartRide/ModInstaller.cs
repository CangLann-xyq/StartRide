using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace StartRide.Core
{

    public sealed class ModInstaller
    {
        public const string ModPackageName = "startride.zip";

        private static string ModVersion => BuildInfo.Version;

        private const string StampEntry = "scripts/startride/build.stamp";

        private readonly AppSettings _settings;

        public ModInstaller(AppSettings settings) => _settings = settings;

        public event Action<string>? Log;

        private static string SourceDirectory =>
            Path.Combine(AppContext.BaseDirectory, "Mods");

        public string ModsDirectory => _settings.ResolveModsDirectory();
        public string InstalledPackagePath => Path.Combine(ModsDirectory, ModPackageName);

        public bool IsInstalled => File.Exists(InstalledPackagePath);

        public bool IsUpToDate
        {
            get
            {
                try
                {
                    string ge = Path.Combine(SourceDirectory, "startride_mod.lua");
                    string ve = Path.Combine(SourceDirectory, "startrideVE.lua");
                    string hl = Path.Combine(SourceDirectory, "startrideHL.lua");
                    if (!File.Exists(ge) || !File.Exists(ve) || !File.Exists(hl)) return false;
                    if (!IsInstalled) return false;

                    return string.Equals(
                        ReadStamp(InstalledPackagePath),
                        ComputeSourceStamp(ge, ve, hl),
                        StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }
        }

        public DateTime? InstalledAt =>
            IsInstalled ? File.GetLastWriteTime(InstalledPackagePath) : null;

        public static string PendingRemovalFile =>
            Path.Combine(StartRidePaths.Root, "mod-remove.pending");

        public static bool HasPendingRemoval => File.Exists(PendingRemovalFile);

        public static bool IsGameRunning()
        {
            try
            {
                var procs = Process.GetProcessesByName("BeamNG.drive");
                bool running = procs.Length > 0;
                foreach (var p in procs) { try { p.Dispose(); } catch { } }
                return running;
            }
            catch
            {
                return false;
            }
        }

        private void MarkPendingRemoval(string reason)
        {
            try
            {
                Directory.CreateDirectory(StartRidePaths.Root);
                File.WriteAllText(PendingRemovalFile,
                    $"{DateTimeOffset.Now:o}\n{reason}\n{InstalledPackagePath}\n");
                Log?.Invoke("联机模组已标记为待移除：" + reason);
            }
            catch (Exception ex)
            {
                Log?.Invoke("写待移除标记失败（不影响游戏）：" + ex.Message);
            }
        }

        private void ClearPendingRemoval()
        {
            try
            {
                if (File.Exists(PendingRemovalFile)) File.Delete(PendingRemovalFile);
            }
            catch {                       }
        }

        public string? Install(bool backupExisting = true)
        {
            try
            {
                string ge = Path.Combine(SourceDirectory, "startride_mod.lua");
                string ve = Path.Combine(SourceDirectory, "startrideVE.lua");
                string hl = Path.Combine(SourceDirectory, "startrideHL.lua");

                if (!File.Exists(ge)) return $"找不到模组源文件：{ge}";
                if (!File.Exists(ve)) return $"找不到模组源文件：{ve}";
                if (!File.Exists(hl)) return $"找不到模组源文件：{hl}";

                string stamp = ComputeSourceStamp(ge, ve, hl);

                if (File.Exists(InstalledPackagePath) &&
                    string.Equals(ReadStamp(InstalledPackagePath), stamp, StringComparison.OrdinalIgnoreCase))
                {
                    ClearPendingRemoval();
                    return null;
                }

                Directory.CreateDirectory(ModsDirectory);

                if (backupExisting && File.Exists(InstalledPackagePath))
                {
                    try
                    {
                        string bak = InstalledPackagePath + ".bak_" +
                                     DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        File.Copy(InstalledPackagePath, bak, overwrite: true);
                        CleanupBackups(keep: 1);
                    }
                    catch {                 }
                }

                string infoJson = "{\"name\":\"StartRide Multiplayer\",\"author\":\"CloudFur Studio\"," +
                                  $"\"version\":\"{ModVersion}\",\"description\":\"StartRide multiplayer integration\"," +
                                  "\"type\":\"script\",\"minGameVersion\":\"0.30\"}";

                string tmp = InstalledPackagePath + ".tmp";
                if (File.Exists(tmp)) File.Delete(tmp);

                using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    AddText(zip, "info.json", infoJson);
                    AddFile(zip, "lua/ge/extensions/startride.lua", ge);
                    AddFile(zip, "lua/vehicle/extensions/auto/startrideVE.lua", ve);
                    AddFile(zip, "lua/vehicle/extensions/startride/startrideVE.lua", ve);
                    AddFile(zip, "lua/vehicle/extensions/auto/startrideHL.lua", hl);
                    AddText(zip, "mod_info/startride/info.json", infoJson);
                    AddText(zip, "scripts/startride/modScript.lua",
                            "setExtensionUnloadMode(\"startride\", \"manual\")");
                    AddText(zip, StampEntry, stamp);
                }

                if (File.Exists(InstalledPackagePath))
                {
                    try { File.Replace(tmp, InstalledPackagePath, null); }
                    catch
                    {
                        File.Copy(tmp, InstalledPackagePath, overwrite: true);
                        File.Delete(tmp);
                    }
                }
                else
                {
                    File.Move(tmp, InstalledPackagePath);
                }

                ClearPendingRemoval();

                Log?.Invoke($"联机模组已安装：{InstalledPackagePath}");
                return null;
            }
            catch (Exception ex)
            {
                Log?.Invoke("安装联机模组失败：" + ex.Message);
                return ex.Message;
            }
        }

        public string? Remove()
        {
            try
            {
                if (!File.Exists(InstalledPackagePath))
                {
                    ClearPendingRemoval();
                    return null;
                }

                File.Delete(InstalledPackagePath);
                ClearPendingRemoval();

                CleanupBackups(keep: 0);

                Log?.Invoke("联机模组已移除（退出联机，mods 目录已恢复干净）");
                return null;
            }
            catch (Exception ex)
            {
                MarkPendingRemoval("删除失败（文件被占用）：" + ex.Message);
                return ex.Message;
            }
        }

        public string? RemoveAfterSession()
        {
            if (!_settings.RemoveModOnLeave) return null;

            if (IsGameRunning())
            {
                MarkPendingRemoval("游戏仍在运行，等游戏退出后自动清理");
                return null;
            }

            return Remove();
        }

        public string? ApplyPendingRemoval()
        {
            if (!File.Exists(PendingRemovalFile)) return null;
            if (IsGameRunning()) return null;
            return Remove();
        }

        public string? RemoveStaleOnStartup()
        {
            if (!_settings.RemoveModOnLeave) return null;
            if (!IsInstalled) return null;

            if (IsGameRunning())
            {
                MarkPendingRemoval("启动时发现遗留模组，游戏仍在运行，等游戏退出后自动清理");
                return null;
            }

            Log?.Invoke("清理启动时遗留的联机模组（当前不在联机中）");
            return Remove();
        }

        public void CleanupBackups(int keep = 1)
        {
            try
            {
                string dir = Path.GetDirectoryName(InstalledPackagePath) ?? ModsDirectory;
                if (!Directory.Exists(dir)) return;

                string pattern = ModPackageName + ".bak_*";
                var baks = Directory.GetFiles(dir, pattern)
                    .OrderByDescending(f => f, StringComparer.Ordinal)
                    .ToList();

                for (int i = keep; i < baks.Count; i++)
                {
                    try { File.Delete(baks[i]); } catch { }
                }
            }
            catch { }
        }

        public List<(string Name, long Size, DateTime Modified)> ListInstalledMods()
        {
            var list = new List<(string, long, DateTime)>();
            try
            {
                if (!Directory.Exists(ModsDirectory)) return list;
                foreach (var f in Directory.GetFiles(ModsDirectory, "*.zip"))
                {
                    var fi = new FileInfo(f);
                    list.Add((Path.GetFileNameWithoutExtension(f), fi.Length, fi.LastWriteTime));
                }
            }
            catch { }
            list.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static string ComputeSourceStamp(string ge, string ve, string hl)
        {
            using var sha = SHA256.Create();
            using var ms = new MemoryStream();

            foreach (var path in new[] { ge, ve, hl })
            {
                var info = new FileInfo(path);
                var header = Encoding.UTF8.GetBytes(info.Name + ":" + info.Length + "\n");
                ms.Write(header, 0, header.Length);
                using var fs = File.OpenRead(path);
                fs.CopyTo(ms);
            }

            ms.Position = 0;
            return Convert.ToHexString(sha.ComputeHash(ms));
        }

        private static string? ReadStamp(string zipPath)
        {
            try
            {
                using var zip = ZipFile.OpenRead(zipPath);
                var entry = zip.GetEntry(StampEntry);
                if (entry == null) return null;
                using var s = entry.Open();
                using var r = new StreamReader(s, Encoding.UTF8);
                string text = r.ReadToEnd().Trim();
                return text.Length > 0 ? text : null;
            }
            catch
            {
                return null;
            }
        }

        private static void AddText(ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var s = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            s.Write(bytes, 0, bytes.Length);
        }

        private static void AddFile(ZipArchive zip, string entryName, string path)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var s = entry.Open();
            using var src = File.OpenRead(path);
            src.CopyTo(s);
        }
    }
}
