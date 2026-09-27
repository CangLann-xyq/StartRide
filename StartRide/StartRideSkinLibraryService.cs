using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Accounts;
using Launcher.Domain.Models;

namespace StartRide.Core
{
    /// <summary>
    /// 自有「皮肤库」实现：StartRide 只做 BeamNG 联机，账户只有 Steam 一种，
    /// 不存在 Minecraft 皮肤/披风这一套。
    ///
    /// 为什么必须顶掉框架实现：框架的 <c>AccountSkinLibraryService</c> 只吃
    /// <c>LauncherPathProvider</c>，目录是它按 <c>ApplicationId</c> + 账户数据目录现拼的
    /// （会拼出带框架命名的 &lt;根&gt;\&lt;框架名&gt;\accounts\microsoft\{avatars,skins,capes} 之类），
    /// 那些目录我们根本不用，却会在磁盘上留下框架命名。这里全部返回空集合，
    /// 于是那套目录再也不会被创建。
    /// </summary>
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
