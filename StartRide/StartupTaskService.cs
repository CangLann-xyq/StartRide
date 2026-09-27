using System;
using System.Threading.Tasks;

namespace StartRide.Core
{
    /// <summary>启动时自检的结果。</summary>
    public sealed class StartupTaskResult
    {
        /// <summary>是否自动修正/发现了游戏目录。</summary>
        public bool GameDirectoryApplied { get; init; }

        public string GameDirectory { get; init; } = "";

        /// <summary>车辆模组检查结果（没开启则为 null）。</summary>
        public VehicleModCheckResult? VehicleCheck { get; init; }

        /// <summary>需要提示给用户的一句话（空串 = 一切正常，不用打扰）。</summary>
        public string Notice { get; init; } = "";

        public bool HasNotice => Notice.Length > 0;
    }

    /// <summary>
    /// 启动器启动时的自检。
    ///
    /// 对应设置页「启动器启动时」那三个开关 —— 它们以前只存在配置里、
    /// 没有任何代码读，勾不勾都一样。这里把它们真正接上：
    ///   · 自动探测游戏安装目录（AutoDetectGame）
    ///   · 检查车辆模组完整性（AutoCheckVehicleMods）
    /// （检查启动器更新由 App 的启动流程负责，同样受 AutoUpdate 开关控制）
    /// </summary>
    public sealed class StartupTaskService
    {
        private readonly AppSettings _settings;

        public StartupTaskService(AppSettings settings) => _settings = settings;

        public async Task<StartupTaskResult> RunAsync()
        {
            bool applied = false;
            string gameDir = _settings.GameDirectory ?? "";

            // 1) 自动探测游戏目录
            if (_settings.AutoDetectGame)
            {
                try
                {
                    if (!AppSettings.IsBeamNgInstall(gameDir))
                    {
                        var candidates = await Task.Run(() => AppSettings.DetectGameDirectoryCandidates()).ConfigureAwait(false);
                        if (candidates.Count > 0)
                        {
                            gameDir = candidates[0];
                            _settings.GameDirectory = gameDir;
                            _settings.Save();
                            applied = true;
                        }
                    }
                }
                catch
                {
                    // 探测失败不影响启动
                }
            }

            // 2) 检查车辆模组
            VehicleModCheckResult? check = null;
            if (_settings.AutoCheckVehicleMods)
            {
                try
                {
                    var service = new VehicleModCheckService(_settings);
                    check = await Task.Run(() => service.Check()).ConfigureAwait(false);
                }
                catch
                {
                    // 检查失败不影响启动
                }
            }

            string notice = "";
            if (applied)
            {
                notice = "已自动识别到 BeamNG.drive：" + gameDir;
            }
            if (check is { HasIssues: true })
            {
                string head = "发现 " + check.Issues.Count + " 个车辆包有问题（" +
                              VehicleModCheckService.Summarize(check) + "）";
                notice = notice.Length > 0 ? notice + "\n" + head : head;
            }

            return new StartupTaskResult
            {
                GameDirectoryApplied = applied,
                GameDirectory = gameDir,
                VehicleCheck = check,
                Notice = notice,
            };
        }
    }
}
