using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using StartRide.Core;
// StartRide.Core 里也有一个 GameInstance（ApiService 的联机房间模型），
// 与原启动器的实例模型重名。这里用别名钉死，避免 CS0104 歧义。
using GameInstance = Launcher.Domain.Models.GameInstance;

namespace Launcher.App.Services;

/// <summary>
/// 用「本机 BeamNG.drive」这一个合成实例顶替原启动器的 Minecraft 实例扫描。
///
/// 原启动器的主页/游戏设置都以 GameInstance 为中心，而 BeamNG 没有"多版本实例"这回事。
/// 这里返回一个代表当前机器上 BeamNG.drive 安装的实例，于是：
///   主页能显示「BeamNG.drive &lt;版本&gt;」、启动按钮可用、游戏设置页也能挂到它上面。
///
/// 注意所有方法都不抛异常（启动期就会调用），失败一律退化成空结果。
/// </summary>
public sealed class StartRideInstanceService : IGameInstanceService
{
	/// <summary>固定 Id，保证跨启动稳定（界面会按 Id 记住选中项）。</summary>
	public const string InstanceId = "beamng-drive";

	/// <summary>
	/// 本次会话开始时刻。只在真实信息探测不到时兜底用：
	/// 宁可显示"今天"，也不要伪造一个不存在的历史时间。
	/// </summary>
	private static readonly DateTimeOffset SessionStartedUtc = DateTimeOffset.UtcNow;

	private readonly AppSettings settings = AppSettings.Load();

	/// <summary>
	/// 探测真实的安装时间：优先游戏目录本身（用户指定的目录就是 BeamNG 装的地方，
	/// 它的创建时间就是"这台机器上什么时候有的这个游戏"）。探测不到再退到会话开始时刻。
	/// </summary>
	private static DateTimeOffset ResolveInstalledAtUtc(string directory, string executablePath)
	{
		string[] candidates = { directory, SafeDirectoryName(executablePath) };
		foreach (string probe in candidates)
		{
			if (string.IsNullOrWhiteSpace(probe)) continue;
			try
			{
				if (!Directory.Exists(probe)) continue;
				DateTime created = Directory.GetCreationTimeUtc(probe);
				if (created.Year > 1980 && created <= DateTime.UtcNow)
				{
					return new DateTimeOffset(created, TimeSpan.Zero);
				}
			}
			catch (Exception)
			{
				// 目录被删/无权限/网络盘掉线都不该让启动器起不来，继续试下一个。
			}
		}
		return SessionStartedUtc;
	}

	/// <summary>
	/// 探测真实的"最后更新时间"：BeamNG.drive.exe 的最后写入时间就是游戏本体被更新（补丁/换版本）的时刻。
	/// </summary>
	private static DateTimeOffset ResolveUpdatedAtUtc(string executablePath)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
			{
				DateTime written = File.GetLastWriteTimeUtc(executablePath);
				if (written.Year > 1980) return new DateTimeOffset(written, TimeSpan.Zero);
			}
		}
		catch (Exception)
		{
		}
		return DateTimeOffset.UtcNow;
	}

	private static string SafeDirectoryName(string path)
	{
		try
		{
			return path.Length == 0 ? "" : Path.GetDirectoryName(path) ?? "";
		}
		catch (Exception)
		{
			return "";
		}
	}

	/// <summary>
	/// 实例描述：说清"这是本机的哪个游戏"，而不是把安装路径塞进描述框
	/// （路径已经在「安装目录」一节里了，重复一遍只会让人以为是排版错乱）。
	/// </summary>
	private static string BuildDescription(bool installed, string version)
	{
		if (!installed) return "未在本机检测到 BeamNG.drive";
		return version.Length > 0
			? "本机安装 · BeamNG.drive " + version
			: "本机安装 · BeamNG.drive";
	}

	private GameInstance Build()
	{
		var launcher = new GameLauncher(settings);
		string version = launcher.GameVersion;
		string directory = settings.GameDirectory ?? "";
		string executable = launcher.ExecutablePath ?? "";

		bool installed = version.Length > 0 || launcher.IsInstalled;

		return new GameInstance
		{
			Id = InstanceId,
			Name = "BeamNG.drive",
			MinecraftVersion = installed ? version : string.Empty,
			VersionName = installed ? version : string.Empty,
			VersionType = "release",
			Description = BuildDescription(installed, version),
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
			// 都是真实探测值，不再写死：创建时间 = 游戏目录的创建时间，
			// 更新时间 = BeamNG.drive.exe 的最后写入时间（即游戏本体的更新日期）。
			CreatedAt = ResolveInstalledAtUtc(directory, executable),
			UpdatedAt = ResolveUpdatedAtUtc(executable),
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
