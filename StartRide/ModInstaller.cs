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

        /// <summary>GE 扩展在模组包内的目录（require 按这里解析）。</summary>
        private const string GeExtensionsDir = "lua/ge/extensions";

        /// <summary>
        /// Mods\ 下这些文件是「入口桥」，各有专门的打包落点，不算兄弟模块。
        /// startride_mod.lua → lua/ge/extensions/startride.lua（改名，扩展入口）
        /// startrideVE.lua   → lua/vehicle/extensions/...（车辆侧，两个落点）
        /// startrideHL.lua   → lua/vehicle/extensions/auto/...（车辆侧）
        /// </summary>
        private static readonly HashSet<string> BridgeStems =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "startride_mod", "startrideVE", "startrideHL"
            };

        private readonly AppSettings _settings;

        public ModInstaller(AppSettings settings) => _settings = settings;

        public event Action<string>? Log;

        private static string SourceDirectory =>
            Path.Combine(AppContext.BaseDirectory, "Mods");

        /// <summary>
        /// 列出需要随包分发的「兄弟 Lua 模块」（Mods\*.lua 里除 3 个入口桥之外的全部）。
        ///
        /// startride_mod.lua 运行时用 loadPeer('startrideMode') 这样按名 require，
        /// require 会去 lua/ge/extensions/ 找同名文件 —— 所以这些模块必须一起进包，
        /// 而且落点必须是 &lt;GeExtensionsDir&gt;/&lt;名字&gt;.lua。
        ///
        /// ⚠️ 漏掉任何一个都不会报错：loadPeer 内部是 pcall + nil 兜底，
        /// 失败只写一条日志，玩法直接静默失效。所以这里「宁可全带上」。
        /// </summary>
        private static List<(string Name, string Path)> EnumeratePeerModules()
        {
            var list = new List<(string, string)>();
            try
            {
                string dir = SourceDirectory;
                if (!Directory.Exists(dir)) return list;

                foreach (var path in Directory.GetFiles(dir, "*.lua"))
                {
                    string stem = Path.GetFileNameWithoutExtension(path);
                    if (BridgeStems.Contains(stem)) continue;
                    list.Add((stem, path));
                }
                list.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
            }
            catch { }
            return list;
        }

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
                        ComputeSourceStamp(),
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

                string stamp = ComputeSourceStamp();

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

                // 兄弟模块计数（作用域外也要用，用于安装日志）
                int peerCount = 0;

                using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    AddText(zip, "info.json", infoJson);
                    AddFile(zip, "lua/ge/extensions/startride.lua", ge);
                    AddFile(zip, "lua/vehicle/extensions/auto/startrideVE.lua", ve);
                    AddFile(zip, "lua/vehicle/extensions/startride/startrideVE.lua", ve);
                    AddFile(zip, "lua/vehicle/extensions/auto/startrideHL.lua", hl);

                    // 兄弟模块：startride_mod.lua 用 loadPeer 按名 require 它们，
                    // 必须落在 lua/ge/extensions/ 下且同名，否则静默失效。
                    foreach (var (name, path) in EnumeratePeerModules())
                    {
                        AddFile(zip, GeExtensionsDir + "/" + name + ".lua", path);
                        peerCount++;
                    }
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

                Log?.Invoke($"联机模组已安装：{InstalledPackagePath}（含 {peerCount} 个玩法模块）");
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

        /// <summary>
        /// 对「实际会打进 zip 的全部文件」算指纹。
        ///
        /// ⚠️ 必须覆盖兄弟模块：只算 3 个入口桥的话，新增一个玩法模块
        /// （如 startrideHideSeek.lua）指纹不变 → IsUpToDate 误判为最新 →
        /// Install 直接 return → 新模块永远进不去游戏。
        /// </summary>
        private static string ComputeSourceStamp()
        {
            using var sha = SHA256.Create();
            using var ms = new MemoryStream();

            var files = new List<string>
            {
                Path.Combine(SourceDirectory, "startride_mod.lua"),
                Path.Combine(SourceDirectory, "startrideVE.lua"),
                Path.Combine(SourceDirectory, "startrideHL.lua"),
            };
            foreach (var (_, path) in EnumeratePeerModules()) files.Add(path);

            foreach (var path in files)
            {
                if (!File.Exists(path)) continue;
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
