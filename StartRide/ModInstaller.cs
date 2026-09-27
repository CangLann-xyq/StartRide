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
    /// <summary>
    /// 把 Mods/ 下的三个 Lua 文件打包成 startride.zip 并装进游戏 mods 目录。
    ///
    /// 打包结构（顺序与路径都不能改）：
    ///   info.json
    ///   lua/ge/extensions/startride.lua                        &lt;- startride_mod.lua（GE 侧）
    ///   lua/vehicle/extensions/auto/startrideVE.lua            &lt;- startrideVE.lua（VE 侧，自动加载）
    ///   lua/vehicle/extensions/startride/startrideVE.lua       &lt;- 同一份，供 spawnRemote 显式加载
    ///   lua/vehicle/extensions/auto/startrideHL.lua            &lt;- startrideHL.lua（高光检测，只对玩家车生效）
    ///   mod_info/startride/info.json
    ///   scripts/startride/modScript.lua
    ///   scripts/startride/build.stamp                          &lt;- 源文件指纹（我们自用，游戏忽略）
    ///
    /// 为什么高光检测只放 auto/ 一份：它只对"玩家乘坐的车"生效（模块内部按
    /// playerInfo.anyPlayerSeated + 非远程车两道守卫过滤），而玩家车由游戏正常生成，
    /// 必然走 auto/ 自动加载这条路；远程车那份挂上也是空转，不需要显式再塞一份。
    ///
    /// 为什么必须写两份 VE：BeamNG 只在 postspawn 自动加载 lua/vehicle/extensions/auto/，
    /// 放到 extensions/ 根目录永远不会被加载；而 GE 侧生成远程车时又会显式
    /// loadModulesInDirectory('lua/vehicle/extensions/startride')，所以两份都要有。
    /// （VE 内用 v.srVEModule 守卫去重，不会重复注册钩子。）
    ///
    /// ── 生命周期（2.13 起，按需装卸）────────────────────────────────────────
    /// 默认 <see cref="AppSettings.RemoveModOnLeave"/> = true：
    ///   进入联机（开房 / 进房）时才安装；退出 / 解散房间后移除。
    ///   游戏还在运行时**先挂一个 pending 标记**（游戏可能正读着这个包，而且删了
    ///   当前会话里已经加载的扩展也收不回来），等游戏进程结束或下次启动启动器时真删。
    ///
    /// 安装是**幂等**的：源文件指纹与包内记录一致就完全不碰磁盘 —— 既不重打包，
    /// 也不产生 .bak_*。改动 Mods/ 下的 Lua 之后指纹会变，下次启动器启动（或下次联机）
    /// 才会重新打包，所以「改完 Lua 必须让启动器重跑一遍」这条依然成立。
    /// </summary>
    public sealed class ModInstaller
    {
        public const string ModPackageName = "startride.zip";

        /// <summary>
        /// 包内版本号。**必须从 <see cref="BuildInfo.Version"/> 取**：以前这里是硬编码的
        /// "1.2.3"，发版时忘了同步就会出现"包里是旧版、界面说新版"。注意这只是
        /// zip 的 info.json 字段，游戏内日志里那行 `[StartRide VE] v1.2.3 已加载`
        /// 是 Lua 里的独立文案，两者互不相干。
        /// </summary>
        private static string ModVersion => BuildInfo.Version;

        /// <summary>源文件指纹条目（放在 scripts/ 下，游戏只认 modScript.lua，不会解析它）。</summary>
        private const string StampEntry = "scripts/startride/build.stamp";

        private readonly AppSettings _settings;

        public ModInstaller(AppSettings settings) => _settings = settings;

        public event Action<string>? Log;

        private static string SourceDirectory =>
            Path.Combine(AppContext.BaseDirectory, "Mods");

        public string ModsDirectory => _settings.ResolveModsDirectory();
        public string InstalledPackagePath => Path.Combine(ModsDirectory, ModPackageName);

        public bool IsInstalled => File.Exists(InstalledPackagePath);

        /// <summary>
        /// 包与当前 Mods/ 下的源文件是不是完全一致（内容指纹相同）。「没装」也算 false。
        ///
        /// 用它判断"要不要重装"比时间戳可靠得多 —— 源文件是
        /// <c>CopyToOutputDirectory</c> 复制过来的，时间戳会跟着安装包一起刷新，
        /// 拿时间戳比每次都会误报"模组落后了"。
        /// </summary>
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

        /// <summary>已安装包的时间戳，用于显示（判断新鲜度请用 <see cref="IsUpToDate"/>）。</summary>
        public DateTime? InstalledAt =>
            IsInstalled ? File.GetLastWriteTime(InstalledPackagePath) : null;

        // ==================== 待删标记 ====================

        /// <summary>
        /// "退出联机后要删，但游戏当时还在跑"的标记文件。
        /// 放在启动器自己的数据目录（不在游戏 mods 里，游戏不会看到它）。
        /// </summary>
        public static string PendingRemovalFile =>
            Path.Combine(StartRidePaths.Root, "mod-remove.pending");

        public static bool HasPendingRemoval => File.Exists(PendingRemovalFile);

        /// <summary>游戏进程是否在跑（跨启动器重启也准，所以直接查进程而不是看自家 Process 字段）。</summary>
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
                // 查不到就当成没在跑：删不掉时下面的 Remove 会重新挂标记
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
            catch { /* 标记删不掉无害，下次删除时再试 */ }
        }

        // ==================== 安装 ====================

        /// <summary>
        /// 打包并安装。返回 null 表示成功（或**内容已是最新，无需动作**），否则返回错误说明。
        ///
        /// 幂等：源文件（三个 .lua）指纹与包内 build.stamp 一致时直接返回，不写盘。
        /// </summary>
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

                // 已装且内容一致 → 什么都不做。这一步让"启动器每次启动都重打包"成为历史：
                // 既不会在 mods/ 里堆 .bak_*，也不会因为重写包而打断正在跑的游戏。
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
                    catch { /* 备份失败不影响安装 */ }
                }

                string infoJson = "{\"name\":\"StartRide Multiplayer\",\"author\":\"CloudFur Studio\"," +
                                  $"\"version\":\"{ModVersion}\",\"description\":\"StartRide multiplayer integration\"," +
                                  "\"type\":\"script\",\"minGameVersion\":\"0.30\"}";

                // 先写临时文件再改名，避免游戏正在读时写出半个包
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
                    // EDR/游戏占用时改名可能失败，退化为就地覆盖
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

                // 装回来了，之前的"待移除"作废
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

        // ==================== 移除 ====================

        /// <summary>
        /// 立即移除联机模组。返回 null 表示成功。
        /// 删不掉（游戏占着文件）时会留下待移除标记，交给 <see cref="ApplyPendingRemoval"/>。
        /// </summary>
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

                // 顺手把以前版本堆下来的备份也收掉，mods/ 里不留联机模组的痕迹
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

        /// <summary>
        /// 联机会话结束时的收尾。仅当 <see cref="AppSettings.RemoveModOnLeave"/> 为真才动手。
        /// 游戏还在跑就先挂标记 —— 此刻删掉，游戏当前会话里已经加载的扩展也收不回来，
        /// 反而可能在重载车辆时踩到"文件不存在"。
        /// </summary>
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

        /// <summary>
        /// 启动器启动时 / 游戏进程退出后调用：有标记且游戏不在跑就真删。
        /// 返回 null 表示无事可做或已处理完。
        /// </summary>
        public string? ApplyPendingRemoval()
        {
            if (!File.Exists(PendingRemovalFile)) return null;
            if (IsGameRunning()) return null;
            return Remove();
        }

        /// <summary>
        /// 启动时的「遗留清理」：<see cref="AppSettings.RemoveModOnLeave"/> 为真时，模组只该在联机期间存在。
        /// 以前版本装的、或上次联机异常中断留下的那一份，在没有会话时一并收掉
        /// （游戏在跑就交给待删标记，不能此刻删）。
        ///
        /// 与 <see cref="ApplyPendingRemoval"/> 的区别：这条**不看标记**，只看"按设置该不该有"。
        /// 有了它，"不联机时 mods 目录永远干净"才是真的闭环，而不是只在正常退出路径成立。
        /// </summary>
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

        /// <summary>清掉历史遗留的 .bak_* 备份，只保留最近 <paramref name="keep"/> 份。</summary>
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

        /// <summary>列出 mods 目录里的远程车辆候选（供"可装模组/已装模组"页面展示）。</summary>
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

        // ==================== 指纹 ====================

        /// <summary>
        /// 三个源文件的 SHA-256 指纹（含各自文件名与长度，避免顺序变化产生歧义）。
        /// 只要 Lua 内容有任何一个字节变化，指纹就变 → 下次 Install 会重新打包。
        /// </summary>
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

        /// <summary>读包内记录的指纹；包坏了/是旧版格式（没有该条目）都返回 null。</summary>
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
                // 包损坏 / 不是 zip → 当作"需要重装"
                return null;
            }
        }

        // ==================== zip 写入 ====================

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
