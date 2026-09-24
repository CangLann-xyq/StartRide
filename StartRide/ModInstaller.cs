using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace StartRide.Core
{
    /// <summary>
    /// 把 Mods/ 下的两个 Lua 文件打包成 startride.zip 并装进游戏 mods 目录。
    ///
    /// 打包结构（顺序与路径都不能改）：
    ///   info.json
    ///   lua/ge/extensions/startride.lua                        &lt;- startride_mod.lua（GE 侧）
    ///   lua/vehicle/extensions/auto/startrideVE.lua            &lt;- startrideVE.lua（VE 侧，自动加载）
    ///   lua/vehicle/extensions/startride/startrideVE.lua       &lt;- 同一份，供 spawnRemote 显式加载
    ///   mod_info/startride/info.json
    ///   scripts/startride/modScript.lua
    ///
    /// 为什么必须写两份 VE：BeamNG 只在 postspawn 自动加载 lua/vehicle/extensions/auto/，
    /// 放到 extensions/ 根目录永远不会被加载；而 GE 侧生成远程车时又会显式
    /// loadModulesInDirectory('lua/vehicle/extensions/startride')，所以两份都要有。
    /// （VE 内用 v.srVEModule 守卫去重，不会重复注册钩子。）
    /// </summary>
    public sealed class ModInstaller
    {
        public const string ModPackageName = "startride.zip";
        private const string ModVersion = "2.7.0";

        private readonly AppSettings _settings;

        public ModInstaller(AppSettings settings) => _settings = settings;

        public event Action<string>? Log;

        private static string SourceDirectory =>
            Path.Combine(AppContext.BaseDirectory, "Mods");

        public string ModsDirectory => _settings.ResolveModsDirectory();
        public string InstalledPackagePath => Path.Combine(ModsDirectory, ModPackageName);

        public bool IsInstalled => File.Exists(InstalledPackagePath);

        /// <summary>已安装包的时间戳，用于判断是否需要重新安装。</summary>
        public DateTime? InstalledAt =>
            IsInstalled ? File.GetLastWriteTime(InstalledPackagePath) : null;

        /// <summary>
        /// 重新打包并安装。返回 null 表示成功，否则返回错误说明。
        /// 注意：启动器每次启动都会无条件重写该包，所以改了 Mods/ 下的 Lua
        /// 必须重新运行启动器（或重新打包 EXE）才会生效。
        /// </summary>
        public string? Install(bool backupExisting = true)
        {
            try
            {
                string ge = Path.Combine(SourceDirectory, "startride_mod.lua");
                string ve = Path.Combine(SourceDirectory, "startrideVE.lua");

                if (!File.Exists(ge)) return $"找不到模组源文件：{ge}";
                if (!File.Exists(ve)) return $"找不到模组源文件：{ve}";

                Directory.CreateDirectory(ModsDirectory);

                if (backupExisting && File.Exists(InstalledPackagePath))
                {
                    try
                    {
                        string bak = InstalledPackagePath + ".bak_" +
                                     DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        File.Copy(InstalledPackagePath, bak, overwrite: true);
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
                    AddText(zip, "mod_info/startride/info.json", infoJson);
                    AddText(zip, "scripts/startride/modScript.lua",
                            "setExtensionUnloadMode(\"startride\", \"manual\")");
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

                Log?.Invoke($"联机模组已安装：{InstalledPackagePath}");
                return null;
            }
            catch (Exception ex)
            {
                Log?.Invoke("安装联机模组失败：" + ex.Message);
                return ex.Message;
            }
        }

        public string? Uninstall()
        {
            try
            {
                if (File.Exists(InstalledPackagePath)) File.Delete(InstalledPackagePath);
                Log?.Invoke("联机模组已卸载");
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
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
