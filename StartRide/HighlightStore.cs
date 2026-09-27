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
    /// <summary>高光会话里的一场房间信息（模组写下来的）。</summary>
    public sealed class HighlightRoom
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("players")] public int Players { get; set; }
    }

    /// <summary>这条高光落在哪个录像文件的第几秒到第几秒。</summary>
    public sealed class HighlightReplaySegment
    {
        [JsonPropertyName("file")] public string? File { get; set; }
        [JsonPropertyName("from")] public double From { get; set; }
        [JsonPropertyName("to")] public double To { get; set; }
    }

    /// <summary>
    /// 一条高光。字段名与 Mods/startride_mod.lua 里 hlSave/onHighlight 写出的 JSON **一一对应**，
    /// 改一边必须改另一边。
    /// </summary>
    public sealed class HighlightItem
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("label")] public string Label { get; set; } = "";
        [JsonPropertyName("value")] public double Value { get; set; }
        [JsonPropertyName("extra")] public double Extra { get; set; }
        [JsonPropertyName("speed")] public double Speed { get; set; }

        /// <summary>相对本段录像起点的时间码（秒）——"跳到那一刻"就靠它。</summary>
        [JsonPropertyName("offset")] public double Offset { get; set; }

        [JsonPropertyName("replay")] public string? Replay { get; set; }
        [JsonPropertyName("at")] public string? At { get; set; }
        [JsonPropertyName("shot")] public string? Shot { get; set; }

        /// <summary>截图所在目录，反序列化后由 Store 补上（JSON 里只存文件名）。</summary>
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

        /// <summary>给人看的数值。**必须与 startride_mod.lua 的 hlValueText 保持一致**（跨语言没法共享）。</summary>
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

        /// <summary>"1 分 23 秒" 这种时间码。</summary>
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

        /// <summary>文件名（含扩展名）与"从文件名猜地图"用的原始片段，只读展示用。</summary>
        [JsonIgnore]
        public string ReplayFileName => string.IsNullOrWhiteSpace(Replay) ? "" : Path.GetFileName(Replay!);

        private bool shotTried;
        private ImageSource? shotImage;

        /// <summary>
        /// 截图缩略图。懒加载 + 只解码到 320px 宽：高光截图是全屏 JPEG（1920×1080），
        /// 一屏几十张全尺寸解码要吃掉几百 MB。
        /// </summary>
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
                    bmp.Freeze();          // 冻结后才能安全跨线程复用
                    shotImage = bmp;
                }
                catch
                {
                    shotImage = null;      // 图片坏了不能影响整张卡片
                }
                return shotImage;
            }
        }

        [JsonIgnore] public bool HasShot => ShotImage != null;
    }

    /// <summary>一次联机会话（模组在断开/退出时写一个 session-*.json）。</summary>
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

        /// <summary>会话开始时间。模组写的是 "yyyy-MM-dd HH:mm:ss"（本地时间，无时区）。</summary>
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

        /// <summary>"3 个大跳跃 · 1 次重击" —— 卡片副标题用，一眼看出这局有什么。</summary>
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

        /// <summary>这一局有没有留下录像文件（没有的话"跳到那一刻"就不可能）。</summary>
        [JsonIgnore] public bool HasReplay => Replays != null && Replays.Any(r => !string.IsNullOrWhiteSpace(r.File));

        /// <summary>会话目录（= 启动器用来定位截图和 JSON 的地方）。</summary>
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

    /// <summary>
    /// 读模组写在 &lt;userpath&gt;/replays/startride/ 下的高光会话。
    ///
    /// 目录选在 replays 下面是有意的：启动器已经知道怎么解析回放目录
    /// （AppSettings.ResolveReplaysDirectory），两边不需要再约定第二套路径规则。
    ///
    /// 容错要求很高 —— 这些 JSON 是游戏进程随时可能被强杀时写下的，
    /// 半截文件/字段缺失/编码异常都必须只是"这条不显示"，不能让回放页整体挂掉。
    /// </summary>
    public static class HighlightStore
    {
        public const string SubDirectoryName = "startride";

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        /// <summary>高光目录（&lt;replays&gt;/startride）。可能不存在，调用方判空。</summary>
        public static string ResolveDirectory(string? replaysDirectory)
        {
            if (string.IsNullOrWhiteSpace(replaysDirectory)) return "";
            return Path.Combine(replaysDirectory!, SubDirectoryName);
        }

        /// <summary>目录里有没有配置（用来说明"模组有没有跑过"）。</summary>
        public static string ResolveConfigPath(string? replaysDirectory)
        {
            string dir = ResolveDirectory(replaysDirectory);
            return dir.Length == 0 ? "" : Path.Combine(dir, "config.json");
        }

        /// <summary>
        /// 扫出全部会话，按开始时间**倒序**（最近的在前）。
        /// 读不动的文件直接跳过 —— 详见类注释的容错要求。
        /// </summary>
        public static List<HighlightSession> LoadAll(string? replaysDirectory)
        {
            var list = new List<HighlightSession>();
            string dir = ResolveDirectory(replaysDirectory);
            if (dir.Length == 0 || !Directory.Exists(dir)) return list;

            IEnumerable<string> files;
            try
            {
                files = Directory.GetFiles(dir, "session-*.json");
            }
            catch
            {
                return list;
            }

            foreach (string file in files)
            {
                HighlightSession? s = LoadOne(file, dir, replaysDirectory);
                if (s != null) list.Add(s);
            }

            return list
                .OrderByDescending(s => s.StartedLocal ?? DateTime.MinValue)
                .ThenByDescending(s => s.SessionId, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>读单个会话文件。坏文件返回 null。</summary>
        public static HighlightSession? LoadOne(string jsonPath, string sessionDirectory, string replaysDirectory)
        {
            try
            {
                string text = File.ReadAllText(jsonPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(text)) return null;

                var s = JsonSerializer.Deserialize<HighlightSession>(text, ReadOptions);
                if (s == null) return null;

                s.JsonPath = jsonPath;
                s.ShotsDirectory = sessionDirectory;
                s.ReplaysDirectory = replaysDirectory ?? "";
                if (string.IsNullOrWhiteSpace(s.SessionId))
                {
                    // 老文件没写 sessionId 就退回文件名，至少让卡片标题不空
                    s.SessionId = Path.GetFileNameWithoutExtension(jsonPath);
                }
                foreach (HighlightItem it in s.Items)
                {
                    it.ShotsDirectory = sessionDirectory;
                }
                return s;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>这一局里出现过的类型（给筛选按钮用）。</summary>
        public static List<string> CollectTypes(IEnumerable<HighlightSession> sessions)
        {
            return sessions
                .SelectMany(s => s.Items)
                .Select(i => i.Type)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// 把启动器设置推给模组（= WriteModConfig + 从 AppSettings 取值的包装）。
        /// 两个调用点：启动器拉起游戏之前、以及用户在高光页改开关时。
        /// 模组每次开会话都会重读 config.json，所以改完不需要重启游戏。
        /// </summary>
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
                // 模组那边叫 onlyInSession，语义与「单人模式也记录」正好相反
                !settings.HighlightInSinglePlayer);
        }

        /// <summary>
        /// 把启动器设置写给模组读（&lt;replays&gt;/startride/config.json）。
        /// 模组每次开会话都会读一遍，所以这里改完不需要重启游戏。
        /// 返回 null 表示成功，否则是错误说明 —— 写不进去不该让设置页崩掉。
        /// </summary>
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
                // 不带 BOM：BeamNG 的 jsonReadFile 不保证吃 BOM
                File.WriteAllText(Path.Combine(dir, "config.json"), json, new UTF8Encoding(false));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
