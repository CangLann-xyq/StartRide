using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace StartRide.Services
{
    /// <summary>上一次自更新的结算结果。</summary>
    public enum StartRideUpdateOutcome
    {
        /// <summary>没有待结算的更新（正常启动）。</summary>
        None,

        /// <summary>更新装好了。</summary>
        Succeeded,

        /// <summary>更新没装成功，当前还是旧版本。</summary>
        Failed,
    }

    /// <summary>一次自更新的结算结果（失败时带原因摘要）。</summary>
    public readonly record struct StartRideUpdateStatus(
        StartRideUpdateOutcome Outcome,
        string TargetVersion,
        string CurrentVersion,
        string Reason)
    {
        /// <summary>是否有结果要告诉用户。</summary>
        public bool HasResult => Outcome != StartRideUpdateOutcome.None;

        /// <summary>是不是失败。</summary>
        public bool IsFailure => Outcome == StartRideUpdateOutcome.Failed;
    }

    /// <summary>
    /// 自更新的「交接条」。
    ///
    /// 为什么需要它
    /// ------------
    /// 自更新的最后一步是：本进程退出 → apply.ps1 覆盖安装目录 → 重新拉起启动器。
    /// 这一跳跨越了进程生死，于是**新进程根本无从知道刚才那次更新到底成没成**：
    /// 包坏了、文件被占用没替换掉、脚本压根没跑起来…… 在界面上表现完全一样 ——
    /// 还是老样子。实测有用户反馈「点了更新，闪一下就没了，重新打开还是旧版本」，
    /// 而日志显示那次其实**装成功了**，只是没有任何地方把结果告诉他。
    ///
    /// 所以：替换前先写下目标版本，下次启动时拿实际版本对照，一次性结算并提示；
    /// 失败时把 apply.ps1 的日志摘要带出来，一眼能看出卡在哪。
    ///
    /// 为什么放 %APPDATA%（Roaming）而不是 update 缓存目录
    /// --------------------------------------------------
    /// 框架启动时会跑 LauncherUpdateCacheCleaner 清理更新缓存。
    /// 交接条绝不能被它顺手删掉，因此放在与日志/配置同一层（%APPDATA%\StartRide）。
    /// </summary>
    public static class StartRideUpdateJournal
    {
        private static readonly object Gate = new();

        private static bool consumed;

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StartRide",
            "update-pending.json");

        /// <summary>替换前登记：从哪个版本升到哪个版本、apply.ps1 的日志在哪。</summary>
        public static void Begin(string fromVersion, string targetVersion, string applyLogPath)
        {
            try
            {
                string? directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var payload = new PendingUpdate
                {
                    FromVersion = fromVersion ?? string.Empty,
                    TargetVersion = targetVersion ?? string.Empty,
                    ApplyLog = applyLogPath ?? string.Empty,
                    StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                };
                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch
            {
                // 登记失败只影响「下次启动的提示」，绝不能影响更新本身
            }
        }

        /// <summary>
        /// 结算一次（每个进程只结算一次）：实际版本 ≥ 目标版本 = 成功。
        /// 结算完就删掉交接条 —— 结果只提示一次，不反复打扰。
        /// </summary>
        public static StartRideUpdateStatus Consume(string currentVersion)
        {
            lock (Gate)
            {
                if (consumed)
                {
                    return default;
                }

                consumed = true;
            }

            PendingUpdate? pending = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    pending = JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(FilePath));
                }
            }
            catch
            {
                pending = null;
            }

            TryDelete();

            if (pending == null || string.IsNullOrWhiteSpace(pending.TargetVersion))
            {
                return default;
            }

            string target = pending.TargetVersion!;
            bool applied = IsAtLeast(currentVersion, target);
            return new StartRideUpdateStatus(
                applied ? StartRideUpdateOutcome.Succeeded : StartRideUpdateOutcome.Failed,
                target,
                currentVersion ?? string.Empty,
                applied ? string.Empty : DescribeFailure(pending));
        }

        private static void TryDelete()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch
            {
                // 删不掉最多下次再提示一遍，不影响功能
            }
        }

        /// <summary>current &gt;= target（按版本号比较，非法时退化成字符串比较）。</summary>
        private static bool IsAtLeast(string? current, string target)
        {
            if (string.IsNullOrWhiteSpace(current))
            {
                return false;
            }

            string left = Normalize(current);
            string right = Normalize(target);
            if (Version.TryParse(left, out Version? a) && Version.TryParse(right, out Version? b))
            {
                return a >= b;
            }

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string value)
        {
            string text = value.Trim().TrimStart('v', 'V');
            int dash = text.IndexOf('-');
            if (dash > 0)
            {
                text = text.Substring(0, dash);
            }

            return text;
        }

        /// <summary>从 apply.ps1 的日志里提炼一句「卡在哪」；读不到就返回空串（界面用兜底文案）。</summary>
        private static string DescribeFailure(PendingUpdate pending)
        {
            try
            {
                string? log = pending.ApplyLog;
                if (string.IsNullOrWhiteSpace(log) || !File.Exists(log))
                {
                    return string.Empty;
                }

                int copied = -1;
                int failed = -1;
                string last = string.Empty;
                foreach (string line in File.ReadAllLines(log!))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    last = line.Trim();
                    int index = line.IndexOf("copied=", StringComparison.OrdinalIgnoreCase);
                    if (index < 0)
                    {
                        continue;
                    }

                    foreach (string part in line.Substring(index).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (part.StartsWith("copied=", StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(part.Substring("copied=".Length), out int parsedCopied))
                        {
                            copied = parsedCopied;
                        }
                        else if (part.StartsWith("failed=", StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(part.Substring("failed=".Length), out int parsedFailed))
                        {
                            failed = parsedFailed;
                        }
                    }
                }

                if (copied >= 0 || failed >= 0)
                {
                    return string.Format(CultureInfo.InvariantCulture, "copied={0} failed={1}", copied, failed);
                }

                return last;
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class PendingUpdate
        {
            public string? FromVersion { get; set; }

            public string? TargetVersion { get; set; }

            public string? ApplyLog { get; set; }

            public string? StartedAt { get; set; }
        }
    }
}
