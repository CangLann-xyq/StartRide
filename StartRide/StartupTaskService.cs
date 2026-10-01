using System;
using System.Threading.Tasks;

namespace StartRide.Core
{
    public sealed class StartupTaskResult
    {
        public bool GameDirectoryApplied { get; init; }

        public string GameDirectory { get; init; } = "";

        public VehicleModCheckResult? VehicleCheck { get; init; }

        public string Notice { get; init; } = "";

        public bool HasNotice => Notice.Length > 0;
    }

    public sealed class StartupTaskService
    {
        private readonly AppSettings _settings;

        public StartupTaskService(AppSettings settings) => _settings = settings;

        public async Task<StartupTaskResult> RunAsync()
        {
            bool applied = false;
            string gameDir = _settings.GameDirectory ?? "";

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
                }
            }

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
