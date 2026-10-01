using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Accounts;
using Launcher.Domain.Models;

namespace StartRide.Core
{

    public sealed class StartRideSkinLibraryService : IAccountSkinLibraryService
    {
        private static readonly IReadOnlyList<LauncherSkinRecord> Empty = Array.Empty<LauncherSkinRecord>();

        public StartRideSkinLibraryService()
        {
            Serilog.Log.Information("StartRide: StartRideSkinLibraryService 已构造（自有皮肤库生效）");
        }

        public IReadOnlyList<LauncherSkinRecord> GetAvailableSkins(LauncherAccount account) => Empty;

        public IReadOnlyList<LauncherSkinRecord> GetSharedSkins() => Empty;

        public Task MigrateLegacySkinsAsync(IReadOnlyList<LauncherAccount> accounts, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyDictionary<string, LauncherSkinRecord>> SyncMicrosoftAccountSkinsAsync(
            IReadOnlyList<LauncherAccount> accounts, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, LauncherSkinRecord>>(
                new Dictionary<string, LauncherSkinRecord>(StringComparer.OrdinalIgnoreCase));

        public Task<LauncherSkinRecord> ImportSkinAsync(LauncherAccount account, string skinFilePath,
            MinecraftSkinModel skinModel, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("StartRide 不使用 Minecraft 皮肤。");

        public Task<string> CreateAvatarSourceAsync(LauncherAccount account, LauncherSkinRecord skin,
            CancellationToken cancellationToken = default)
            => Task.FromResult<string>(null);

        public Task DeleteSkinAsync(LauncherAccount account, LauncherSkinRecord skin,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
