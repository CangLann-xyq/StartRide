using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
    /// <summary>单个游戏文件的体检状态。</summary>
    public enum GameFileState
    {
        Unknown = 0,
        /// <summary>存在且可用</summary>
        Ok,
        /// <summary>缺失</summary>
        Missing,
        /// <summary>本次已由启动器补全</summary>
        Fixed,
        /// <summary>缺失但启动器补不了（游戏本体文件，需要走 Steam 校验）</summary>
        NeedsSteam,
        /// <summary>无法判断（游戏目录没设置/不存在）</summary>
        NotApplicable,
        /// <summary>补全过程出错</summary>
        Failed,
    }

    public sealed class GameFileHealthItem
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        /// <summary>相对路径或绝对路径，界面上作为副标题显示</summary>
        public string RelativePath { get; set; } = "";
        public string Detail { get; set; } = "";
        public GameFileState State { get; set; } = GameFileState.Unknown;
        /// <summary>启动器是否能自动补全这一项</summary>
        public bool Fixable { get; set; }
        public bool IsProblem => State != GameFileState.Ok && State != GameFileState.Fixed;
    }

    public sealed class GameFileHealthResult
    {
        public int Checked { get; set; }
        public int Fixed { get; set; }
        public int NeedsSteam { get; set; }
        public int Missing { get; set; }
        /// <summary>配置文件的检查明细（复用 ConfigRepairService 的状态模型）</summary>
        public List<ConfigFileStatus> ConfigFiles { get; } = new List<ConfigFileStatus>();
        public int ConfigRepaired { get; set; }
        public int ConfigChecked { get; set; }
        public bool ServerReachable { get; set; }
        public string? Error { get; set; }
        public List<GameFileHealthItem> Items { get; } = new List<GameFileHealthItem>();

        public bool HasProblem => Missing > 0 || NeedsSteam > 0;
    }

    /// <summary>
    /// 游戏文件体检与补全（"补全整个游戏文件"）。
    ///
    /// 与 ConfigRepairService 的分工：
    ///   - ConfigRepairService 只管 &lt;userpath&gt;/settings 下的**必需配置文件**（能从云端模板补）
    ///   - 本服务管**整个游戏**：游戏本体运行文件 + 用户数据目录 + 联机模组包 + 配置（委托给上面那个）
    ///
    /// 关键原则：**能补的补，补不了的别装能补**。
    ///   游戏本体文件来自 Steam，启动器无法凭空造出来 —— 这类缺失只报"需要在 Steam 里验证完整性"，
    ///   并且给出精确的缺失清单与操作路径，不做假动作。
    /// </summary>
    public sealed class GameFileHealthService
    {
        /// <summary>游戏安装目录下必须存在的运行文件/目录（缺失 = 装不完整，只能 Steam 校验）。</summary>
        private static readonly (string Relative, string DisplayName)[] GameCoreEntries =
        {
            ("BeamNG.drive.exe", "游戏主程序"),
            (Path.Combine("lua", "ge"), "游戏引擎脚本(GE)"),
            (Path.Combine("lua", "vehicle"), "车辆脚本(VE)"),
            (Path.Combine("content", "vehicles"), "车辆内容"),
            (Path.Combine("content", "levels"), "地图内容"),
        };

        private readonly AppSettings settings;

        public GameFileHealthService(AppSettings? settings = null)
        {
            this.settings = settings ?? AppSettings.Current;
        }

        /// <summary>联机模组在启动器侧的源目录（随 EXE 一起分发）。</summary>
        public static string ModSourceDirectory => Path.Combine(AppContext.BaseDirectory, "Mods");

        /// <summary>Steam 校验游戏完整性的操作指引（给用户看的原文）。</summary>
        public static string SteamVerifyHint =>
            "在 Steam 库中右键 BeamNG.drive → 属性 → 已安装文件 → 验证游戏文件的完整性";

        // ------------------------------------------------------------ 检查

        /// <summary>只做本地检查，不联网、不改动任何文件。</summary>
        public List<GameFileHealthItem> Inspect()
        {
            var items = new List<GameFileHealthItem>();
            string gameDir = settings.GameDirectory ?? string.Empty;

            // 1) 游戏本体
            bool gameDirUsable = !string.IsNullOrWhiteSpace(gameDir) && Directory.Exists(gameDir);
            if (!gameDirUsable)
            {
                items.Add(new GameFileHealthItem
                {
                    Key = "game.root",
                    DisplayName = "游戏安装目录",
                    RelativePath = string.IsNullOrWhiteSpace(gameDir) ? "(未设置)" : gameDir,
                    State = GameFileState.NotApplicable,
                    Fixable = false,
                    Detail = "没找到游戏目录：请先点上方「自动识别」，或手动选择 BeamNG.drive 安装目录。",
                });
            }
            else
            {
                foreach (var (relative, displayName) in GameCoreEntries)
                {
                    string full = Path.Combine(gameDir, relative);
                    // 结尾是文件名的看文件，其余看目录
                    bool isFile = Path.GetFileName(relative).IndexOf('.') > 0;
                    bool exists = isFile ? File.Exists(full) : Directory.Exists(full);
                    items.Add(new GameFileHealthItem
                    {
                        Key = "game." + relative.Replace('\\', '/'),
                        DisplayName = displayName,
                        RelativePath = relative,
                        State = exists ? GameFileState.Ok : GameFileState.NeedsSteam,
                        Fixable = false,
                        Detail = exists ? "正常" : "缺失：启动器无法补全游戏本体，请" + SteamVerifyHint,
                    });
                }
            }

            // 2) 用户数据目录（存档/设置/模组都在这里）
            string? userRoot = null;
            try
            {
                userRoot = settings.ResolveUserDataRoot();
            }
            catch
            {
                // 解析失败按未就绪处理
            }
            items.Add(new GameFileHealthItem
            {
                Key = "user.root",
                DisplayName = "用户数据目录",
                RelativePath = string.IsNullOrWhiteSpace(userRoot) ? "(未找到)" : userRoot!,
                State = string.IsNullOrWhiteSpace(userRoot) ? GameFileState.NotApplicable
                    : (Directory.Exists(userRoot!) ? GameFileState.Ok : GameFileState.Missing),
                Fixable = !string.IsNullOrWhiteSpace(userRoot),
                Detail = string.IsNullOrWhiteSpace(userRoot)
                    ? "游戏还没运行过，首次启动游戏后会自动生成。"
                    : (Directory.Exists(userRoot!) ? "正常" : "缺失：启动一次游戏即可生成（或点「检查并补全」尝试创建）。"),
            });

            // 3) 模组目录
            string modsDir = settings.ResolveModsDirectory();
            items.Add(new GameFileHealthItem
            {
                Key = "user.mods",
                DisplayName = "模组目录",
                RelativePath = modsDir,
                State = Directory.Exists(modsDir) ? GameFileState.Ok : GameFileState.Missing,
                Fixable = true,
                Detail = Directory.Exists(modsDir) ? "正常" : "缺失：可自动创建，联机模组需要装在这里。",
            });

            // 4) 联机模组包（随启动器分发，可自动打包补装）
            items.AddRange(InspectModPackage());

            return items;
        }

        private IEnumerable<GameFileHealthItem> InspectModPackage()
        {
            var installer = new ModInstaller(settings);
            string sourceGe = Path.Combine(ModSourceDirectory, "startride_mod.lua");
            string sourceVe = Path.Combine(ModSourceDirectory, "startrideVE.lua");

            // 源文件（启动器自带）
            bool sourceOk = File.Exists(sourceGe) && File.Exists(sourceVe);
            yield return new GameFileHealthItem
            {
                Key = "mod.source",
                DisplayName = "联机模组源文件",
                RelativePath = ModSourceDirectory,
                State = sourceOk ? GameFileState.Ok : GameFileState.Failed,
                Fixable = false,
                Detail = sourceOk
                    ? "正常"
                    : "启动器缺少 Mods 目录下的模组源文件，请重新安装启动器（安装包不完整）。",
            };

            // 已安装的包
            bool installed = installer.IsInstalled;
            bool stale = false;
            if (installed && sourceOk)
            {
                try
                {
                    DateTime newestSource = new[] { File.GetLastWriteTime(sourceGe), File.GetLastWriteTime(sourceVe) }.Max();
                    DateTime installedAt = installer.InstalledAt ?? DateTime.MinValue;
                    stale = newestSource > installedAt.AddSeconds(1);
                }
                catch
                {
                    // 时间戳读不到按不判定过期处理
                }
            }
            yield return new GameFileHealthItem
            {
                Key = "mod.installed",
                DisplayName = "联机模组已安装",
                RelativePath = installer.InstalledPackagePath,
                State = !installed ? GameFileState.Missing : (stale ? GameFileState.Missing : GameFileState.Ok),
                Fixable = sourceOk,
                Detail = !installed
                    ? "未安装：点「检查并补全」自动打包安装（联机必需）。"
                    : (stale ? "版本落后于启动器自带模组：点「检查并补全」会重新打包安装。" : "正常"),
            };
        }

        // ------------------------------------------------------------ 检查 + 补全

        /// <summary>
        /// 检查并按需补全。能补的：必需配置（云端模板）、模组目录、联机模组包。
        /// 游戏本体文件缺失只报需 Steam 校验，不做假动作。
        /// </summary>
        public async Task<GameFileHealthResult> RepairAsync(
            ApiService api, Action<string>? log = null, CancellationToken cancellationToken = default)
        {
            var result = new GameFileHealthResult();
            var items = Inspect();
            result.Checked = items.Count;

            // 1) 模组目录：缺失就建（纯本地动作）
            foreach (var item in items.Where(i => i.Key == "user.mods" && i.State == GameFileState.Missing))
            {
                try
                {
                    Directory.CreateDirectory(item.RelativePath);
                    item.State = GameFileState.Fixed;
                    item.Detail = "已创建模组目录";
                    result.Fixed++;
                    log?.Invoke("已创建模组目录：" + item.RelativePath);
                }
                catch (Exception ex)
                {
                    item.State = GameFileState.Failed;
                    item.Detail = "创建失败：" + ex.Message;
                }
            }

            // 2) 联机模组包：缺失/过期就重新打包安装
            foreach (var item in items.Where(i => i.Key == "mod.installed" && i.State == GameFileState.Missing))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!item.Fixable)
                {
                    continue;
                }
                try
                {
                    string? error = new ModInstaller(settings).Install();
                    if (error == null)
                    {
                        item.State = GameFileState.Fixed;
                        item.Detail = "已重新打包并安装联机模组";
                        result.Fixed++;
                        log?.Invoke("已安装联机模组包：startride.zip");
                    }
                    else
                    {
                        item.State = GameFileState.Failed;
                        item.Detail = "安装失败：" + error;
                    }
                }
                catch (Exception ex)
                {
                    item.State = GameFileState.Failed;
                    item.Detail = "安装失败：" + ex.Message;
                }
            }

            // 3) 必需配置文件：交给 ConfigRepairService（云端模板）
            try
            {
                var configService = new ConfigRepairService(settings);
                var configResult = await configService.RepairAsync(api, log, cancellationToken).ConfigureAwait(false);
                result.ConfigFiles.AddRange(configResult.Files);
                result.ConfigChecked = configResult.Checked;
                result.ConfigRepaired = configResult.Repaired;
                result.ServerReachable = configResult.ServerReachable;
                result.Error = configResult.Error;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Error = "配置检查失败：" + ex.Message;
            }

            result.Items.AddRange(items);
            result.Missing = items.Count(i => i.State == GameFileState.Missing);
            result.NeedsSteam = items.Count(i => i.State == GameFileState.NeedsSteam);
            return result;
        }
    }
}
