using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
    public enum GameFileState
    {
        Unknown = 0,
        Ok,
        Missing,
        Fixed,
        NeedsSteam,
        NotApplicable,
        Failed,
    }

    public sealed class GameFileHealthItem
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public string Detail { get; set; } = "";
        public GameFileState State { get; set; } = GameFileState.Unknown;
        public bool Fixable { get; set; }
        public bool IsProblem => State != GameFileState.Ok && State != GameFileState.Fixed;
    }

    public sealed class GameFileHealthResult
    {
        public int Checked { get; set; }
        public int Fixed { get; set; }
        public int NeedsSteam { get; set; }
        public int Missing { get; set; }
        public List<ConfigFileStatus> ConfigFiles { get; } = new List<ConfigFileStatus>();
        public int ConfigRepaired { get; set; }
        public int ConfigChecked { get; set; }
        public bool ServerReachable { get; set; }
        public string? Error { get; set; }
        public List<GameFileHealthItem> Items { get; } = new List<GameFileHealthItem>();

        public bool HasProblem => Missing > 0 || NeedsSteam > 0;
    }

    public sealed class GameFileHealthService
    {
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

        public static string ModSourceDirectory => Path.Combine(AppContext.BaseDirectory, "Mods");

        public static string SteamVerifyHint =>
            "在 Steam 库中右键 BeamNG.drive → 属性 → 已安装文件 → 验证游戏文件的完整性";

        public List<GameFileHealthItem> Inspect()
        {
            var items = new List<GameFileHealthItem>();
            string gameDir = settings.GameDirectory ?? string.Empty;

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

            string? userRoot = null;
            try
            {
                userRoot = settings.ResolveUserDataRoot();
            }
            catch
            {
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

            items.AddRange(InspectModPackage());

            return items;
        }

        private IEnumerable<GameFileHealthItem> InspectModPackage()
        {
            var installer = new ModInstaller(settings);
            string sourceGe = Path.Combine(ModSourceDirectory, "startride_mod.lua");
            string sourceVe = Path.Combine(ModSourceDirectory, "startrideVE.lua");

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

            bool installed = installer.IsInstalled;
            bool onDemand = settings.RemoveModOnLeave;
            bool stale = installed && !installer.IsUpToDate;

            yield return new GameFileHealthItem
            {
                Key = "mod.installed",
                DisplayName = "联机模组包",
                RelativePath = installer.InstalledPackagePath,
                State = installed
                    ? (stale ? GameFileState.Missing : GameFileState.Ok)
                    : (onDemand ? GameFileState.Ok : GameFileState.Missing),
                Fixable = sourceOk,
                Detail = !installed
                    ? (onDemand
                        ? "按需安装：不在联机状态时不占用 mods 目录，进入房间会自动装上。"
                        : "未安装：点「检查并补全」自动打包安装（联机必需）。")
                    : (stale
                        ? "内容与启动器自带的模组不一致：点「检查并补全」会重新打包安装。"
                        : "正常"),
            };
        }

        public async Task<GameFileHealthResult> RepairAsync(
            ApiService api, Action<string>? log = null, CancellationToken cancellationToken = default)
        {
            var result = new GameFileHealthResult();
            var items = Inspect();
            result.Checked = items.Count;

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
