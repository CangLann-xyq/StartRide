using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace StartRide.Core
{
    /// <summary>
    /// 一键诊断包：把排查问题需要的东西打成一个 zip。
    ///
    /// 收进去的：
    ///   logs/            最近几份启动器日志
    ///   settings.json    启动器设置（不含任何令牌 —— 令牌在 cloud-auth.json，故意不收）
    ///   environment.txt  系统/.NET/硬件/游戏目录/版本/联机中继等环境信息
    ///   game-files.txt   游戏文件体检结果（缺什么、该去 Steam 校验哪些）
    ///
    /// 目的很直接：出了问题不用来回复述环境，导一个包发出去就能看。
    /// </summary>
    public static class DiagnosticsBundleService
    {
        private const int MaxLogFiles = 6;

        /// <summary>诊断包输出目录：%AppData%\StartRide\diagnostics。</summary>
        public static string DiagnosticsDirectory =>
            Path.Combine(AppSettings.ConfigDirectory, "diagnostics");

        /// <summary>
        /// 导出诊断包，返回生成的 zip 完整路径；失败抛异常（调用方负责提示）。
        /// </summary>
        public static string Export(AppSettings settings, string? launcherLogDirectory, Action<string>? progress = null)
        {
            Directory.CreateDirectory(DiagnosticsDirectory);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string zipPath = Path.Combine(DiagnosticsDirectory, "StartRide-diag-" + stamp + ".zip");

            progress?.Invoke("收集环境信息");
            var temp = Path.Combine(Path.GetTempPath(), "StartRide-diag-" + stamp);
            Directory.CreateDirectory(temp);

            try
            {
                File.WriteAllText(Path.Combine(temp, "environment.txt"), BuildEnvironmentText(settings), new UTF8Encoding(false));

                progress?.Invoke("收集游戏文件体检结果");
                try
                {
                    File.WriteAllText(Path.Combine(temp, "game-files.txt"), BuildGameFilesText(settings), new UTF8Encoding(false));
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(temp, "game-files.txt"), "体检失败：" + ex.Message + Environment.NewLine, new UTF8Encoding(false));
                }

                progress?.Invoke("复制启动器设置");
                try
                {
                    if (File.Exists(AppSettings.ConfigPath))
                    {
                        File.Copy(AppSettings.ConfigPath, Path.Combine(temp, "settings.json"), overwrite: true);
                    }
                }
                catch
                {
                    // 设置读不到不影响诊断包
                }

                progress?.Invoke("复制启动器日志");
                string logsDir = Path.Combine(temp, "logs");
                Directory.CreateDirectory(logsDir);
                foreach (string log in EnumerateRecentLogs(launcherLogDirectory).Take(MaxLogFiles))
                {
                    try
                    {
                        File.Copy(log, Path.Combine(logsDir, Path.GetFileName(log)), overwrite: true);
                    }
                    catch
                    {
                        // 单份日志复制失败就跳过（可能是正在写入）
                    }
                }

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
                progress?.Invoke("打包");
                ZipFile.CreateFromDirectory(temp, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

                settings.LastDiagnosticsBundlePath = zipPath;
                settings.Save();
                return zipPath;
            }
            finally
            {
                try
                {
                    Directory.Delete(temp, recursive: true);
                }
                catch
                {
                    // 临时目录清理失败无所谓
                }
            }
        }

        /// <summary>诊断目录里最新的一个包；没有返回 null。</summary>
        public static string? FindLatestBundle()
        {
            try
            {
                if (!Directory.Exists(DiagnosticsDirectory))
                {
                    return null;
                }
                return new DirectoryInfo(DiagnosticsDirectory)
                    .GetFiles("StartRide-diag-*.zip")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Select(f => f.FullName)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<string> EnumerateRecentLogs(string? launcherLogDirectory)
        {
            if (string.IsNullOrWhiteSpace(launcherLogDirectory) || !Directory.Exists(launcherLogDirectory))
            {
                yield break;
            }

            FileInfo[] files;
            try
            {
                files = new DirectoryInfo(launcherLogDirectory).GetFiles("*.log");
            }
            catch
            {
                yield break;
            }

            foreach (var file in files.OrderByDescending(f => f.LastWriteTimeUtc))
            {
                yield return file.FullName;
            }
        }

        private static string BuildEnvironmentText(AppSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("StartRide 诊断包");
            sb.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine("启动器版本：" + BuildInfo.Version);
            sb.AppendLine();
            sb.AppendLine("[系统]");
            sb.AppendLine("操作系统：" + Environment.OSVersion.VersionString);
            sb.AppendLine("64 位进程：" + Environment.Is64BitProcess);
            sb.AppendLine(".NET：" + Environment.Version);
            sb.AppendLine("计算机名：" + Environment.MachineName);
            sb.AppendLine("处理器数：" + Environment.ProcessorCount);

            try
            {
                var gc = GC.GetGCMemoryInfo();
                sb.AppendLine("可用内存：" + (gc.TotalAvailableMemoryBytes / 1024 / 1024) + " MB");
            }
            catch
            {
                // 拿不到就算
            }

            sb.AppendLine();
            sb.AppendLine("[游戏]");
            sb.AppendLine("安装目录：" + (string.IsNullOrWhiteSpace(settings.GameDirectory) ? "(未设置)" : settings.GameDirectory));
            try
            {
                var launcher = new GameLauncher(settings);
                sb.AppendLine("版本：" + (launcher.GameVersion.Length > 0 ? launcher.GameVersion : "(未知)"));
                sb.AppendLine("可执行文件：" + (launcher.IsInstalled ? launcher.ExecutablePath : "(未找到)"));
            }
            catch (Exception ex)
            {
                sb.AppendLine("读取游戏信息失败：" + ex.Message);
            }
            sb.AppendLine("用户数据目录：" + AppSettings.UserDataDirectory);
            sb.AppendLine("游戏正在运行：" + (GameRuntimeService.IsRunning() ? "是" : "否"));
            sb.AppendLine();

            sb.AppendLine("[游玩统计]");
            sb.AppendLine("累计时长：" + PlaytimeTracker.FormatDuration(PlaytimeTracker.GetTotalPlaytime(settings)));
            sb.AppendLine("启动次数：" + settings.LaunchCount);
            sb.AppendLine();

            sb.AppendLine("[联机]");
            sb.AppendLine("中继地址：" + settings.RelayHost + ":" + settings.RelayWebSocketPort + settings.RelayWebSocketPath);
            sb.AppendLine("优先 WebSocket：" + settings.PreferWebSocket);
            sb.AppendLine("玩家昵称：" + settings.PlayerName);
            return sb.ToString();
        }

        private static string StateLabel(GameFileHealthItem item)
        {
            switch (item.State)
            {
                case GameFileState.Ok:
                    return StartRide.App.Resources.Strings.Settings_GameConfigStatusOk;
                case GameFileState.Fixed:
                    return StartRide.App.Resources.Strings.Settings_GameHealthStatusFixed;
                case GameFileState.NeedsSteam:
                    return StartRide.App.Resources.Strings.Settings_GameHealthStatusNeedSteam;
                case GameFileState.Failed:
                    return StartRide.App.Resources.Strings.Settings_GameHealthStatusFailed;
                default:
                    return StartRide.App.Resources.Strings.Settings_GameHealthStatusNotApplicable;
            }
        }

        private static string BuildGameFilesText(AppSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("游戏文件体检结果");
            sb.AppendLine();
            try
            {
                foreach (var item in new GameFileHealthService(settings).Inspect())
                {
                    sb.AppendLine("- " + item.DisplayName);
                    sb.AppendLine("    路径：" + item.RelativePath);
                    sb.AppendLine("    状态：" + StateLabel(item));
                    if (!string.IsNullOrWhiteSpace(item.Detail))
                    {
                        sb.AppendLine("    说明：" + item.Detail);
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("体检失败：" + ex.Message);
            }
            return sb.ToString();
        }
    }
}
