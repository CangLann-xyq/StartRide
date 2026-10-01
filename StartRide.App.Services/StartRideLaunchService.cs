using System;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Accounts;
using Launcher.Application.Services;
using StartRide.App.Resources;
using Launcher.Domain.Models;
using StartRide.Core;
using GameInstance = Launcher.Domain.Models.GameInstance;

namespace StartRide.App.Services;

public sealed class StartRideLaunchService : ILaunchService
{

	private static GameLauncher? activeLauncher;

	private static readonly object gate = new();

	public async Task<GameLaunchSession> LaunchAsync(
		GameInstance instance,
		LauncherAccount account,
		LauncherSettings settings,
		IProgress<LauncherProgress>? progress,
		LaunchRequestOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var appSettings = AppSettings.Load();

		string? nickname = account?.DisplayName;
		if (!string.IsNullOrWhiteSpace(nickname) && nickname != appSettings.PlayerName)
		{
			appSettings.PlayerName = nickname!.Trim();
			appSettings.EnsureAccounts();
			appSettings.Save();
		}

		progress?.Report(new LauncherProgress(
			LaunchProgressStages.CheckingInstance, "正在准备 BeamNG.drive…", 5));

		try
		{
			HighlightStore.PushModConfig(appSettings);
		}
		catch
		{
		}

		if (appSettings.AutoRepairGameConfig)
		{
			progress?.Report(new LauncherProgress(
				LaunchProgressStages.CheckingInstance, "正在检查游戏必需配置文件…", 12));
			try
			{
				var repair = new ConfigRepairService(appSettings);
				var result = await Task.Run(() => repair.RepairAsync(new ApiService(), null), cancellationToken)
					.ConfigureAwait(false);
				if (result.Repaired > 0)
				{
					progress?.Report(new LauncherProgress(
						LaunchProgressStages.CheckingInstance,
						string.Format(Strings.Settings_GameConfigStartupRepairedFormat, result.Repaired), 18));
				}
			}
			catch (Exception)
			{
			}
		}

		if (appSettings.CheckFilesBeforeLaunch)
		{
			try
			{
				string? brokenNames = null;
				foreach (var item in new GameFileHealthService(appSettings).Inspect())
				{
					if (item.State == GameFileState.NeedsSteam)
					{
						brokenNames = brokenNames == null ? item.DisplayName : brokenNames + "、" + item.DisplayName;
					}
				}
				if (brokenNames != null)
				{
					progress?.Report(new LauncherProgress(
						LaunchProgressStages.CheckingInstance,
						"游戏文件不完整（" + brokenNames + "）：请在 Steam 里验证游戏文件完整性", 18));
				}
			}
			catch (Exception)
			{
			}
		}

		var launcher = new GameLauncher(appSettings);
		if (!launcher.IsInstalled)
		{
			throw new InvalidOperationException(
				"找不到 BeamNG.drive.exe。请先在「全局设置 → 通用」里指定 BeamNG.drive 安装目录。");
		}

		var exitSource = new TaskCompletionSource<LaunchExitResult>(
			TaskCreationOptions.RunContinuationsAsynchronously);

		launcher.RunningChanged += running =>
		{
			if (!running)
			{
				exitSource.TrySetResult(LaunchExitResult.Success);
			}
		};

		if (appSettings.PreInstallMod)
		{
			progress?.Report(new LauncherProgress(
				LaunchProgressStages.RunningPreLaunchCommand, "正在安装联机模组…", 45));
		}

		progress?.Report(new LauncherProgress(
			LaunchProgressStages.RunningPreLaunchCommand, "正在启动 BeamNG.drive…", 85));

		string? error;
		lock (gate)
		{
			activeLauncher = launcher;
			error = launcher.Launch(withMod: appSettings.PreInstallMod);
		}

		if (error != null)
		{
			throw new InvalidOperationException(error);
		}

		progress?.Report(new LauncherProgress(
			LaunchProgressStages.RunningPreLaunchCommand, "BeamNG.drive 已启动", 100));

		PlaytimeTracker.BeginSession(appSettings);

		await Task.CompletedTask.ConfigureAwait(false);

		return new GameLaunchSession(instance?.Id ?? StartRideInstanceService.InstanceId,
			instance?.Name ?? "BeamNG.drive", exitSource.Task);
	}
}
