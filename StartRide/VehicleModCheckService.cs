using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace StartRide.Core
{
    public sealed class VehicleModIssue
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public string Reason { get; init; } = "";

        public string DisplayText => Name + " — " + Reason;
    }

    public sealed class VehicleModCheckResult
    {
        public int Total { get; init; }
        public List<VehicleModIssue> Issues { get; init; } = new List<VehicleModIssue>();
        public string Directory { get; init; } = "";

        public bool HasIssues => Issues.Count > 0;
    }

    public sealed class VehicleModCheckService
    {
        private readonly AppSettings _settings;

        public VehicleModCheckService(AppSettings settings) => _settings = settings;

        public string VehiclesDirectory =>
            string.IsNullOrWhiteSpace(_settings.GameDirectory)
                ? ""
                : Path.Combine(_settings.GameDirectory, "content", "vehicles");

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

                if (seen.TryGetValue(name, out _))
                {
                    issues.Add(new VehicleModIssue { Name = name, Path = file, Reason = "与另一个文件同名，可能装重了" });
                }
                else
                {
                    seen[name] = file;
                }

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

        public static string Summarize(VehicleModCheckResult result)
        {
            if (!result.HasIssues) return "";
            var names = result.Issues.Take(3).Select(i => i.Name).ToList();
            string head = string.Join("、", names);
            if (result.Issues.Count > names.Count) head += " 等 " + result.Issues.Count + " 个";
            return head;
        }

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
