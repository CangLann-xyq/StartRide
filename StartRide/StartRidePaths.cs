using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StartRide.Core
{
    /// <summary>
    /// StartRide 自己的「配置与文件夹」**唯一收敛点**。
    ///
    /// 背景：这个启动器是在一个 Minecraft 启动器反编译出来的框架上改造的。框架层
    /// （三个无源码的 Launcher.*.dll）把数据目录算成了三处带它自己命名习惯的路径：
    ///     LauncherPathProvider.ApplicationId              = "BHL"
    ///     LauncherPathProvider.DefaultDataDirectory       = &lt;EXE&gt;\BHL            ← 设置文件
    ///     LauncherPathProvider.DefaultAccountDataDirectory= %APPDATA%\BHL\accounts ← 账户状态
    ///     LauncherPathProvider.DefaultMinecraftDirectory   = &lt;EXE&gt;\.minecraft
    /// 以前是靠「目录联接把这三个名字兜到 %APPDATA%\StartRide\app」遮掩过去的。
    ///
    /// 现在改成正面接管：这些路径全部由 DI 注入我们自己的值（构造参数本来就是可注入的），
    /// 于是框架那三处默认值根本不会被用到，界面上、磁盘上都不再出现框架的命名。
    /// 应用启动时会调用 <see cref="MigrateLegacyLayout"/> 把老布局的数据搬过来。
    /// </summary>
    public static class StartRidePaths
    {
        /// <summary>应用数据根目录：%APPDATA%\StartRide</summary>
        public static string Root =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StartRide");

        /// <summary>自有启动器设置文件（<see cref="AppSettings"/> 的唯一落点，47 项）。框架那套在 <see cref="LauncherState"/>，两者不可混用。</summary>
        public static string SettingsFile => Path.Combine(Root, "settings.json");

        /// <summary>账户与账户皮肤/披风缓存</summary>
        public static string Accounts => Path.Combine(Root, "accounts");

        /// <summary>游戏侧数据目录（顶替框架默认的 .minecraft）</summary>
        public static string GameData => Path.Combine(Root, "game");

        /// <summary>
        /// 框架层（<c>ISettingsService</c> / <c>JsonSettingsService</c>）自己的设置文件目录。
        ///
        /// ⚠️ 必须与 <see cref="Root"/> 分开，否则会和 <see cref="SettingsFile"/> 撞成同一个文件：
        /// JsonSettingsService 写的是「&lt;dataDirectory&gt;\settings.json」，而自有设置也正好在
        /// 「&lt;Root&gt;\settings.json」。两者 schema 完全不同（框架：Revision/Theme/AccentColor/
        /// LauncherLanguage…；自有：GameDirectory/MaxMemoryMB/RelayHost… 共 47 项），
        /// 谁后写谁把对方整体抹掉。实测（fwprobe saveprobe）确认了框架写的就是 settings.json：
        /// 框架保存一次 → 用户自有设置全变默认值；自有设置保存一次 → 主题/语言/窗口位置被清空。
        /// 因此框架那套独立到 Root\launcher\settings.json，两边各写各的。
        /// </summary>
        public static string LauncherState => Path.Combine(Root, "launcher");

        public static string Log => Path.Combine(Root, "Log");
        public static string Avatars => Path.Combine(Root, "avatars");
        public static string Backups => Path.Combine(Root, "backups");
        public static string Diagnostics => Path.Combine(Root, "diagnostics");

        /// <summary>老布局：所有东西都在 <see cref="Root"/>\app 里（当时靠目录联接把 BHL 指过去）。</summary>
        public static string LegacyAppDirectory => Path.Combine(Root, "app");

        /// <summary>框架留下的、我们已不再使用的目录名（迁移后仍可能存在，仅用于显示名过滤）。</summary>
        public static readonly string[] LegacyDirectoryNames =
        {
            ".minecraft", "cache", "images", "tools", "BHL"
        };

        /// <summary>历史遗留的框架语义目录（用于「排除目录」与显示名清理）。</summary>
        public static IEnumerable<string> LegacyDirectories()
        {
            yield return Path.Combine(Root, ".minecraft");
            yield return Path.Combine(LegacyAppDirectory, ".minecraft");
            yield return Path.Combine(AppContext.BaseDirectory, ".minecraft");
            yield return Path.Combine(AppContext.BaseDirectory, "BHL");
        }

        /// <summary>
        /// 把老布局（%APPDATA%\StartRide\app 下的 settings.json / accounts）搬到新布局。
        /// 幂等：目标已存在就跳过，绝不做删除。
        /// </summary>
        public static void MigrateLegacyLayout()
        {
            try { Directory.CreateDirectory(Root); } catch { }
            try { MoveIfMissing(Path.Combine(LegacyAppDirectory, "settings.json"), SettingsFile); } catch { }
            try { MoveDirectoryIfMissing(Path.Combine(LegacyAppDirectory, "accounts"), Accounts); } catch { }
        }

        /// <summary>
        /// 确保自有布局的目录都存在。必须在读取设置之前调用：设置里持久化了
        /// MinecraftDirectory，若目标目录不存在，框架会在启动阶段走「目录恢复」并抛致命错误。
        /// </summary>
        public static void EnsureLayout()
        {
            foreach (string dir in new[] { Root, Accounts, GameData, Log, Avatars, Backups, Diagnostics, LauncherState })
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
        }

        private static void MoveIfMissing(string source, string target)
        {
            if (!File.Exists(source) || File.Exists(target)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(source, target);
        }

        private static void MoveDirectoryIfMissing(string source, string target)
        {
            if (!Directory.Exists(source)) return;
            if (Directory.Exists(target))
            {
                // 目标已存在：只把缺的文件补过去，不覆盖已有内容
                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(source, file);
                    string dest = Path.Combine(target, rel);
                    if (File.Exists(dest)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Move(file, dest);
                }
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Directory.Move(source, target);
        }

        /// <summary>本机是否还留着框架时代的目录联接/目录（用于设置页提示与诊断包）。</summary>
        public static List<string> FindLegacyArtifacts()
        {
            var found = new List<string>();
            foreach (string dir in LegacyDirectories().Concat(new[]
                     {
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BHL"),
                         LegacyAppDirectory
                     }))
            {
                try { if (Directory.Exists(dir)) found.Add(dir); } catch { }
            }
            return found;
        }
    }
}
