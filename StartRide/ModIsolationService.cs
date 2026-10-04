using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StartRide.Core
{

    /// <summary>
    /// 联机时的模组隔离。
    ///
    /// 为什么需要它：StartRide 的联机模组要和别的联机模组（BeamMP、BeamLink 等）
    /// 抢同一批远程车辆对象。两个都在跑时，远程车会被对方接管、抖动、反复消失，
    /// 而玩家看到的只是「联机时看不见别人」。
    ///
    /// 以前（startride_mod.lua 的 conflictScan）只是**检测并弹窗警告**，让玩家自己
    /// 去 mods 目录手动移。参考 BeamLink 的做法把它做成**自动隔离**：
    ///   1. 进联机前，把 mods/db.json 里所有非 StartRide 的模组 active 置 false；
    ///   2. 同时把「原来哪些是开着的」记进一份日志（journal）；
    ///   3. 退出游戏后，按日志原样恢复。
    ///
    /// 三层安全约束（都是踩过的坑，别省）：
    ///   - **只改 active 字段**，绝不删除/移动模组文件本身；
    ///   - 关游戏前**必写日志**，日志写不成就不动手（宁可带冲突进游戏，也不能把玩家的模组弄丢）；
    ///   - 恢复时**只认日志里记过的条目**，日志里没有的模组一律不碰（玩家这期间新装的模组不受影响）。
    /// </summary>
    public sealed class ModIsolationService
    {
        private readonly AppSettings _settings;

        public ModIsolationService(AppSettings settings) => _settings = settings;

        public event Action<string>? Log;

        /// <summary>日志文件名。放在 mods 目录下，跟着存档目录走。</summary>
        private const string JournalName = "startride-mod-isolation.json";

        /// <summary>日志格式版本。将来改结构时用它把老日志识别成「不认识、不恢复」。</summary>
        private const int JournalVersion = 1;

        /// <summary>
        /// 被隔离的模组名里若含这些片段，一律当「自己人」，永不隔离。
        /// 比对前会把名字里的分隔符去掉，所以 "Start Ride"、"start-ride"、"START_RIDE"
        /// 也都能认出来 —— 玩家/打包工具对我们的模组命名并不统一。
        /// </summary>
        private static readonly string[] SelfMarkers = { "startride" };

        /// <summary>
        /// 这个模组名是不是「自己人」（StartRide 自己的模组 / 内置模组）。
        /// 自己人永不隔离。
        ///
        /// ⚠️ 判不准的（空名、叫不出名字的）一律当自己人处理 —— 宁可漏隔离一个，
        /// 也不能把玩家的东西关了还没法恢复。
        /// </summary>
        public static bool IsSelfMod(string? modName)
        {
            if (string.IsNullOrWhiteSpace(modName)) return true; // 叫不出名字的别碰
            string normalized = Normalize(modName);
            return SelfMarkers.Any(m => normalized.Contains(m, StringComparison.Ordinal));
        }

        /// <summary>
        /// 归一化：小写 + 去掉所有非字母数字字符。
        /// "Start Ride" / "start-ride" / "START_RIDE.zip" / "start.ride" → "startride"
        /// </summary>
        private static string Normalize(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (char c in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        private string JournalPath => Path.Combine(ModsDirectory, JournalName);

        private string DbPath => Path.Combine(ModsDirectory, "db.json");

        private string ModsDirectory => _settings.ResolveModsDirectory();

        /// <summary>
        /// 一条被隔离模组的记录。恢复时靠它把 active 打回原值。
        /// </summary>
        private sealed class JournalEntry
        {
            public string ModName { get; set; } = "";

            /// <summary>隔离前是不是开着的。只恢复「本来是开的」那些。</summary>
            public bool WasActive { get; set; }
        }

        private sealed class Journal
        {
            public int Version { get; set; } = JournalVersion;
            public string CreatedUtc { get; set; } = "";
            public string ModsDirectory { get; set; } = "";
            public List<JournalEntry> Isolated { get; set; } = new();
        }

        /// <summary>当前有没有一份待恢复的隔离日志。</summary>
        public bool HasPendingJournal
        {
            get
            {
                try { return File.Exists(JournalPath); }
                catch { return false; }
            }
        }

        /// <summary>
        /// 进联机前调用：把所有非自己的模组关掉，并把原状态记进日志。
        ///
        /// 返回被隔离的模组名列表。任何异常都吞掉并返回空表 ——
        /// 隔离失败最多是「带着冲突进游戏」，比抛异常让联机开不起来强。
        /// </summary>
        public IReadOnlyList<string> Isolate()
        {
            var isolated = new List<string>();
            try
            {
                // 已经有一份日志了 → 说明上次没恢复干净，先把它恢复掉再重新隔离，
                // 免得把「已经被我们关掉的模组」当成「本来就是关的」记错。
                if (HasPendingJournal) Restore();

                if (!File.Exists(DbPath))
                {
                    Log?.Invoke("模组隔离：db.json 不存在，跳过（玩家可能还没开过游戏）");
                    return isolated;
                }

                JsonNode? root = JsonNode.Parse(File.ReadAllText(DbPath, Encoding.UTF8));
                if (root is not JsonObject obj || obj["mods"] is not JsonObject mods)
                {
                    Log?.Invoke("模组隔离：db.json 结构不认识，跳过");
                    return isolated;
                }

                var journal = new Journal
                {
                    Version = JournalVersion,
                    CreatedUtc = DateTime.UtcNow.ToString("o"),
                    ModsDirectory = ModsDirectory,
                };
                var keysToDisable = new List<string>();

                foreach (var (key, node) in mods.ToList())
                {
                    if (node is not JsonObject entry) continue;

                    string name = ReadName(entry, key);
                    if (IsSelfMod(name)) continue;

                    bool active = !(entry["active"] is JsonValue av
                                    && av.TryGetValue(out bool b) && !b);
                    if (!active) continue; // 本来就关着，不用管

                    journal.Isolated.Add(new JournalEntry { ModName = name, WasActive = true });
                    entry["active"] = false;   // 内存里同步一份，供 journal 之外的调用方（如 UI）读
                    keysToDisable.Add(key);    // ⚠️ 必须在这一趟里收集：下一趟内存已被改成 false，再判就读不出来了
                    isolated.Add(name);
                }

                if (journal.Isolated.Count == 0)
                {
                    Log?.Invoke("模组隔离：没有需要隔离的模组");
                    return isolated;
                }

                // ⚠️ 顺序关键：**先写日志，再动 db.json**。
                // 反过来的话，万一写日志失败，db.json 已经被改了却没有恢复依据 ——
                // 玩家的模组就永远关着了。
                WriteJournal(journal);

                // ⚠️ 落盘必须走**定点替换**，不能用 JsonNode.ToJsonString 整文件重写：
                //    .NET 的 WriteIndented 会把 BeamNG 的紧凑大括号风格（`"key":{`）改成
                //    大括号独占行、并把 \n 全写成 \r\n —— 实测 2.05 MB 的文件会膨胀 3.2%，
                //    等于每次进联机都悄悄污染一遍玩家的 db.json（语义虽对，但很脏）。
                //    定点替换只把 `"active":true` 改成 `"active":false`，
                //    125 个模组只多 125 字节，且恢复时能**逐字节还原**。
                PatchActiveFlags(keysToDisable, enable: false);

                Log?.Invoke($"模组隔离：已临时关闭 {journal.Isolated.Count} 个第三方模组（退出后自动恢复）");
            }
            catch (Exception ex)
            {
                Log?.Invoke("模组隔离失败（不影响联机）：" + ex.Message);
            }
            return isolated;
        }

        /// <summary>
        /// 退出游戏后调用：按日志把模组原样恢复，然后删掉日志。
        ///
        /// 恢复只认日志里记过的条目 —— 玩家在联机期间新装的模组不会被动到。
        /// </summary>
        public IReadOnlyList<string> Restore()
        {
            var restored = new List<string>();
            try
            {
                if (!File.Exists(JournalPath))
                    return restored;

                Journal? journal;
                try
                {
                    journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(JournalPath, Encoding.UTF8));
                }
                catch
                {
                    // 日志读不出来：宁可不动 db.json，也不能瞎猜该恢复成什么。
                    Log?.Invoke("模组隔离：日志损坏，保留现场不动（模组可手动在模组管理里重新启用）");
                    return restored;
                }

                if (journal is null || journal.Version != JournalVersion)
                {
                    Log?.Invoke("模组隔离：日志版本不认识，保守起见不动 db.json");
                    return restored;
                }

                if (!File.Exists(DbPath))
                {
                    Log?.Invoke("模组隔离：db.json 不见了，保留日志等待下次恢复");
                    return restored;
                }

                JsonNode? root = JsonNode.Parse(File.ReadAllText(DbPath, Encoding.UTF8));
                if (root is not JsonObject obj || obj["mods"] is not JsonObject mods)
                {
                    Log?.Invoke("模组隔离：db.json 结构不认识，保留日志");
                    return restored;
                }

                var wanted = new HashSet<string>(
                    journal.Isolated.Where(e => e.WasActive).Select(e => e.ModName),
                    StringComparer.OrdinalIgnoreCase);
                var keysToEnable = new List<string>();

                foreach (var (key, node) in mods.ToList())
                {
                    if (node is not JsonObject entry) continue;
                    string name = ReadName(entry, key);
                    if (!wanted.Contains(name)) continue;

                    entry["active"] = true;
                    keysToEnable.Add(key);
                    restored.Add(name);
                }

                // 同样走定点替换，保证能逐字节还原成玩家原来的 db.json。
                PatchActiveFlags(keysToEnable, enable: true);

                // 恢复成功才删日志。上面任何一步失败都提前 return，日志留着下次再试。
                TryDeleteJournal();

                if (restored.Count > 0)
                    Log?.Invoke($"模组隔离：已恢复 {restored.Count} 个模组");
            }
            catch (Exception ex)
            {
                Log?.Invoke("模组恢复失败（可在模组管理里手动开启）：" + ex.Message);
            }
            return restored;
        }

        /// <summary>
        /// 从一条 db.json 条目里取模组名。取不到就退回 map 的 key
        /// —— BeamNG 的 db.json 里 modname 和 key 通常一致，但不保证。
        /// </summary>
        private static string ReadName(JsonObject entry, string fallbackKey)
        {
            foreach (var field in new[] { "modname", "modName", "name", "title", "filename", "modId" })
            {
                if (entry[field] is JsonValue v && v.TryGetValue(out string? s)
                    && !string.IsNullOrWhiteSpace(s))
                {
                    return s.Trim();
                }
            }
            return fallbackKey;
        }

        /// <summary>
        /// 字节级定点替换：只改指定条目里的 `"active"` 值，其余字节（缩进、换行、
        /// 键序、大括号风格）一律原样保留。
        ///
        /// 为什么必须这么做：BeamNG 自己写的 db.json 用「紧凑大括号 + LF」风格，
        /// 而 .NET 的 JsonNode.ToJsonString(WriteIndented=true) 会改成「大括号独占行 +
        /// CRLF」。整文件重写虽然语义正确，却会让玩家的 db.json 每次进出联机都被改写一遍。
        /// 定点替换则做到**隔离只多几个字节、恢复后逐字节还原**。
        ///
        /// 定位方式：每个模组条目的 key 在文件里以 `"&lt;key&gt;":{` 唯一出现
        /// （已实测：126 个条目全部唯一，且 modname == key 无重名），
        /// 从该位置做花括号配对扫描得到条目区间，只在区间内替换。
        /// </summary>
        private void PatchActiveFlags(IReadOnlyList<string> keys, bool enable)
        {
            if (keys.Count == 0) return;

            // 保留原始字节风格：按 UTF-8 读、按 UTF-8 写，不加 BOM、不做换行转换。
            string text = File.ReadAllText(DbPath, new UTF8Encoding(false));
            var utf8NoBom = new UTF8Encoding(false);

            string from = enable ? "\"active\":false" : "\"active\":true";
            string to = enable ? "\"active\":true" : "\"active\":false";

            // 收集 (start, end, newText)，统一从后往前应用，避免偏移失效。
            var edits = new List<(int Start, int End, string New)>();
            foreach (string key in keys)
            {
                if (string.IsNullOrEmpty(key)) continue;

                string marker = "\"" + key + "\":{";
                int i = text.IndexOf(marker, StringComparison.Ordinal);
                if (i < 0) continue;

                int end = FindObjectEnd(text, i + marker.Length - 1);
                if (end < 0) continue;

                string seg = text.Substring(i, end - i);
                int at = seg.IndexOf(from, StringComparison.Ordinal);
                if (at < 0) continue;

                string segNew = seg.Remove(at, from.Length).Insert(at, to);
                edits.Add((i, end, segNew));
            }

            edits.Sort((a, b) => b.Start.CompareTo(a.Start));
            foreach (var (start, end, segNew) in edits)
                text = text.Remove(start, end - start).Insert(start, segNew);

            // 临时文件 + 原子改名：写一半被中断也不会留下半个 db.json。
            string tmp = DbPath + ".srtmp";
            File.WriteAllText(tmp, text, utf8NoBom);
            if (File.Exists(DbPath)) File.Delete(DbPath);
            File.Move(tmp, DbPath);
        }

        /// <summary>
        /// 从 <paramref name="openBrace"/>（指向 '{' 的下标）开始做花括号配对扫描，
        /// 返回配对 '}' 之后一位的下标；扫描不到返回 -1。
        /// 会正确跳过字符串字面量里的括号与转义。
        /// </summary>
        private static int FindObjectEnd(string text, int openBrace)
        {
            if (openBrace < 0 || openBrace >= text.Length || text[openBrace] != '{') return -1;

            int depth = 0;
            int p = openBrace;
            while (p < text.Length)
            {
                char c = text[p];
                if (c == '"')
                {
                    // 跳过整个字符串字面量
                    int q = p + 1;
                    while (q < text.Length)
                    {
                        if (text[q] == '\\') { q += 2; continue; }
                        if (text[q] == '"') break;
                        q++;
                    }
                    p = q + 1;
                    continue;
                }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return p + 1;
                }
                p++;
            }
            return -1;
        }

        private void WriteJournal(Journal journal)
        {
            Directory.CreateDirectory(ModsDirectory);
            string json = JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true });

            // 先写临时文件再原子改名：避免写一半被中断留下半个日志。
            string tmp = JournalPath + ".tmp";
            File.WriteAllText(tmp, json, Encoding.UTF8);
            if (File.Exists(JournalPath)) File.Delete(JournalPath);
            File.Move(tmp, JournalPath);
        }

        private void TryDeleteJournal()
        {
            try { if (File.Exists(JournalPath)) File.Delete(JournalPath); }
            catch { }
        }
    }
}
