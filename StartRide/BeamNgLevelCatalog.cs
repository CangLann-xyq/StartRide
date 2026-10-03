using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace StartRide.Core
{
    /// <summary>关卡里的一个出生点（对应 scenetree 里的一个对象）。</summary>
    public sealed class BeamNgSpawnPoint
    {
        /// <summary>scenetree 对象名，例如 <c>spawns_industrial</c>。模组就是拿它 findObject 求位置。</summary>
        public string ObjectName { get; set; } = "";

        /// <summary>本地化后的显示名，例如「工业区」。</summary>
        public string Name { get; set; } = "";

        /// <summary>本地化后的一句话说明。</summary>
        public string Description { get; set; } = "";

        /// <summary>是否是该关卡 info.json 里声明的默认出生点。</summary>
        public bool IsDefault { get; set; }
    }

    /// <summary>一个可选的 BeamNG 地图（关卡）。</summary>
    public sealed class BeamNgLevel
    {
        /// <summary>关卡 id（= zip/目录名），也是 -level 参数要传的值。</summary>
        public string Id { get; set; } = "";

        /// <summary>本地化后的地图名，例如「美国西海岸」。</summary>
        public string Name { get; set; } = "";

        /// <summary>info.json 里的 defaultSpawnPointName。</summary>
        public string DefaultSpawnPoint { get; set; } = "";

        /// <summary>是否来自用户 mods 目录（而不是游戏自带 content/levels）。</summary>
        public bool IsMod { get; set; }

        /// <summary>本地化后的一句话介绍（info.json description 的词条）。</summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 地图缩略图的本地缓存路径（file:/// 形式，直接绑 Image.Source）。
        /// 从关卡包的 previews[] 里抽出来的第一张，抽不出来就是空串。
        /// </summary>
        public string PreviewPath { get; set; } = "";

        public List<BeamNgSpawnPoint> SpawnPoints { get; } = new();

        /// <summary>用户能看懂的出生点数量文案，例如「13 个出生点」。</summary>
        public string SpawnCountText => SpawnPoints.Count > 0
            ? SpawnPoints.Count + " 个出生点"
            : "仅默认出生点";
    }

    /// <summary>
    /// BeamNG 关卡目录：从游戏目录（以及 mods 目录）里把可玩地图和它们的出生点扫出来。
    ///
    /// 为什么要读游戏自己的文件而不是内置一张表：
    ///   1) 玩家装了 mod 地图、官方更新加了关卡，表就会过期；
    ///   2) 出生点就是 scenetree 里的对象名（spawns_industrial 之类），只能从 info.json 拿，
    ///      硬编码坐标会被地图改动打脸。
    ///
    /// 坑：BeamNG 的 info.json 是「带尾逗号」的宽松 JSON（west_coast_usa 第 130 行就是），
    ///     必须开 AllowTrailingCommas，否则一解析就炸。
    /// </summary>
    public static class BeamNgLevelCatalog
    {
        /// <summary>官方开发用的脚手架关卡，不给玩家选。</summary>
        private static readonly HashSet<string> Hidden =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "template", "autotest" };

        private static readonly object Gate = new object();
        private static List<BeamNgLevel>? _cache;
        private static string _cacheKey = "";

        /// <summary>换地图 / 换语言 / 换游戏目录后必须清缓存。</summary>
        public static void Invalidate()
        {
            lock (Gate)
            {
                _cache = null;
                _cacheKey = "";
            }
        }

        public static List<BeamNgLevel> Load(AppSettings settings)
        {
            if (settings == null) return new List<BeamNgLevel>();

            string gameDir = settings.GameDirectory ?? "";
            string locale = GameLocale(settings.Language);
            string key = gameDir + "|" + locale;

            lock (Gate)
            {
                if (_cache != null && string.Equals(_cacheKey, key, StringComparison.Ordinal)) return _cache;
                var built = Build(gameDir, locale, settings.ResolveModsDirectory());
                _cache = built;
                _cacheKey = key;
                return built;
            }
        }

        public static BeamNgLevel? Find(AppSettings settings, string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return Load(settings).FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>只想要一个名字时用它（找不到就回落到 id 本身）。</summary>
        public static string DisplayName(AppSettings settings, string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "";
            return Find(settings, id)?.Name ?? id!;
        }

        /// <summary>该关卡里有没有这个出生点，没有就当没选。</summary>
        public static bool HasSpawnPoint(AppSettings settings, string? levelId, string? spawnPoint)
        {
            if (string.IsNullOrWhiteSpace(spawnPoint)) return false;
            var level = Find(settings, levelId);
            if (level == null) return false;
            return level.SpawnPoints.Any(s => string.Equals(s.ObjectName, spawnPoint, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>把默认 UI 语言映射到 BeamNG 的 locales/translations 目录名。</summary>
        private static string GameLocale(string? launcherLanguage)
        {
            string l = (launcherLanguage ?? "").Trim().Replace('_', '-').ToLowerInvariant();
            if (l.StartsWith("zh-hant") || l.StartsWith("zh-hk") || l.StartsWith("zh-tw") || l.StartsWith("zh-mo"))
                return "zh_Hant";
            if (l.StartsWith("zh")) return "zh_Hans";
            if (l.StartsWith("ja")) return "ja_JP";
            if (l.StartsWith("ko")) return "ko_KR";
            if (l.StartsWith("de")) return "de_DE";
            if (l.StartsWith("fr")) return "fr_FR";
            if (l.StartsWith("ru")) return "ru_RU";
            if (l.StartsWith("pl")) return "pl_PL";
            if (l.StartsWith("pt-br")) return "pt_BR";
            if (l.StartsWith("pt")) return "pt_PT";
            if (l.StartsWith("es-4")) return "es_419";
            if (l.StartsWith("es")) return "es_ES";
            return "en-US";
        }

        private static List<BeamNgLevel> Build(string gameDir, string locale, string? modsDir)
        {
            var map = new Dictionary<string, BeamNgLevel>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir)) return new List<BeamNgLevel>();

            var text = LoadTranslations(gameDir, locale);

            try
            {
                string official = Path.Combine(gameDir, "content", "levels");
                if (Directory.Exists(official))
                {
                    foreach (var zip in SafeFiles(official, "*.zip"))
                        ReadZip(zip, text, isMod: false, map);

                    foreach (var dir in SafeDirectories(official))
                        ReadFolder(dir, text, isMod: false, map);
                }
            }
            catch { }

            // mods 里的地图：只读 zip 的中央目录，发现 levels/*/info.json 才真的打开它
            try
            {
                if (!string.IsNullOrWhiteSpace(modsDir) && Directory.Exists(modsDir))
                {
                    foreach (var zip in SafeFiles(modsDir, "*.zip"))
                        ReadZip(zip, text, isMod: true, map);
                }
            }
            catch { }

            var list = map.Values
                .OrderByDescending(l => l.SpawnPoints.Count > 0)
                .ThenByDescending(l => string.Equals(l.Id, "west_coast_usa", StringComparison.OrdinalIgnoreCase))
                .ThenBy(l => l.Name, StringComparer.CurrentCulture)
                .ToList();
            return list;
        }

        private static IEnumerable<string> SafeFiles(string dir, string pattern)
        {
            try { return Directory.GetFiles(dir, pattern); } catch { return Array.Empty<string>(); }
        }

        private static IEnumerable<string> SafeDirectories(string dir)
        {
            try { return Directory.GetDirectories(dir); } catch { return Array.Empty<string>(); }
        }

        private static void ReadZip(string zipPath, IReadOnlyDictionary<string, string> text, bool isMod,
            Dictionary<string, BeamNgLevel> map)
        {
            try
            {
                using var zip = ZipFile.OpenRead(zipPath);

                // levels/<id>/info.json —— 只认这一层，levels/x/driftSpots/y/info.json 是漂移点不是地图
                foreach (var entry in zip.Entries)
                {
                    if (!entry.FullName.EndsWith("/info.json", StringComparison.OrdinalIgnoreCase)) continue;

                    var parts = entry.FullName.Split('/');
                    if (parts.Length != 3) continue;
                    if (!string.Equals(parts[0], "levels", StringComparison.OrdinalIgnoreCase)) continue;

                    string id = parts[1];
                    if (id.Length == 0 || Hidden.Contains(id) || map.ContainsKey(id)) continue;

                    var level = ParseLevel(id, ReadText(entry), text, isMod);
                    if (level == null) continue;

                    // 关卡预览图（info.json 的 previews[] 里的第一张），抽到本地缓存当缩略图
                    level.PreviewPath = ExtractPreview(zip, entry.FullName, level, isMod);
                    map[id] = level;
                }
            }
            catch { }
        }

        private static void ReadFolder(string levelDir, IReadOnlyDictionary<string, string> text, bool isMod,
            Dictionary<string, BeamNgLevel> map)
        {
            try
            {
                string id = Path.GetFileName(levelDir);
                if (id.Length == 0 || Hidden.Contains(id) || map.ContainsKey(id)) return;

                string info = Path.Combine(levelDir, "info.json");
                if (!File.Exists(info)) return;

                var level = ParseLevel(id, File.ReadAllText(info), text, isMod);
                if (level == null) return;

                level.PreviewPath = CopyPreviewFromFolder(levelDir, level, isMod);
                map[id] = level;
            }
            catch { }
        }

        private static BeamNgLevel? ParseLevel(string id, string json, IReadOnlyDictionary<string, string> text, bool isMod)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });

                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;

                var level = new BeamNgLevel
                {
                    Id = id,
                    IsMod = isMod,
                    Name = id,
                };

                if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    string key = title.GetString() ?? "";
                    level.Name = Lookup(text, key) ?? HumanizeLevelId(id);
                    level.Description = Lookup(text, key + ".description") ?? "";
                }
                else
                {
                    level.Name = HumanizeLevelId(id);
                }

                if (level.Description.Length == 0)
                    level.Description = Lookup(text, "levels." + id + ".info.description") ?? "";

                if (root.TryGetProperty("defaultSpawnPointName", out var dsp) && dsp.ValueKind == JsonValueKind.String)
                    level.DefaultSpawnPoint = dsp.GetString() ?? "";

                if (root.TryGetProperty("spawnPoints", out var sp) && sp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in sp.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object) continue;

                        string obj = item.TryGetProperty("objectname", out var o) && o.ValueKind == JsonValueKind.String
                            ? o.GetString() ?? "" : "";
                        if (obj.Length == 0) continue;

                        string tid = item.TryGetProperty("translationId", out var t) && t.ValueKind == JsonValueKind.String
                            ? t.GetString() ?? "" : "";

                        var point = new BeamNgSpawnPoint
                        {
                            ObjectName = obj,
                            Name = Lookup(text, tid) ?? HumanizeObjectName(obj),
                            Description = Lookup(text, tid + ".description") ?? "",
                            IsDefault = obj.Equals(level.DefaultSpawnPoint, StringComparison.OrdinalIgnoreCase),
                        };
                        level.SpawnPoints.Add(point);
                    }
                }

                // info.json 有 defaultSpawnPointName 但 spawnPoints 里没列它：补进去，别让玩家无处可选
                if (level.DefaultSpawnPoint.Length > 0 &&
                    !level.SpawnPoints.Any(s => string.Equals(s.ObjectName, level.DefaultSpawnPoint, StringComparison.OrdinalIgnoreCase)))
                {
                    level.SpawnPoints.Insert(0, new BeamNgSpawnPoint
                    {
                        ObjectName = level.DefaultSpawnPoint,
                        Name = HumanizeObjectName(level.DefaultSpawnPoint),
                        IsDefault = true,
                    });
                }

                return level;
            }
            catch
            {
                // 坏了就当这个地图不存在，不要让目录整个塌掉
                return null;
            }
        }

        private static string? Lookup(IReadOnlyDictionary<string, string> text, string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return text.TryGetValue(key!, out var v) && v.Length > 0 ? v : null;
        }

        /// <summary>词条缺失时的兜底：west_coast_usa → West Coast, USA 有点难，退回可读化即可。</summary>
        private static string HumanizeLevelId(string id)
        {
            string s = id.Replace('_', ' ').Trim();
            if (s.Length == 0) return id;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>spawns_redwood_forest → Redwood Forest</summary>
        private static string HumanizeObjectName(string obj)
        {
            string s = obj;
            if (s.StartsWith("spawns_", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
            else if (s.StartsWith("spawn_", StringComparison.OrdinalIgnoreCase)) s = s.Substring(6);
            if (s.Length == 0) s = obj;

            var parts = s.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return obj;

            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                var p = parts[i];
                // 保留 camelCase 里的词边界：driftTrack → Drift Track
                var word = new StringBuilder();
                for (int j = 0; j < p.Length; j++)
                {
                    char c = p[j];
                    if (j > 0 && char.IsUpper(c) && char.IsLower(p[j - 1])) word.Append(' ');
                    word.Append(j == 0 ? char.ToUpperInvariant(c) : c);
                }
                sb.Append(word);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 读游戏自己的词条表（locales/translations/&lt;locale&gt;/main.translation.json）。
        /// 只留 levels.* 开头的键，省内存也省查找。
        /// </summary>
        private static Dictionary<string, string> LoadTranslations(string gameDir, string locale)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var loc in new[] { locale, "en-US" })
            {
                if (loc.Length == 0) continue;
                string path = Path.Combine(gameDir, "locales", "translations", loc, "main.translation.json");
                if (!File.Exists(path)) continue;

                try
                {
                    using var stream = File.OpenRead(path);
                    using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip,
                    });

                    if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;

                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (!prop.Name.StartsWith("levels.", StringComparison.Ordinal)) continue;
                        if (prop.Value.ValueKind != JsonValueKind.String) continue;
                        result[prop.Name] = prop.Value.GetString() ?? "";
                    }
                }
                catch { }

                if (result.Count > 0) break;
            }

            return result;
        }

        private static string ReadText(ZipArchiveEntry entry)
        {
            using var s = entry.Open();
            using var r = new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return r.ReadToEnd();
        }

        // ---- 地图缩略图 ----

        private static string PreviewDirectory =>
            Path.Combine(StartRidePaths.Root, "level-previews");

        private static string PreviewFileName(BeamNgLevel level) =>
            (level.IsMod ? "mod_" : "") + Sanitize(level.Id) + ".jpg";

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' ? c : '_');
            return sb.ToString();
        }

        private static string ExtractPreview(ZipArchive zip, string infoEntryName, BeamNgLevel level, bool isMod)
        {
            try
            {
                string? preview = FirstPreviewName(zip, infoEntryName);
                if (preview == null) return "";

                string folder = infoEntryName.Substring(0, infoEntryName.Length - "/info.json".Length);
                var entry = zip.GetEntry(folder + "/" + preview.TrimStart('/'))
                            ?? zip.Entries.FirstOrDefault(e =>
                                e.FullName.EndsWith("/" + Path.GetFileName(preview), StringComparison.OrdinalIgnoreCase));
                if (entry == null || entry.Length <= 0 || entry.Length > 4 * 1024 * 1024) return "";

                string target = Path.Combine(PreviewDirectory, PreviewFileName(level));
                if (File.Exists(target) && new FileInfo(target).Length == entry.Length) return AsUri(target);

                Directory.CreateDirectory(PreviewDirectory);
                string tmp = target + ".tmp";
                using (var input = entry.Open())
                using (var output = File.Create(tmp))
                    input.CopyTo(output);
                File.Move(tmp, target, overwrite: true);
                return AsUri(target);
            }
            catch
            {
                return "";
            }
        }

        private static string CopyPreviewFromFolder(string levelDir, BeamNgLevel level, bool isMod)
        {
            try
            {
                string info = Path.Combine(levelDir, "info.json");
                if (!File.Exists(info)) return "";

                using var doc = JsonDocument.Parse(File.ReadAllText(info), new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });

                if (!doc.RootElement.TryGetProperty("previews", out var pv) || pv.ValueKind != JsonValueKind.Array)
                    return "";

                foreach (var item in pv.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) continue;
                    string name = item.GetString() ?? "";
                    if (name.Length == 0) continue;

                    string src = Path.Combine(levelDir, name.TrimStart('/', '\\'));
                    if (!File.Exists(src)) continue;

                    string target = Path.Combine(PreviewDirectory, PreviewFileName(level));
                    Directory.CreateDirectory(PreviewDirectory);
                    if (File.Exists(target) && new FileInfo(target).Length == new FileInfo(src).Length) return AsUri(target);
                    File.Copy(src, target, overwrite: true);
                    return AsUri(target);
                }
            }
            catch { }
            return "";
        }

        private static string? FirstPreviewName(ZipArchive zip, string infoEntryName)
        {
            var entry = zip.GetEntry(infoEntryName);
            if (entry == null) return null;

            using var doc = JsonDocument.Parse(ReadText(entry), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            if (!doc.RootElement.TryGetProperty("previews", out var pv) || pv.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var item in pv.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                string name = item.GetString() ?? "";
                if (name.Length > 0) return name;
            }
            return null;
        }

        /// <summary>WPF 的 Image.Source 认 file:/// 开头的 URI 最稳。</summary>
        private static string AsUri(string path) =>
            "file:///" + path.Replace('\\', '/');
    }
}
