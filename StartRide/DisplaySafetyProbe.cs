using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StartRide.Core
{
    /// <summary>
    /// 全屏启动前的显示配置体检。
    ///
    /// 背景（2026-10-03 内测崩溃，CrashRpt ×4）：
    ///   BeamNG 的 <c>GraphicDisplayResolutions</c> 只在**非 Window** 模式下才做合法性校验
    ///   （lua/ge/extensions/core/settings/graphic.lua:486 —— 窗口模式直接 `return`，
    ///   任何分辨率都算 "Passed"）。于是玩家在窗口模式里把窗口拖成 1426x777 之后，
    ///   这个值就被存进 settings.json；等他改成全屏（或启动器传 `-fullscreen` 强开独占全屏），
    ///   同一个 1426x777 会被当成**独占全屏显示模式**去 `D3D11Device::reset`，
    ///   而任何显示器都不广播这个模式 → device removed → 加载界面直接崩。
    ///
    /// 所以：启动器在传 `-fullscreen` 之前先读一眼游戏自己的 settings.json，
    /// 发现「存的分辨率不可能是合法全屏模式」就**不传 `-fullscreen`**，
    /// 并把原因交给上层提示玩家，让游戏按它自己的窗口模式起来（不会崩）。
    /// </summary>
    public static class DisplaySafetyProbe
    {
        /// <summary>体检结论。</summary>
        public sealed class Result
        {
            /// <summary>允许传 <c>-fullscreen</c>。</summary>
            public bool SafeToForceFullscreen { get; set; } = true;

            /// <summary>给玩家看的一句话原因（不安全时才有）。</summary>
            public string Reason { get; set; } = "";

            /// <summary>读到的原始值，便于诊断包留痕。</summary>
            public string DisplayMode { get; set; } = "";
            public string Resolution { get; set; } = "";
            public string RefreshRate { get; set; } = "";
            public string Gpu { get; set; } = "";

            /// <summary>游戏声明的合法全屏分辨率列表（"1920 1080" 这种）。</summary>
            public List<string> ValidFullscreenResolutions { get; } = new();

            /// <summary>validModes.json 里该 GPU 支持的显示模式（"1920 1080 60" 这种）。</summary>
            public List<string> MonitorModes { get; } = new();
        }

        /// <summary>
        /// 跑体检。读不到 settings.json 时一律返回「安全」—— 不因为读不到文件
        /// 就擅自改变玩家选的行为。
        /// </summary>
        public static Result Inspect(AppSettings settings) => InspectAt(null);

        /// <summary>
        /// 指定 userpath 根做体检（<paramref name="userPathRootOverride"/> 为空则走真实 userpath）。
        /// 之所以留这个口子：回归测试要用「内测者那台机器」的 settings.json 形态跑判定，
        /// 不能依赖跑测试这台机器自己的配置。
        /// </summary>
        public static Result InspectAt(string? userPathRootOverride)
        {
            var result = new Result();
            try
            {
                string userPathRoot = string.IsNullOrWhiteSpace(userPathRootOverride)
                    ? AppSettings.ResolveGameUserPathRoot()
                    : userPathRootOverride!.Trim();

                string json = Path.Combine(userPathRoot, "settings", "settings.json");
                if (!File.Exists(json)) return result;

                using var doc = JsonDocument.Parse(File.ReadAllText(json), new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return result;

                result.DisplayMode = Str(root, "GraphicDisplayModes");
                result.Resolution = Str(root, "GraphicDisplayResolutions");
                result.Gpu = Str(root, "GraphicGPU");
                if (root.TryGetProperty("GraphicDisplayRefreshRates", out var rr) &&
                    rr.ValueKind == JsonValueKind.Number)
                {
                    result.RefreshRate = rr.GetInt32().ToString();
                }

                if (result.Resolution.Length == 0) return result;

                // ① 拿游戏自己承认的合法全屏分辨率本（validModes / drivers 目录）
                CollectValidResolutions(userPathRoot, result);

                // ② 比对：存的分辨率不在合法表里 → 大概率就是那次崩溃的成因
                bool listed = result.ValidFullscreenResolutions.Count == 0
                           || result.ValidFullscreenResolutions.Contains(result.Resolution, StringComparer.Ordinal);

                if (!listed)
                {
                    result.SafeToForceFullscreen = false;
                    result.Reason =
                        "游戏里存的显示分辨率 " + result.Resolution +
                        " 不是任何显示器支持的全屏模式（多半是窗口模式下拖出来的尺寸）。" +
                        "直接强开独占全屏会让显卡切换模式失败并崩溃，所以本次不强制全屏。";
                    return result;
                }

                // ③ 刷新率也得对得上：写过 180Hz 但面板只到 120Hz 同样是崩因
                if (result.RefreshRate.Length > 0 &&
                    result.MonitorModes.Count > 0 &&
                    result.RefreshRate != "0")
                {
                    bool rateOk = result.MonitorModes.Any(m =>
                        m.StartsWith(result.Resolution + " ", StringComparison.Ordinal) &&
                        m.EndsWith(" " + result.RefreshRate, StringComparison.Ordinal));
                    if (!rateOk)
                    {
                        result.SafeToForceFullscreen = false;
                        result.Reason =
                            "游戏里存的刷新率 " + result.RefreshRate + "Hz 与显示器在该分辨率下支持的模式对不上，" +
                            "强开独占全屏会切模式失败并崩溃，所以本次不强制全屏。";
                    }
                }
            }
            catch
            {
                // 读不动就当没问题：绝不能因为体检本身出错而拦住玩家
            }
            return result;
        }

        /// <summary>
        /// 收集该机器上合法的「全屏分辨率」字符串集合（形如 "1920 1080"）。
        ///
        /// 依据来自游戏自己的 validModes 数据；拿不到时就退回「显示器支持的全部模式」，
        /// 再拿不到就留空（空表表示不做这层校验，避免误伤）。
        /// </summary>
        private static void CollectValidResolutions(string userPathRoot, Result result)
        {
            try
            {
                string modesFile = Path.Combine(userPathRoot, "settings", "validModes.json");
                if (!File.Exists(modesFile)) return;

                using var doc = JsonDocument.Parse(File.ReadAllText(modesFile), new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });

                // validModes.json 结构：{ "<gpu>": { "<display>": [ "1920 1080 60", ... ] } }
                // 结构可能随版本变，所以这里做「宽松收集」：把所有形如 "W H R" 的字符串都当模式。
                CollectModeStrings(doc.RootElement, result.MonitorModes, depth: 0);
            }
            catch
            {
            }

            foreach (var m in result.MonitorModes)
            {
                var parts = m.Split(' ');
                if (parts.Length < 2) continue;
                string res = parts[0] + " " + parts[1];
                if (!result.ValidFullscreenResolutions.Contains(res))
                {
                    result.ValidFullscreenResolutions.Add(res);
                }
            }
        }

        /// <summary>递归把所有 "W H [R]" 形状的字符串收进 bag（结构不确定时最稳）。</summary>
        private static void CollectModeStrings(JsonElement el, List<string> bag, int depth)
        {
            if (depth > 8 || bag.Count > 4096) return;

            switch (el.ValueKind)
            {
                case JsonValueKind.String:
                    {
                        string s = (el.GetString() ?? "").Trim();
                        var p = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (p.Length is 2 or 3 &&
                            p[0].Length > 0 && p[1].Length > 0 &&
                            int.TryParse(p[0], out _) && int.TryParse(p[1], out _))
                        {
                            bag.Add(p[0] + " " + p[1] + (p.Length == 3 ? " " + p[2] : ""));
                        }
                        break;
                    }
                case JsonValueKind.Array:
                    foreach (var item in el.EnumerateArray()) CollectModeStrings(item, bag, depth + 1);
                    break;
                case JsonValueKind.Object:
                    foreach (var prop in el.EnumerateObject()) CollectModeStrings(prop.Value, bag, depth + 1);
                    break;
            }
        }

        private static string Str(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var v)) return "";
            return v.ValueKind switch
            {
                JsonValueKind.String => (v.GetString() ?? "").Trim(),
                JsonValueKind.Number => v.ToString(),
                _ => "",
            };
        }
    }
}
