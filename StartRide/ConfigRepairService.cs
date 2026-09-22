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
    /// <summary>某个必需配置文件的检查结果。</summary>
    public enum ConfigFileState
    {
        Unknown = 0,
        /// <summary>存在且能正常解析</summary>
        Ok,
        /// <summary>文件不存在</summary>
        Missing,
        /// <summary>存在但内容坏了（空文件 / JSON 解析不过）</summary>
        Broken,
        /// <summary>本次已从云端补全</summary>
        Repaired,
        /// <summary>云端没有这个模板，补不了</summary>
        TemplateMissing,
        /// <summary>补全过程出错</summary>
        Failed,
    }

    public sealed class ConfigFileStatus
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public string FullPath { get; set; } = "";
        public ConfigFileState State { get; set; } = ConfigFileState.Unknown;
        /// <summary>给界面看的一句话说明</summary>
        public string Detail { get; set; } = "";
        public bool NeedsRepair => State == ConfigFileState.Missing || State == ConfigFileState.Broken;
    }

    public sealed class ConfigRepairResult
    {
        public int Checked { get; set; }
        public int Repaired { get; set; }
        public int Failed { get; set; }
        /// <summary>云端模板清单是否取到（false = 离线/服务器不可用）</summary>
        public bool ServerReachable { get; set; }
        public string? Error { get; set; }
        public List<ConfigFileStatus> Files { get; } = new List<ConfigFileStatus>();
    }

    /// <summary>
    /// 游戏必需配置文件的检查与自动补全。
    ///
    /// 背景：BeamNG.drive 的 settings.json / game-settings.json / cloud/settings.json /
    /// 键位文件一旦损坏或丢失，轻则键位错乱，重则游戏起不来。启动器在这里：
    ///   1. 逐个检查必需文件是否存在、能否解析
    ///   2. 缺失/损坏的从云端模板库（/config/template/:key）下载写回
    ///   3. 写回前把坏文件改名备份成 *.broken-时间戳，不直接销毁用户数据
    /// </summary>
    public sealed class ConfigRepairService
    {
        public sealed class EssentialFile
        {
            public string Key { get; }
            public string RelativePath { get; }
            public string DisplayName { get; }
            /// <summary>true = 必须是合法 JSON；false = 只要求存在且非空</summary>
            public bool StrictJson { get; }

            public EssentialFile(string key, string relativePath, string displayName, bool strictJson)
            {
                Key = key;
                RelativePath = relativePath;
                DisplayName = displayName;
                StrictJson = strictJson;
            }
        }

        /// <summary>
        /// 必需配置文件清单。与 storm-server/upload_startride_configs.py 里的 ESSENTIAL 一一对应，
        /// 改一处必须同时改另一处（key 就是服务器上的模板主键）。
        /// </summary>
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

        /// <summary>本机配置目录（&lt;userpath&gt;/settings）。</summary>
        public string SettingsDirectory => string.IsNullOrWhiteSpace(settingsDirectoryOverride)
            ? settings.ResolveSettingsDirectory()
            : settingsDirectoryOverride!;

        public string GetFullPath(EssentialFile file) => Path.Combine(SettingsDirectory, file.RelativePath);

        // ---------------- 检查 ----------------

        /// <summary>只做本地检查，不联网。</summary>
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

        /// <summary>文件是否可用：非空 + （严格 JSON 时能解析）。</summary>
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
                    // 键位文件是 .diff，BeamNG 有时会在字符串里写入控制字符，
                    // 所以先把控制字符换成空格再解析：既能容忍它自己的怪癖，
                    // 又能发现真的被截断/写坏的文件。
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

        /// <summary>去掉 JSON 里不该出现的控制字符（BeamNG 自己偶尔会写坏）。</summary>
        public static string StripControlChars(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                sb.Append(ch >= ' ' || ch == '\r' || ch == '\n' || ch == '\t' ? ch : ' ');
            }
            return sb.ToString();
        }

        // ---------------- 检查 + 补全 ----------------

        /// <summary>
        /// 检查并按需从云端补全。返回结果里带每个文件的最终状态。
        /// 网络不可用时不会抛异常，只在 Error/ServerReachable 上体现。
        /// </summary>
        public async Task<ConfigRepairResult> RepairAsync(
            ApiService api, Action<string>? log = null, CancellationToken cancellationToken = default)
        {
            var result = new ConfigRepairResult();
            var statuses = Inspect();
            result.Checked = statuses.Count;
            result.Files.AddRange(statuses);

            var damaged = statuses.Where(s => s.NeedsRepair).ToList();
            if (damaged.Count == 0) return result;

            // 只有真需要补的时候才联网
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

                    // 坏文件先留档，别让用户自己修好的东西被静默覆盖
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
