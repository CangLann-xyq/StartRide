using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using StartRide.Core;
// StartRide.Core 里也有一个 GameInstance（ApiService 的联机房间模型），
// 与 StartRide 的实例模型重名。这里用别名钉死，避免 CS0104 歧义。
using GameInstance = Launcher.Domain.Models.GameInstance;

namespace Launcher.App.Services;

/// <summary>
/// 用「本机 BeamNG.drive」这一个合成实例顶替 StartRide 的 Minecraft 实例扫描。
///
/// StartRide 的主页/游戏设置都以 GameInstance 为中心，而 BeamNG 没有"多版本实例"这回事。
/// 这里返回一个代表当前机器上 BeamNG.drive 安装的实例，于是：
///   主页能显示「BeamNG.drive &lt;版本&gt;」、启动按钮可用、游戏设置页也能挂到它上面。
///
/// 注意所有方法都不抛异常（启动期就会调用），失败一律退化成空结果。
/// </summary>
public sealed class StartRideInstanceService : IGameInstanceService
{
	/// <summary>固定 Id，保证跨启动稳定（界面会按 Id 记住选中项）。</summary>
	public const string InstanceId = "beamng-drive";

	private readonly AppSettings settings = AppSettings.Load();

	private GameInstance Build()
	{
		var launcher = new GameLauncher(settings);
		string version = launcher.GameVersion;
		string directory = settings.GameDirectory ?? "";

		bool installed = version.Length > 0 || launcher.IsInstalled;

		return new GameInstance
		{
			Id = InstanceId,
			Name = "BeamNG.drive",
			MinecraftVersion = installed ? version : string.Empty,
			VersionName = installed ? version : string.Empty,
			VersionType = "release",
			Description = directory,
			// 必须是带程序集前缀的绝对 pack URI：写成 "assets/..." 相对路径时
			// IconSourceImageLoader 解析不出资源，首页启动卡片/实例列表的图标位就是空的。
			IconSource = BrandingIcons.BeamNgLogo,
			Loader = LoaderKind.Vanilla,
			InstanceDirectory = directory,
			BackupDirectory = string.Empty,
			MemoryMb = settings.MaxMemoryMB,
			// BeamNG 不走 Minecraft 的文件校验/修复流程，关掉避免它去扫不存在的文件。
			CheckFilesBeforeLaunch = false,
			AutoRepairMissingFiles = false,
			MinimizeLauncherAfterLaunch = settings.MinimizeToTray,
			LaunchFullScreen = false,
			CreatedAt = DateTimeOffset.UtcNow.AddYears(-1),
			UpdatedAt = DateTimeOffset.UtcNow,
		};
	}

	private IReadOnlyList<GameInstance> All() => new[] { Build() };

	public Task<IReadOnlyList<GameInstance>> GetStoredInstancesAsync(
		LauncherSettings settings, CancellationToken cancellationToken = default)
		=> Task.FromResult(All());

	public Task<IReadOnlyList<GameInstance>> GetInstancesAsync(CancellationToken cancellationToken = default)
		=> Task.FromResult(All());

	public Task<GameInstance?> GetDefaultInstanceAsync(CancellationToken cancellationToken = default)
		=> Task.FromResult<GameInstance?>(Build());

	public Task SaveInstanceAsync(GameInstance instance, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	public Task<GameInstance> RenameInstanceAsync(
		string instanceId, string? newName, string? newIconSource, CancellationToken cancellationToken = default)
		=> Task.FromResult(Build());

	public Task<bool> SetDefaultInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
		=> Task.FromResult(true);

	public Task<bool> DeleteInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
		=> Task.FromResult(false);

	/// <summary>
	/// StartRide 不做"下载并创建 Minecraft 实例"。被调用时给出明确原因，
	/// 而不是静默返回一个空实例让界面显示成"创建成功"。
	/// </summary>
	public Task<GameInstance> CreateInstanceAsync(
		string minecraftVersion,
		LoaderKind loader,
		string? loaderVersion,
		string? name,
		IProgress<LauncherProgress>? progress,
		CancellationToken cancellationToken = default,
		DownloadSourcePreference downloadSourcePreference = DownloadSourcePreference.Official,
		int downloadSpeedLimitMbPerSecond = 0,
		bool installFabricApi = true,
		string? fabricApiVersionId = null,
		string? quiltStandardLibraryVersionId = null)
		=> throw new NotSupportedException(
			"StartRide 不通过本启动器下载游戏。请先在「全局设置」里指定 BeamNG.drive 安装目录。");
}
