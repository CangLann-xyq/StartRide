using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace StartRide.Services
{
    public enum StartRideUpdateOutcome
    {
        None,

        Succeeded,

        Failed,
    }

    public readonly record struct StartRideUpdateStatus(
        StartRideUpdateOutcome Outcome,
        string TargetVersion,
        string CurrentVersion,
        string Reason)
    {
        public bool HasResult => Outcome != StartRideUpdateOutcome.None;

        public bool IsFailure => Outcome == StartRideUpdateOutcome.Failed;
    }

    public static class StartRideUpdateJournal
    {
        private static readonly object Gate = new();

        private static bool consumed;

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StartRide",
            "update-pending.json");

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
            }
        }

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
            }
        }

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
