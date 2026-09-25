using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Launcher.Infrastructure.Persistence;

namespace StartRide.Core
{
    /// <summary>
    /// 自有设置服务：包住框架的 <see cref="JsonSettingsService"/>，把「配置放在哪、游戏目录叫什么」
    /// 强制收敛到 <see cref="StartRidePaths"/>。
    ///
    /// 为什么要包一层而不是直接改 JsonSettingsService：
    /// 1) 那个实现无源码（在 Launcher.Infrastructure.dll 里，只能等长替换字符串，改不了逻辑）；
    /// 2) 它本身支持注入 dataDirectory，所以文件落在哪我们能定；
    /// 3) 但设置文件里持久化的 DataDirectory / MinecraftDirectory 是上一版写进去的，
    ///    读出来还是旧值——所以读/写两个方向都要归一化，否则框架又会去建 .minecraft。
    ///
    /// 注册方式见 Launcher.App/App.cs：排在 AddLauncherInfrastructure() 之后（MS.DI 取后注册者）。
    /// </summary>
    public sealed class StartRideSettingsService : ISettingsService
    {
        private const string GameDataDisplayName = "StartRide 数据";

        private readonly JsonSettingsService _inner;

        public StartRideSettingsService(JsonSettingsService inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public async Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            LauncherSettings settings = await _inner.LoadAsync(cancellationToken).ConfigureAwait(false);
            Normalize(settings);
            return settings;
        }

        public async Task<LauncherSettingsLoadResult> LoadWithMetadataAsync(CancellationToken cancellationToken = default)
        {
            LauncherSettingsLoadResult result = await _inner.LoadWithMetadataAsync(cancellationToken).ConfigureAwait(false);
            Normalize(result.Settings);
            return result;
        }

        public Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
        {
            Normalize(settings);
            return _inner.SaveAsync(settings, cancellationToken);
        }

        public Task<LauncherSettings> UpdateAsync(Action<LauncherSettings> update, CancellationToken cancellationToken = default)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            return _inner.UpdateAsync(settings =>
            {
                update(settings);
                Normalize(settings);
            }, cancellationToken);
        }

        /// <summary>
        /// 把设置里所有「目录」字段拉回我们自己的布局。任何写入路径都会经过这里，
        /// 所以框架的默认值（&lt;EXE&gt;\BHL、&lt;EXE&gt;\.minecraft）不会被落盘。
        /// </summary>
        private static void Normalize(LauncherSettings settings)
        {
            if (settings == null) return;

            settings.DataDirectory = StartRidePaths.Root;
            settings.MinecraftDirectory = StartRidePaths.GameData;

            var directories = new List<string> { StartRidePaths.GameData };
            settings.MinecraftDirectories = directories;

            var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [StartRidePaths.GameData] = GameDataDisplayName
            };
            settings.MinecraftDirectoryDisplayNames = displayNames;

            // 老框架时代的目录不再作为候选/排除项出现在界面上
            settings.ExcludedMinecraftDirectories = new List<string>(StartRidePaths.LegacyDirectories());
        }
    }
}
