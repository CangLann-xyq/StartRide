using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StartRide.Core
{
    public sealed class HighlightRoom
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("players")] public int Players { get; set; }
    }

    public sealed class HighlightReplaySegment
    {
        [JsonPropertyName("file")] public string? File { get; set; }
        [JsonPropertyName("from")] public double From { get; set; }
        [JsonPropertyName("to")] public double To { get; set; }
    }

    public sealed class HighlightItem
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("label")] public string Label { get; set; } = "";
        [JsonPropertyName("value")] public double Value { get; set; }
        [JsonPropertyName("extra")] public double Extra { get; set; }
        [JsonPropertyName("speed")] public double Speed { get; set; }

        [JsonPropertyName("offset")] public double Offset { get; set; }

        [JsonPropertyName("replay")] public string? Replay { get; set; }
        [JsonPropertyName("at")] public string? At { get; set; }
        [JsonPropertyName("shot")] public string? Shot { get; set; }

        [JsonIgnore] public string ShotsDirectory { get; set; } = "";

        [JsonIgnore] public string Title => string.IsNullOrWhiteSpace(Label) ? Type : Label;

        [JsonIgnore] public string? ShotPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Shot) || string.IsNullOrWhiteSpace(ShotsDirectory)) return null;
                return Path.Combine(ShotsDirectory, Shot);
            }
        }

        [JsonIgnore]
        public string ValueText
        {
            get
            {
                switch (Type)
                {
                    case "jump":
                        if (Extra >= 1)
                        {
                            return Num(Value, "0.0") + " 秒 · " + Num(Extra, "0") + " 米";
                        }
                        return Num(Value, "0.0") + " 秒";
                    case "impact":
                        return Num(Value, "0") + " 能量";
                    case "rollover":
                        return "翻滚 " + Num(Value, "0.0") + " 秒";
                    case "burnout":
                        return Num(Value, "0.0") + " 秒";
                    case "topspeed":
                        return Num(Value * 3.6, "0") + " km/h";
                    default:
                        return Num(Value, "0.##");
                }
            }
        }

        private static string Num(double v, string fmt)
        {
            return v.ToString(fmt, CultureInfo.InvariantCulture);
        }

        [JsonIgnore]
        public string TimeCodeText
        {
            get
            {
                int total = (int)Math.Round(Offset, MidpointRounding.AwayFromZero);
                if (total <= 0) return "0 秒";
                if (total < 60) return total + " 秒";
                int m = total / 60, s = total % 60;
                return s == 0 ? m + " 分" : m + " 分 " + s + " 秒";
            }
        }

        [JsonIgnore]
        public string ReplayFileName => string.IsNullOrWhiteSpace(Replay) ? "" : Path.GetFileName(Replay!);

        private bool shotTried;
        private ImageSource? shotImage;

        [JsonIgnore]
        public ImageSource? ShotImage
        {
            get
            {
                if (shotTried) return shotImage;
                shotTried = true;
                string? p = ShotPath;
                if (string.IsNullOrEmpty(p) || !File.Exists(p)) return null;
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(p!, UriKind.Absolute);
                    bmp.DecodePixelWidth = 320;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    shotImage = bmp;
                }
                catch
                {
                    shotImage = null;
                }
                return shotImage;
            }
        }

        [JsonIgnore] public bool HasShot => ShotImage != null;
    }

    public sealed class HighlightSession
    {
        [JsonPropertyName("version")] public int Version { get; set; }
        [JsonPropertyName("sessionId")] public string SessionId { get; set; } = "";
        [JsonPropertyName("reason")] public string? Reason { get; set; }
        [JsonPropertyName("player")] public string? Player { get; set; }
        [JsonPropertyName("map")] public string? Map { get; set; }
        [JsonPropertyName("room")] public HighlightRoom? Room { get; set; }
        [JsonPropertyName("startedAt")] public string? StartedAt { get; set; }
        [JsonPropertyName("endedAt")] public string? EndedAt { get; set; }
        [JsonPropertyName("durationSeconds")] public double DurationSeconds { get; set; }
        [JsonPropertyName("replays")] public List<HighlightReplaySegment>? Replays { get; set; }
        [JsonPropertyName("highlights")] public List<HighlightItem>? Highlights { get; set; }

        [JsonIgnore] public string JsonPath { get; set; } = "";
        [JsonIgnore] public string ShotsDirectory { get; set; } = "";
        [JsonIgnore] public string ReplaysDirectory { get; set; } = "";

        [JsonIgnore] public IReadOnlyList<HighlightItem> Items => Highlights ?? (IReadOnlyList<HighlightItem>)Array.Empty<HighlightItem>();

        [JsonIgnore] public int Count => Items.Count;

        [JsonIgnore] public bool HasHighlights => Count > 0;

        [JsonIgnore] public string MapText => string.IsNullOrWhiteSpace(Map) ? "未知地图" : Map!;

        [JsonIgnore]
        public DateTime? StartedLocal
        {
            get
            {
                if (string.IsNullOrWhiteSpace(StartedAt)) return null;
                if (DateTime.TryParseExact(StartedAt, "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    return dt;
                }
                if (DateTime.TryParse(StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                {
                    return dt;
                }
                return null;
            }
        }

        [JsonIgnore]
        public string AtText => StartedLocal.HasValue
            ? StartedLocal.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : (StartedAt ?? "");

        [JsonIgnore] public string AgoText => DescribeAgo(StartedLocal);

        [JsonIgnore]
        public string DurationText
        {
            get
            {
                int total = (int)Math.Round(DurationSeconds, MidpointRounding.AwayFromZero);
                if (total < 60) return total + " 秒";
                int m = total / 60, s = total % 60;
                return s == 0 ? m + " 分钟" : m + " 分 " + s + " 秒";
            }
        }

        [JsonIgnore]
        public string SummaryText
        {
            get
            {
                if (!HasHighlights) return "没有捕捉到高光";
                var parts = Items
                    .GroupBy(i => i.Title)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key + " ×" + g.Count());
                return string.Join(" · ", parts);
            }
        }

        [JsonIgnore] public bool HasReplay => Replays != null && Replays.Any(r => !string.IsNullOrWhiteSpace(r.File));

        [JsonIgnore] public string SessionDirectory => ShotsDirectory;

        private static string DescribeAgo(DateTime? at)
        {
            if (!at.HasValue) return "";
            TimeSpan d = DateTime.Now - at.Value;
            if (d.TotalSeconds < 0) return "刚刚";
            if (d.TotalMinutes < 1) return "刚刚";
            if (d.TotalHours < 1) return (int)d.TotalMinutes + " 分钟前";
            if (d.TotalDays < 1) return (int)d.TotalHours + " 小时前";
            if (d.TotalDays < 30) return (int)d.TotalDays + " 天前";
            if (d.TotalDays < 365) return (int)(d.TotalDays / 30) + " 个月前";
            return (int)(d.TotalDays / 365) + " 年前";
        }
    }

    public static class HighlightStore
    {
        public const string SubDirectoryName = "startride";

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        public static string ResolveDirectory(string? replaysDirectory)
        {
            if (string.IsNullOrWhiteSpace(replaysDirectory)) return "";
            return Path.Combine(replaysDirectory!, SubDirectoryName);
        }

        public static string ResolveConfigPath(string? replaysDirectory)
        {
            string dir = ResolveDirectory(replaysDirectory);
            return dir.Length == 0 ? "" : Path.Combine(dir, "config.json");
        }

        public static List<HighlightSession> LoadAll(string? replaysDirectory)
        {
            var list = new List<HighlightSession>();
            string dir = ResolveDirectory(replaysDirectory);
            if (dir.Length == 0 || !Directory.Exists(dir)) return list;

            IEnumerable<string> files;
            try
            {
                // 同时收 .json 与 .json.tmp：游戏侧先写 .tmp 再改名，
                // 改名那一瞬 / 改名失败时 .tmp 是这份会话唯一的完整快照。
                var found = new List<string>();
                found.AddRange(Directory.GetFiles(dir, "session-*.json"));
                found.AddRange(Directory.GetFiles(dir, "session-*.json.tmp"));
                files = found;
            }
            catch
            {
                return list;
            }

            // 同一个 sessionId 可能同时存在两份（.json 与 .tmp）。
            // 取高光条数更多的那份，绝不因为一次半截写入就让整场消失。
            var best = new Dictionary<string, HighlightSession>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                HighlightSession? s = LoadOne(file, dir, replaysDirectory);
                if (s == null) continue;
                if (!best.TryGetValue(s.SessionId, out var prev) || s.Count > prev.Count)
                {
                    best[s.SessionId] = s;
                }
            }
            list.AddRange(best.Values);

            return list
                .OrderByDescending(s => s.StartedLocal ?? DateTime.MinValue)
                .ThenByDescending(s => s.SessionId, StringComparer.Ordinal)
                .ToList();
        }

        public static HighlightSession? LoadOne(string jsonPath, string sessionDirectory, string replaysDirectory)
        {
            string text = ReadTolerant(jsonPath);
            HighlightSession? s = null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    s = JsonSerializer.Deserialize<HighlightSession>(text, ReadOptions);
                }
                catch
                {
                    s = null;      // 半截 JSON：下面用占位会话兜住
                }
            }

            // 🔴 以前这里 catch 之后直接 return null —— 游戏正在写文件的那几十毫秒里，
            //    整场会话就从回放页消失，刷新一下又回来，表现就是「高光时有时无」。
            //    现在返回一个带文件名的占位会话：列表里至少有这一场，
            //    下一次刷新（文件写完）自然会被内容更全的那份替换掉。
            if (s == null)
            {
                s = new HighlightSession { SessionId = SessionIdFromPath(jsonPath) };
                try
                {
                    s.StartedAt = File.GetLastWriteTime(jsonPath)
                        .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                }
                catch
                {
                    // 时间拿不到无所谓，AtText 会回退到空串
                }
            }

            s.JsonPath = jsonPath;
            s.ShotsDirectory = sessionDirectory;
            s.ReplaysDirectory = replaysDirectory ?? "";
            if (string.IsNullOrWhiteSpace(s.SessionId))
            {
                s.SessionId = SessionIdFromPath(jsonPath);
            }
            foreach (HighlightItem it in s.Items)
            {
                it.ShotsDirectory = sessionDirectory;
            }
            return s;
        }

        /// <summary>
        /// 以「允许别人同时读写」的方式读文本。File.ReadAllText 默认只放行
        /// FileShare.Read，游戏侧正握着写句柄时会直接抛 IOException。
        /// </summary>
        private static string ReadTolerant(string path)
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var sr = new StreamReader(fs, Encoding.UTF8, true);
                    return sr.ReadToEnd();
                }
                catch (IOException)
                {
                    if (attempt == 2) return "";
                    System.Threading.Thread.Sleep(20);
                }
                catch (UnauthorizedAccessException)
                {
                    if (attempt == 2) return "";
                    System.Threading.Thread.Sleep(20);
                }
                catch
                {
                    return "";
                }
            }
            return "";
        }

        private static string SessionIdFromPath(string path)
        {
            var name = Path.GetFileName(path);
            if (name.EndsWith(".json.tmp", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - ".json.tmp".Length);
            }
            else
            {
                name = Path.GetFileNameWithoutExtension(name);
            }
            return name.StartsWith("session-", StringComparison.OrdinalIgnoreCase)
                ? name.Substring("session-".Length)
                : name;
        }

        public static List<string> CollectTypes(IEnumerable<HighlightSession> sessions)
        {
            return sessions
                .SelectMany(s => s.Items)
                .Select(i => i.Type)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        public static string? PushModConfig(AppSettings? settings)
        {
            if (settings == null) return "没有设置对象";
            int minutes = Math.Clamp(settings.HighlightSegmentMinutes, 2, 120);
            return WriteModConfig(
                settings.ResolveReplaysDirectory(),
                settings.HighlightCaptureEnabled,
                settings.HighlightAutoRecord,
                minutes * 60,
                40,
                !settings.HighlightInSinglePlayer);
        }

        public static string? WriteModConfig(
            string? replaysDirectory,
            bool enabled,
            bool autoRecord,
            int segmentSeconds,
            int maxShots,
            bool onlyInSession)
        {
            string dir = ResolveDirectory(replaysDirectory);
            if (dir.Length == 0) return "没有解析到回放目录";
            try
            {
                Directory.CreateDirectory(dir);
                var payload = new Dictionary<string, object?>
                {
                    ["enabled"] = enabled,
                    ["autoRecord"] = autoRecord,
                    ["segmentSeconds"] = segmentSeconds,
                    ["maxShots"] = maxShots,
                    ["onlyInSession"] = onlyInSession,
                    ["writtenBy"] = "StartRide launcher",
                };
                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = true,
                });
                // 原子替换：先写 .tmp 再 Move(overwrite)。直接原地 WriteAllText
                // 会让游戏侧读到半截 JSON，配置静默回退成默认值
                //（onlyInSession=true → 单人模式高光时有时无）。
                string target = Path.Combine(dir, "config.json");
                string staging = target + ".tmp";
                File.WriteAllText(staging, json, new UTF8Encoding(false));
                File.Move(staging, target, true);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
