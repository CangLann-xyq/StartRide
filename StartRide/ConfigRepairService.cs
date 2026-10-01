using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
    public enum ConfigFileState
    {
        Unknown = 0,
        Ok,
        Missing,
        Broken,
        Repaired,
        TemplateMissing,
        Failed,
    }

    public sealed class ConfigFileStatus
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public string FullPath { get; set; } = "";
        public ConfigFileState State { get; set; } = ConfigFileState.Unknown;
        public string Detail { get; set; } = "";
        public bool NeedsRepair => State == ConfigFileState.Missing || State == ConfigFileState.Broken;
    }

    public sealed class ConfigRepairResult
    {
        public int Checked { get; set; }
        public int Repaired { get; set; }
        public int Failed { get; set; }
        public bool ServerReachable { get; set; }
        public string? Error { get; set; }
        public List<ConfigFileStatus> Files { get; } = new List<ConfigFileStatus>();
    }

    public sealed class ConfigRepairService
    {
        public sealed class EssentialFile
        {
            public string Key { get; }
            public string RelativePath { get; }
            public string DisplayName { get; }
            public bool StrictJson { get; }

            public EssentialFile(string key, string relativePath, string displayName, bool strictJson)
            {
                Key = key;
                RelativePath = relativePath;
                DisplayName = displayName;
                StrictJson = strictJson;
            }
        }

        public static readonly EssentialFile[] EssentialFiles =
        {
            new EssentialFile("settings.json", "settings.json", "游戏主设置", true),
            new EssentialFile("game-settings.json", "game-settings.json", "画质与引擎设置", true),
            new EssentialFile("cloud-settings.json", Path.Combine("cloud", "settings.json"), "在线与单位设置", true),
            new EssentialFile("imgui-settings.json", "imguiSettings.json", "内置应用布局", true),
            new EssentialFile("keyboard.diff", Path.Combine("inputmaps", "keyboard.diff"), "键盘键位", false),
        };

        private readonly AppSettings settings;

        private readonly string? settingsDirectoryOverride;

        public ConfigRepairService(AppSettings? settings = null, string? settingsDirectoryOverride = null)
        {
            this.settings = settings ?? AppSettings.Current;
            this.settingsDirectoryOverride = settingsDirectoryOverride;
        }

        public string SettingsDirectory => string.IsNullOrWhiteSpace(settingsDirectoryOverride)
            ? settings.ResolveSettingsDirectory()
            : settingsDirectoryOverride!;

        public string GetFullPath(EssentialFile file) => Path.Combine(SettingsDirectory, file.RelativePath);

        public List<ConfigFileStatus> Inspect()
        {
            var list = new List<ConfigFileStatus>();
            foreach (var f in EssentialFiles)
            {
                string path = GetFullPath(f);
                var status = new ConfigFileStatus
                {
                    Key = f.Key,
                    DisplayName = f.DisplayName,
                    RelativePath = f.RelativePath,
                    FullPath = path,
                };

                if (!File.Exists(path))
                {
                    status.State = ConfigFileState.Missing;
                    status.Detail = "文件不存在";
                }
                else if (IsUsable(path, f.StrictJson, out string why))
                {
                    status.State = ConfigFileState.Ok;
                    status.Detail = "正常";
                }
                else
                {
                    status.State = ConfigFileState.Broken;
                    status.Detail = why;
                }
                list.Add(status);
            }
            return list;
        }

        public static bool IsUsable(string path, bool strictJson, out string reason)
        {
            reason = "正常";
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) { reason = "文件不存在"; return false; }
                if (info.Length == 0) { reason = "空文件"; return false; }

                string text = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(text)) { reason = "内容为空"; return false; }

                if (!strictJson)
                {

                    if (text.IndexOf("bindings", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        reason = "缺少 bindings 段";
                        return false;
                    }
                    try
                    {
                        using var diffDoc = JsonDocument.Parse(StripControlChars(text));
                    }
                    catch (JsonException)
                    {
                        reason = "键位文件解析失败（文件已损坏）";
                        return false;
                    }
                    return true;
                }

                using var doc = JsonDocument.Parse(StripControlChars(text));
                return true;
            }
            catch (JsonException)
            {
                reason = "JSON 解析失败（文件已损坏）";
                return false;
            }
            catch (Exception ex)
            {
                reason = "无法读取：" + ex.Message;
                return false;
            }
        }

        public static string StripControlChars(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                sb.Append(ch >= ' ' || ch == '\r' || ch == '\n' || ch == '\t' ? ch : ' ');
            }
            return sb.ToString();
        }

        public async Task<ConfigRepairResult> RepairAsync(
            ApiService api, Action<string>? log = null, CancellationToken cancellationToken = default)
        {
            var result = new ConfigRepairResult();
            var statuses = Inspect();
            result.Checked = statuses.Count;
            result.Files.AddRange(statuses);

            var damaged = statuses.Where(s => s.NeedsRepair).ToList();
            if (damaged.Count == 0) return result;

            var templates = await api.GetConfigTemplatesAsync().ConfigureAwait(false);
            if (templates.Count == 0)
            {
                result.ServerReachable = false;
                result.Error = "取不到云端配置模板（可能离线）";
                foreach (var s in damaged)
                {
                    s.State = ConfigFileState.TemplateMissing;
                    s.Detail = "无法连接云端模板库";
                    result.Failed++;
                }
                return result;
            }

            result.ServerReachable = true;
            var byKey = templates
                .GroupBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var status in damaged)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!byKey.ContainsKey(status.Key))
                {
                    status.State = ConfigFileState.TemplateMissing;
                    status.Detail = "云端没有这个模板";
                    result.Failed++;
                    continue;
                }

                try
                {
                    var tpl = await api.GetConfigTemplateAsync(status.Key).ConfigureAwait(false);
                    if (tpl == null || string.IsNullOrWhiteSpace(tpl.Content))
                    {
                        status.State = ConfigFileState.Failed;
                        status.Detail = "模板下载失败";
                        result.Failed++;
                        continue;
                    }

                    string dir = Path.GetDirectoryName(status.FullPath)!;
                    Directory.CreateDirectory(dir);

                    if (File.Exists(status.FullPath))
                    {
                        string backup = status.FullPath + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                        try { File.Move(status.FullPath, backup); } catch { }
                    }

                    File.WriteAllText(status.FullPath, tpl.Content, new UTF8Encoding(false));
                    status.State = ConfigFileState.Repaired;
                    status.Detail = "已从云端模板补全（" + tpl.Size + " 字节）";
                    result.Repaired++;
                    log?.Invoke("已补全配置文件：" + status.RelativePath);
                }
                catch (Exception ex)
                {
                    status.State = ConfigFileState.Failed;
                    status.Detail = "补全失败：" + ex.Message;
                    result.Failed++;
                    log?.Invoke("补全配置文件失败：" + status.RelativePath + " -> " + ex.Message);
                }
            }

            return result;
        }
    }
}
