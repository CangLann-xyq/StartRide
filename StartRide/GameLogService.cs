using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace StartRide.Core
{
    /// <summary>游戏日志里的一行（解析出等级用）。</summary>
    public sealed class GameLogLine
    {
        /// <summary>原始整行。</summary>
        public string Raw { get; init; } = "";

        /// <summary>D/I/W/E/A（解析不出来就是 '?'）。</summary>
        public char Level { get; init; } = '?';

        /// <summary>启动后经过的秒数（解析不出来就是 -1）。</summary>
        public double Seconds { get; init; } = -1;

        /// <summary>模块名，例如 fmod / GELua.core_audio.audio。</summary>
        public string Module { get; init; } = "";

        /// <summary>消息正文。</summary>
        public string Message { get; init; } = "";

        /// <summary>等级中文名。</summary>
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

    /// <summary>一份日志的统计结果。</summary>
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

    /// <summary>
    /// 读 BeamNG 自己的运行日志。
    ///
    /// 日志格式（文件第 3 行自己写着）：
    ///     Time since startup | Message level: D(ebug), I(nfo), W(arning), E(rror), A(lways) | Message
    /// 实例：  9.35997|D|fmod| music: Closing project
    /// 所以按 '|' 切四段就能精确判等级，不用猜关键字。
    /// </summary>
    public sealed class GameLogService
    {
        private readonly AppSettings _settings;

        public GameLogService(AppSettings settings) => _settings = settings;

        /// <summary>日志目录（&lt;userpath&gt;）。</summary>
        public string LogDirectory => _settings.ResolveUserDataRoot();

        public string PrimaryLogPath => Path.Combine(LogDirectory, "beamng.log");

        /// <summary>已经存在的日志文件（含轮转出来的 beamng.1.log 等），按修改时间倒序。</summary>
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

        /// <summary>最近在写的那个日志文件（优先 beamng.log）。</summary>
        public string ResolveActiveLogPath()
        {
            string primary = PrimaryLogPath;
            if (File.Exists(primary)) return primary;
            return FindLogFiles().FirstOrDefault() ?? primary;
        }

        /// <summary>把一整行解析成结构化对象；格式对不上也不丢内容。</summary>
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

        /// <summary>
        /// 读文件末尾 N 行（大日志也不整份载入）：从尾部按块回退，凑够行数就停。
        /// </summary>
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

                // 日志里混着非 UTF-8 字节，用 Latin1 逐字节映射，永不抛异常
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
                // 读不到就返回已读到的部分
            }
            return result;
        }

        /// <summary>统计一份日志：总量、各级别条数、错误/警告明细。</summary>
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
                // Latin1：逐字节映射，坏字节也不会抛异常
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

        /// <summary>
        /// 游戏日志的「最近一次写入时间」。用于启动器被强杀后估算会话长度
        /// （PlaytimeTracker 会调用，保持签名兼容）。
        /// </summary>
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
