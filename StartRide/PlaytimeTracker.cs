using System;
using System.Globalization;
using System.IO;
using System.Linq;
using StartRide.App.Resources;

namespace StartRide.Core
{
    /// <summary>
    /// 游玩时长统计。
    ///
    /// 会话模型：启动游戏 → BeginSession（记下开始时间、启动次数 +1）；
    /// 游戏退出 → EndSession（把这段时长累加进 TotalPlaytimeSeconds）。
    /// 启动器被直接关掉/断电导致会话没结算时，RecoverOrphanSession 会用
    /// 游戏日志的最后写入时间估算结束点（估算不出来就按"现在"收尾，上限 24 小时）。
    ///
    /// 所有方法都不抛异常，也不依赖 UI —— 统计失败绝不能影响启动游戏。
    /// </summary>
    public static class PlaytimeTracker
    {
        private const string TimeFormat = "yyyy-MM-ddTHH:mm:ss";

        /// <summary>单次会话最多统计多久（防止启动器长期不开导致虚高）。</summary>
        private static readonly TimeSpan MaxSessionLength = TimeSpan.FromHours(24);

        public static bool HasOpenSession(AppSettings settings)
        {
            return TryParse(settings.RunningSessionStartedAt, out _);
        }

        public static DateTimeOffset? TryGetSessionStart(AppSettings settings)
        {
            return TryParse(settings.RunningSessionStartedAt, out var start) ? start : null;
        }

        /// <summary>游戏进程刚起来：开一个会话。</summary>
        public static void BeginSession(AppSettings settings, DateTimeOffset? startedAt = null)
        {
            try
            {
                var now = startedAt ?? DateTimeOffset.Now;
                settings.RunningSessionStartedAt = now.ToString(TimeFormat, CultureInfo.InvariantCulture);
                settings.LastLaunchAt = settings.RunningSessionStartedAt;
                settings.LaunchCount += 1;
                settings.Save();
            }
            catch
            {
                // 统计失败不影响启动
            }
        }

        /// <summary>游戏退出：结算本次会话。没有进行中的会话时什么都不做。</summary>
        public static void EndSession(AppSettings settings, DateTimeOffset? endedAt = null)
        {
            try
            {
                if (!TryParse(settings.RunningSessionStartedAt, out var start))
                {
                    return;
                }

                var end = endedAt ?? DateTimeOffset.Now;
                TimeSpan span = end - start;
                if (span < TimeSpan.Zero)
                {
                    span = TimeSpan.Zero;
                }
                if (span > MaxSessionLength)
                {
                    span = MaxSessionLength;
                }

                settings.TotalPlaytimeSeconds += (long)span.TotalSeconds;
                settings.LastSessionSeconds = (int)span.TotalSeconds;
                settings.LastSessionEndedAt = end.ToString(TimeFormat, CultureInfo.InvariantCulture);
                settings.RunningSessionStartedAt = "";
                settings.Save();
            }
            catch
            {
                // 统计失败不影响退出流程
            }
        }

        /// <summary>
        /// 上次会话没结算（启动器被关掉/断电）：用游戏日志最后的写入时间估算结束点。
        /// 启动时调用一次即可；游戏现在正在跑的话不动它。
        /// </summary>
        public static void RecoverOrphanSession(AppSettings settings)
        {
            try
            {
                if (!TryParse(settings.RunningSessionStartedAt, out var start))
                {
                    return;
                }
                if (GameRuntimeService.IsRunning())
                {
                    return;
                }
                EndSession(settings, EstimateSessionEnd(start) ?? DateTimeOffset.Now);
            }
            catch
            {
                // 忽略
            }
        }

        /// <summary>当前会话已进行的时长（没有会话返回 null）。</summary>
        public static TimeSpan? GetCurrentSessionLength(AppSettings settings)
        {
            if (!TryParse(settings.RunningSessionStartedAt, out var start))
            {
                return null;
            }
            var span = DateTimeOffset.Now - start;
            return span < TimeSpan.Zero ? TimeSpan.Zero : span;
        }

        /// <summary>累计游玩时长（含正在进行但还没结算的这一段）。</summary>
        public static TimeSpan GetTotalPlaytime(AppSettings settings)
        {
            var total = TimeSpan.FromSeconds(Math.Max(0, settings.TotalPlaytimeSeconds));
            var current = GetCurrentSessionLength(settings);
            if (current.HasValue)
            {
                total += current.Value;
            }
            return total;
        }

        /// <summary>把秒数说成人话："3 小时 12 分钟" / "45 分钟" / "18 秒"。</summary>
        public static string FormatDuration(TimeSpan span)
        {
            if (span < TimeSpan.Zero)
            {
                span = TimeSpan.Zero;
            }
            if (span.TotalHours >= 1)
            {
                int hours = (int)span.TotalHours;
                int minutes = span.Minutes;
                return string.Format(Strings.Time_HoursMinutesFormat, hours, minutes);
            }
            if (span.TotalMinutes >= 1)
            {
                return string.Format(Strings.Time_MinutesFormat, (int)span.TotalMinutes);
            }
            return string.Format(Strings.Time_SecondsFormat, (int)span.TotalSeconds);
        }

        /// <summary>最近一次启动时间文本；没有记录返回空串。</summary>
        public static string FormatLastLaunchAt(AppSettings settings)
        {
            if (!TryParse(settings.LastLaunchAt, out var value))
            {
                return "";
            }
            return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        }

        private static bool TryParse(string? text, out DateTimeOffset value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out value))
            {
                return true;
            }
            return DateTimeOffset.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out value);
        }

        /// <summary>
        /// 估算上次会话的结束时间：取候选 userpath 里游戏日志的最新写入时间。
        /// 估算不出来返回 null（调用方按"现在"处理）。
        /// </summary>
        private static DateTimeOffset? EstimateSessionEnd(DateTimeOffset start)
        {
            DateTimeOffset? newest = null;
            try
            {
                foreach (string root in AppSettings.ResolveUserPathRoots())
                {
                    foreach (string file in CandidateLogFiles(root))
                    {
                        try
                        {
                            if (!File.Exists(file))
                            {
                                continue;
                            }
                            var written = new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero);
                            if (written <= start)
                            {
                                continue;
                            }
                            if (newest == null || written > newest.Value)
                            {
                                newest = written;
                            }
                        }
                        catch
                        {
                            // 单个文件读不到就跳过
                        }
                    }
                }
            }
            catch
            {
                return null;
            }

            if (newest == null)
            {
                return null;
            }
            var now = DateTimeOffset.Now;
            if (newest.Value > now)
            {
                return now;
            }
            return newest.Value.ToLocalTime();
        }

        private static string[] CandidateLogFiles(string root)
        {
            return new[]
            {
                Path.Combine(root, "beamng-launcher.log"),
                Path.Combine(root, "current", "beamng-launcher.log"),
                Path.Combine(root, "logs", "beamng.log"),
            };
        }
    }
}
