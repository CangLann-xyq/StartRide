using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace StartRide.Core
{
    public sealed class GameLogLine
    {
        public string Raw { get; init; } = "";

        public char Level { get; init; } = '?';

        public double Seconds { get; init; } = -1;

        public string Module { get; init; } = "";

        public string Message { get; init; } = "";

        public string LevelText => Level switch
        {
            'E' => "错误",
            'W' => "警告",
            'A' => "提示",
            'I' => "信息",
            'D' => "调试",
            _ => "其他",
        };

        public bool IsProblem => Level == 'E';
        public bool IsWarning => Level == 'W';
    }

    public sealed class GameLogSummary
    {
        public string Path { get; init; } = "";
        public bool Exists { get; init; }
        public long SizeBytes { get; init; }
        public DateTime? ModifiedAt { get; init; }
        public int TotalLines { get; init; }
        public int ErrorCount { get; init; }
        public int WarningCount { get; init; }
        public string GameVersion { get; init; } = "";
        public string Error { get; init; } = "";

        public bool HasProblems => ErrorCount > 0;
    }

    public sealed class GameLogService
    {
        private readonly AppSettings _settings;

        public GameLogService(AppSettings settings) => _settings = settings;

        public string LogDirectory => _settings.ResolveUserDataRoot();

        public string PrimaryLogPath => Path.Combine(LogDirectory, "beamng.log");

        public List<string> FindLogFiles()
        {
            var list = new List<string>();
            try
            {
                string dir = LogDirectory;
                if (!Directory.Exists(dir)) return list;

                foreach (var f in Directory.GetFiles(dir, "beamng*.log"))
                {
                    list.Add(f);
                }
                string console = Path.Combine(dir, "console.log");
                if (File.Exists(console)) list.Add(console);

                return list
                    .OrderByDescending(f =>
                    {
                        try { return File.GetLastWriteTime(f); }
                        catch { return DateTime.MinValue; }
                    })
                    .ToList();
            }
            catch
            {
                return list;
            }
        }

        public string ResolveActiveLogPath()
        {
            string primary = PrimaryLogPath;
            if (File.Exists(primary)) return primary;
            return FindLogFiles().FirstOrDefault() ?? primary;
        }

        public static GameLogLine ParseLine(string raw)
        {
            var line = raw ?? "";
            var parts = line.Split('|', 4);
            if (parts.Length < 3)
            {
                return new GameLogLine { Raw = line, Message = line.Trim() };
            }

            char level = parts[1].Length > 0 ? char.ToUpperInvariant(parts[1][0]) : '?';
            double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double sec);

            return new GameLogLine
            {
                Raw = line,
                Level = level,
                Seconds = parts[0].Trim().Length > 0 ? sec : -1,
                Module = parts[2].Trim(),
                Message = parts.Length > 3 ? parts[3].Trim() : "",
            };
        }

        public static List<string> ReadTail(string path, int maxLines)
        {
            var result = new List<string>();
            if (maxLines <= 0) return result;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long pos = fs.Length;
                var buffer = new byte[64 * 1024];
                var chunks = new List<byte[]>();
                int newlines = 0;

                while (pos > 0 && newlines <= maxLines)
                {
                    int size = (int)Math.Min(buffer.Length, pos);
                    pos -= size;
                    fs.Seek(pos, SeekOrigin.Begin);
                    int read = fs.Read(buffer, 0, size);
                    if (read <= 0) break;

                    var chunk = new byte[read];
                    Array.Copy(buffer, chunk, read);
                    chunks.Insert(0, chunk);

                    for (int i = 0; i < read; i++)
                    {
                        if (buffer[i] == (byte)'\n') newlines++;
                    }
                }

                var all = new byte[chunks.Sum(c => c.Length)];
                int offset = 0;
                foreach (var c in chunks)
                {
                    Array.Copy(c, 0, all, offset, c.Length);
                    offset += c.Length;
                }

                string text = Encoding.Latin1.GetString(all);
                var lines = text.Split('\n');
                for (int i = lines.Length - 1; i >= 0 && result.Count < maxLines; i--)
                {
                    string s = lines[i].TrimEnd('\r');
                    if (s.Length == 0 && i == lines.Length - 1) continue;
                    result.Insert(0, s);
                }
            }
            catch
            {
            }
            return result;
        }

        public GameLogSummary Analyze(string path, out List<GameLogLine> problems, int problemSampleLimit = 200)
        {
            problems = new List<GameLogLine>();

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new GameLogSummary { Path = path ?? "", Exists = false };
            }

            int total = 0, errors = 0, warnings = 0;
            string version = "";
            var problemLines = new List<GameLogLine>();

            try
            {
                var info = new FileInfo(path);
                using var reader = new StreamReader(path, Encoding.Latin1, detectEncodingFromByteOrderMarks: false);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    total++;
                    var parsed = ParseLine(line);

                    if (version.Length == 0 && parsed.Message.Contains("Log started - v "))
                    {
                        version = ExtractVersion(parsed.Message);
                    }
                    else if (version.Length == 0 &&
                             parsed.Module.Equals("bng::Version::saveStoredVersion", StringComparison.OrdinalIgnoreCase))
                    {
                        version = ExtractVersion(parsed.Message);
                    }

                    if (parsed.Level == 'E')
                    {
                        errors++;
                        if (problemLines.Count < problemSampleLimit) problemLines.Add(parsed);
                    }
                    else if (parsed.Level == 'W')
                    {
                        warnings++;
                        if (problemLines.Count < problemSampleLimit) problemLines.Add(parsed);
                    }
                }

                problems = problemLines;
                return new GameLogSummary
                {
                    Path = path,
                    Exists = true,
                    SizeBytes = info.Length,
                    ModifiedAt = info.LastWriteTime,
                    TotalLines = total,
                    ErrorCount = errors,
                    WarningCount = warnings,
                    GameVersion = version,
                };
            }
            catch (Exception ex)
            {
                problems = problemLines;
                return new GameLogSummary { Path = path, Exists = true, Error = ex.Message };
            }
        }

        private static string ExtractVersion(string message)
        {
            var m = System.Text.RegularExpressions.Regex.Match(message, @"v\s*(\d+\.\d+\.\d+\.\d+)");
            return m.Success ? m.Groups[1].Value : "";
        }

        public DateTime? GetLastWriteTime()
        {
            foreach (var f in new[] { PrimaryLogPath }.Concat(FindLogFiles()))
            {
                try
                {
                    if (File.Exists(f)) return File.GetLastWriteTime(f);
                }
                catch { }
            }
            return null;
        }
    }
}
