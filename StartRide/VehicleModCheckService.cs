using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace StartRide.Core
{
    /// <summary>车辆模组的一个问题。</summary>
    public sealed class VehicleModIssue
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public string Reason { get; init; } = "";

        public string DisplayText => Name + " — " + Reason;
    }

    /// <summary>车辆模组检查结果。</summary>
    public sealed class VehicleModCheckResult
    {
        public int Total { get; init; }
        public List<VehicleModIssue> Issues { get; init; } = new List<VehicleModIssue>();
        public string Directory { get; init; } = "";

        public bool HasIssues => Issues.Count > 0;
    }

    /// <summary>
    /// 车辆模组完整性检查。
    ///
    /// BeamNG 装坏一个车辆 zip（下载中断、盘满、Steam 校验修到一半）后，
    /// 现象是"进游戏选车时这个车不见了"或者直接卡加载 —— 游戏不会告诉你哪个包坏了。
    /// 这里在启动器侧提前把坏包挑出来：0 字节、不是合法 zip、zip 里没有 .jbeam。
    /// </summary>
    public sealed class VehicleModCheckService
    {
        private readonly AppSettings _settings;

        public VehicleModCheckService(AppSettings settings) => _settings = settings;

        /// <summary>车辆目录：&lt;游戏目录&gt;\content\vehicles。</summary>
        public string VehiclesDirectory =>
            string.IsNullOrWhiteSpace(_settings.GameDirectory)
                ? ""
                : Path.Combine(_settings.GameDirectory, "content", "vehicles");

        /// <summary>扫描并检查全部车辆包。</summary>
        public VehicleModCheckResult Check()
        {
            var issues = new List<VehicleModIssue>();
            string dir = VehiclesDirectory;

            if (dir.Length == 0 || !Directory.Exists(dir))
            {
                return new VehicleModCheckResult { Directory = dir };
            }

            int total = 0;
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in Directory.GetFiles(dir, "*.zip").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.StartsWith("___", StringComparison.Ordinal)) continue;
                total++;

                // 1) 重名（同一辆车装了两份，游戏加载行为不确定）
                if (seen.TryGetValue(name, out _))
                {
                    issues.Add(new VehicleModIssue { Name = name, Path = file, Reason = "与另一个文件同名，可能装重了" });
                }
                else
                {
                    seen[name] = file;
                }

                // 2) 空文件 / 截断
                long length;
                try { length = new FileInfo(file).Length; }
                catch (Exception ex)
                {
                    issues.Add(new VehicleModIssue { Name = name, Path = file, Reason = "读不到文件：" + ex.Message });
                    continue;
                }

                if (length == 0)
                {
                    issues.Add(new VehicleModIssue { Name = name, Path = file, Reason = "文件是 0 字节，下载没完成" });
                    continue;
                }

                // 3) zip 结构是否还能打开、里面有没有车辆定义文件
                try
                {
                    using var zip = ZipFile.OpenRead(file);
                    int jbeam = 0;
                    int entries = 0;
                    foreach (var entry in zip.Entries)
                    {
                        entries++;
                        if (entry.FullName.EndsWith(".jbeam", StringComparison.OrdinalIgnoreCase)) jbeam++;
                    }

                    if (entries == 0)
                    {
                        issues.Add(new VehicleModIssue { Name = name, Path = file, Reason = "zip 里是空的" });
                    }
                    else if (jbeam == 0)
                    {
                        issues.Add(new VehicleModIssue { Name = name, Path = file, Reason = "zip 里没有 .jbeam 车辆定义，可能不是车辆包" });
                    }
                }
                catch (InvalidDataException)
                {
                    issues.Add(new VehicleModIssue { Name = name, Path = file, Reason = "压缩包已损坏，无法打开" });
                }
                catch (Exception ex)
                {
                    issues.Add(new VehicleModIssue { Name = name, Path = file, Reason = ex.Message });
                }
            }

            return new VehicleModCheckResult
            {
                Total = total,
                Issues = issues,
                Directory = dir,
            };
        }

        /// <summary>
        /// 把问题包列成一行短摘要（用于启动时的浮动提示，太长会糊屏）。
        /// </summary>
        public static string Summarize(VehicleModCheckResult result)
        {
            if (!result.HasIssues) return "";
            var names = result.Issues.Take(3).Select(i => i.Name).ToList();
            string head = string.Join("、", names);
            if (result.Issues.Count > names.Count) head += " 等 " + result.Issues.Count + " 个";
            return head;
        }

        /// <summary>
        /// 从车辆列表文件里挖出「车辆显示名」，用于日志/报表的可读性。
        /// 读不到就返回包名本身（不影响判定）。
        /// </summary>
        internal static string TryReadDisplayName(string zipPath, string fallback)
        {
            try
            {
                using var zip = ZipFile.OpenRead(zipPath);
                var info = zip.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith("info.json", StringComparison.OrdinalIgnoreCase));
                if (info == null) return fallback;

                using var stream = info.Open();
                using var reader = new StreamReader(stream);
                string text = reader.ReadToEnd();
                var m = Regex.Match(text, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                return m.Success ? m.Groups[1].Value : fallback;
            }
            catch
            {
                return fallback;
            }
        }
    }
}
