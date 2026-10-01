using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using StartRide.Core;

using GameInstance = Launcher.Domain.Models.GameInstance;

namespace StartRide.App.Services;

public sealed class StartRideInstanceService : IGameInstanceService
{
	public const string InstanceId = "beamng-drive";

	private static readonly DateTimeOffset SessionStartedUtc = DateTimeOffset.UtcNow;

	private readonly AppSettings settings = AppSettings.Load();

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
			}
		}
		return SessionStartedUtc;
	}

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

			IconSource = BrandingIcons.BeamNgLogo,
			Loader = LoaderKind.Vanilla,
			InstanceDirectory = directory,
			BackupDirectory = string.Empty,
			MemoryMb = settings.MaxMemoryMB,
			CheckFilesBeforeLaunch = false,
			AutoRepairMissingFiles = false,
			MinimizeLauncherAfterLaunch = settings.MinimizeToTray,
			LaunchFullScreen = false,

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
