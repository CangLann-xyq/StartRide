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

/// <summary>
/// 用真实启动 BeamNG.drive 顶替原启动器的 Minecraft 启动器。
///
/// 主页「启动游戏」按钮 → 这里：
///   1. 把界面选中的账户昵称同步成联机昵称
///   2. 按设置决定是否重新安装联机模组
///   3. 拉起 BeamNG.drive.exe（带 -userpath，保证和启动器装的模组同目录）
///   4. 返回一个 GameLaunchSession，游戏退出时它的 ExitTask 完成
///
/// 界面上的 Minecraft 专属环节（Java 版本检查、文件校验修复、正版会话续期）都不参与。
/// </summary>
public sealed class StartRideLaunchService : ILaunchService
{
	/// <summary>
	/// 持有正在运行的启动器实例：GameLauncher 的退出回调挂在 Process 上，
	/// 如果这里不保存引用，GC 会连事件一起收走，界面就永远停在"启动中"。
	/// </summary>
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

		// 界面账户昵称 → 联机昵称
		string? nickname = account?.DisplayName;
		if (!string.IsNullOrWhiteSpace(nickname) && nickname != appSettings.PlayerName)
		{
			appSettings.PlayerName = nickname!.Trim();
			appSettings.EnsureAccounts();
			appSettings.Save();
		}

		progress?.Report(new LauncherProgress(
			LaunchProgressStages.CheckingInstance, "正在准备 BeamNG.drive…", 5));

		// 必需配置文件先查一遍：丢了/坏了游戏会起不来，这一步能直接补回来
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
				// 配置检查失败不阻断启动（离线也要能进游戏）
			}
		}

		// 游戏本体文件体检（纯本地、不联网）：缺了就说清楚，别等用户点了启动再猜。
		// 受「启动前检查游戏文件」开关控制 —— 这个开关以前只存不读，关掉也没用。
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
				// 体检失败不阻断启动
			}
		}

		var launcher = new GameLauncher(appSettings);
		if (!launcher.IsInstalled)
		{
			throw new InvalidOperationException(
				"找不到 BeamNG.drive.exe。请先在「全局设置 → 通用」里指定 BeamNG.drive 安装目录。");
		}

		// 退出信号：RunningChanged(false) 在游戏进程结束时触发
		var exitSource = new TaskCompletionSource<LaunchExitResult>(
			TaskCreationOptions.RunContinuationsAsynchronously);

		launcher.RunningChanged += running =>
		{
			if (!running)
			{
				exitSource.TrySetResult(LaunchExitResult.Success);
			}
		};

		if (appSettings.AutoInstallMod)
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
			error = launcher.Launch(withMod: appSettings.AutoInstallMod);
		}

		if (error != null)
		{
			throw new InvalidOperationException(error);
		}

		progress?.Report(new LauncherProgress(
			LaunchProgressStages.RunningPreLaunchCommand, "BeamNG.drive 已启动", 100));

		// 游玩统计：从这一刻开始计时，游戏退出时在 RunningChanged(false) 里结算
		PlaytimeTracker.BeginSession(appSettings);

		await Task.CompletedTask.ConfigureAwait(false);

		return new GameLaunchSession(instance?.Id ?? StartRideInstanceService.InstanceId,
			instance?.Name ?? "BeamNG.drive", exitSource.Task);
	}
}
