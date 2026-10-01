using System;
using System.Globalization;
using System.IO;
using System.Linq;
using StartRide.App.Resources;

namespace StartRide.Core
{

    public static class PlaytimeTracker
    {
        private const string TimeFormat = "yyyy-MM-ddTHH:mm:ss";

        private static readonly TimeSpan MaxSessionLength = TimeSpan.FromHours(24);

        public static bool HasOpenSession(AppSettings settings)
        {
            return TryParse(settings.RunningSessionStartedAt, out _);
        }

        public static DateTimeOffset? TryGetSessionStart(AppSettings settings)
        {
            return TryParse(settings.RunningSessionStartedAt, out var start) ? start : null;
        }

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
            }
        }

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
            }
        }

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
            }
        }

        public static TimeSpan? GetCurrentSessionLength(AppSettings settings)
        {
            if (!TryParse(settings.RunningSessionStartedAt, out var start))
            {
                return null;
            }
            var span = DateTimeOffset.Now - start;
            return span < TimeSpan.Zero ? TimeSpan.Zero : span;
        }

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
