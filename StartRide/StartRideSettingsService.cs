using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Launcher.Infrastructure.Persistence;

namespace StartRide.Core
{

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

            settings.ExcludedMinecraftDirectories = new List<string>(StartRidePaths.LegacyDirectories());
        }
    }
}
