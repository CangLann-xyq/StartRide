// -----------------------------------------------------------------------------
// StartRide · 联机玩法规则键清单
// -----------------------------------------------------------------------------
// 这份名单必须与 Mods/startrideMode.lua 里的 RULE_DEFS 一一对应。
// 启动器这边只维护"键"，不维护"文案"—— 规则的中文说明由游戏内 HUD 负责显示，
// 这里的作用是两件事：
//   ① 解析"规则逐条覆写"输入（key=1 / key=0）时做白名单校验，
//      拼错的键当场报出来，而不是静默写进 multiplayer.json 再无声失效；
//   ② 写配置时只为认识的键生成字段，避免把垃圾键塞进游戏的 modeRules。
// 增删规则时：Lua 的 RULE_DEFS、Lua 的 readConfig 白名单、这里的 Keys 三处同改。
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace StartRide.Core
{
    /// <summary>联机玩法规则的键清单与覆写解析。</summary>
    public static class ModeRuleCatalog
    {
        /// <summary>全部规则键（29 条），顺序与游戏内 HUD 的分类顺序一致。</summary>
        public static readonly string[] Keys =
        {
            // ① 位置 / 场地
            "zoneEliminate", "noMapTeleport", "noHomeAnchor", "noDropAtCam",
            // ② 时间 / 节奏
            "neutralLock", "freezeStaging", "noPause", "noSlowMotion", "roundLimit",
            // ③ 车辆 / 装备
            "noVehicleSwap", "noPartsSwap", "noRepair", "noTrailer",
            "vehicleEnforce", "sameModel",
            // ④ 视野 / 信息
            "noFreeCam", "noPhotoMode", "noWalkMode", "noMinimap",
            // ⑤ 功能 / 作弊
            "noManualReset", "noFun", "noAiTraffic", "noAiMode",
            "noNodeGrabber", "noWorldEditor", "noSpawnVehicle",
            "noRadialSandbox", "noDebugMenu", "noWeather",
        };

        private static readonly HashSet<string> KeySet =
            new HashSet<string>(Keys, StringComparer.OrdinalIgnoreCase);

        /// <summary>这个键是不是已知规则（大小写不敏感）。</summary>
        public static bool IsKnown(string? key) =>
            !string.IsNullOrWhiteSpace(key) && KeySet.Contains(key.Trim());

        /// <summary>
        /// 解析覆写文本：<c>noNodeGrabber=1, noWeather=0</c>。
        /// 返回可写入配置的键值对，以及无法识别的片段（用于给用户即时反馈）。
        /// <para>
        /// 规则值和游戏里一致：1 = 开限制，0 = 关限制。
        /// 非法值同样归入 unknown，不做"猜一个值"这种事 —— 静默猜错比报错更难查。
        /// </para>
        /// </summary>
        public static (Dictionary<string, int> Values, List<string> Unknown) Parse(string? text)
        {
            var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var unknown = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return (values, unknown);

            foreach (string raw in text!.Split(new[] { ',', ';', '\n', '\r' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string item = raw.Trim();
                if (item.Length == 0) continue;

                int eq = item.IndexOf('=');
                if (eq <= 0 || eq == item.Length - 1)
                {
                    unknown.Add(item);
                    continue;
                }

                string key = item.Substring(0, eq).Trim();
                string val = item.Substring(eq + 1).Trim();

                if (!IsKnown(key))
                {
                    unknown.Add(item);
                    continue;
                }

                if (val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    val.Equals("on", StringComparison.OrdinalIgnoreCase))
                {
                    values[key] = 1;
                }
                else if (val == "0" || val.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                         val.Equals("off", StringComparison.OrdinalIgnoreCase))
                {
                    values[key] = 0;
                }
                else
                {
                    unknown.Add(item);
                }
            }

            return (values, unknown);
        }

        /// <summary>合法的严格度预设值（空串 = 跟随各模式自己的口径）。</summary>
        public static bool IsKnownPreset(string? preset)
        {
            if (string.IsNullOrWhiteSpace(preset)) return true;
            string p = preset.Trim().ToLowerInvariant();
            return p == "loose" || p == "standard" || p == "strict" || p == "off";
        }
    }
}
