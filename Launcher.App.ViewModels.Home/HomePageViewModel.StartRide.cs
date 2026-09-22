using System;
using System.Threading;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using StartRide.Core;

namespace Launcher.App.ViewModels.Home;

/// <summary>
/// 首页「游戏运行状态 + 游玩统计」那一行（partial 扩展，不动反编译文件里的启动逻辑）。
///
/// 启动游戏按钮下面本来只有版本号；现在多一行：
///   游戏在跑 → "游戏运行中 · 已运行 12 分钟" + 「结束游戏」按钮
///   没在跑   → "累计游玩 3 小时 5 分钟 · 启动 12 次"
/// 数据来自 AppSettings（PlaytimeTracker 维护）+ 进程探测，不联网。
/// </summary>
public sealed partial class HomePageViewModel
{
	/// <summary>孤儿会话恢复只做一次（多个首页 VM 实例共享）。</summary>
	private static int orphanRecoveryDone;

	private Timer? gameRuntimeTimer;

	private bool isGameRunning;

	private string gameRuntimeSummary = string.Empty;

	private string playtimeSummary = string.Empty;

	private bool showGameRuntimeLine;

	private bool endGameArmed;

	private RelayCommand? endGameCommand;

	/// <summary>BeamNG.drive 进程是否在跑。</summary>
	public bool IsGameRunning
	{
		get => isGameRunning;
		private set
		{
			if (isGameRunning != value)
			{
				isGameRunning = value;
				OnPropertyChanged("IsGameRunning");
			}
		}
	}

	/// <summary>运行中的提示文字（"游戏运行中 · 已运行 X"）；没在跑时为空。</summary>
	public string GameRuntimeSummary
	{
		get => gameRuntimeSummary;
		private set
		{
			if (gameRuntimeSummary != value)
			{
				gameRuntimeSummary = value;
				OnPropertyChanged("GameRuntimeSummary");
			}
		}
	}

	/// <summary>累计游玩统计文字；从来没启动过时为空。</summary>
	public string PlaytimeSummary
	{
		get => playtimeSummary;
		private set
		{
			if (playtimeSummary != value)
			{
				playtimeSummary = value;
				OnPropertyChanged("PlaytimeSummary");
			}
		}
	}

	/// <summary>这一行要不要显示（有运行状态或有统计才显示）。</summary>
	public bool ShowGameRuntimeLine
	{
		get => showGameRuntimeLine;
		private set
		{
			if (showGameRuntimeLine != value)
			{
				showGameRuntimeLine = value;
				OnPropertyChanged("ShowGameRuntimeLine");
			}
		}
	}

	/// <summary>「结束游戏」按钮文字：第一下变成"再点一次结束"，防误触。</summary>
	public string EndGameButtonText => endGameArmed ? Strings.Home_EndGameConfirmButton : Strings.Home_EndGameButton;

	[System.CodeDom.Compiler.GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand EndGameCommand =>
		endGameCommand ?? (endGameCommand = new RelayCommand(EndGame));

	/// <summary>
	/// 首页 VM 构造时调用一次：补齐上次没结算的会话、把状态刷出来、起一个轻量轮询。
	/// 定时器回调在线程池线程上，更新属性前一律走 uiDispatcher 回 UI 线程
	/// （本项目事件回 UI 线程是硬性要求，见 IUiDispatcher 说明）。
	/// </summary>
	private void InitializeGameRuntime()
	{
		if (Interlocked.Exchange(ref orphanRecoveryDone, 1) == 0)
		{
			try
			{
				// 启动器上次被直接关掉时，会话还挂着 → 用游戏日志最后写入时间估算收尾
				PlaytimeTracker.RecoverOrphanSession(AppSettings.Current);
			}
			catch
			{
				// 统计失败不影响首页
			}
		}

		RefreshGameRuntimeState();
		try
		{
			gameRuntimeTimer = new Timer(OnGameRuntimeTick, null, 4000, 4000);
		}
		catch
		{
			// 定时器建不起来就退化成"进首页时刷一次"
		}
	}

	private void OnGameRuntimeTick(object? state)
	{
		try
		{
			bool running = GameRuntimeService.IsRunning();
			// 没在跑且状态没变（本来就没跑）：不用惊动 UI
			if (!running && !isGameRunning && !endGameArmed)
			{
				return;
			}
			if (uiDispatcher.HasAccess)
			{
				RefreshGameRuntimeState();
			}
			else
			{
				uiDispatcher.Post(RefreshGameRuntimeState);
			}
		}
		catch
		{
			// 轮询异常忽略（下一拍继续）
		}
	}

	/// <summary>把真实状态刷到界面。必须在 UI 线程调用。</summary>
	public void RefreshGameRuntimeState()
	{
		try
		{
			var app = AppSettings.Current;
			bool processRunning = GameRuntimeService.IsRunning();
			DateTimeOffset? start = PlaytimeTracker.TryGetSessionStart(app) ?? GameRuntimeService.TryGetStartTime();

			if (processRunning)
			{
				IsGameRunning = true;
				var length = start.HasValue ? DateTimeOffset.Now - start.Value : (TimeSpan?)null;
				GameRuntimeSummary = length.HasValue && length.Value >= TimeSpan.Zero
					? string.Format(Strings.Home_GameRunningFormat, PlaytimeTracker.FormatDuration(length.Value))
					: Strings.Home_GameRunningOnly;
			}
			else
			{
				IsGameRunning = false;
				GameRuntimeSummary = string.Empty;
				if (endGameArmed)
				{
					endGameArmed = false;
					OnPropertyChanged("EndGameButtonText");
				}
			}

			PlaytimeSummary = app.LaunchCount > 0
				? string.Format(
					Strings.Home_PlaytimeSummaryFormat,
					PlaytimeTracker.FormatDuration(PlaytimeTracker.GetTotalPlaytime(app)),
					app.LaunchCount)
				: string.Empty;

			ShowGameRuntimeLine = IsGameRunning || PlaytimeSummary.Length > 0;

			// 托盘提示跟着游戏状态走，鼠标悬停就能看出游戏在不在跑
			UpdateTrayTooltip();
		}
		catch
		{
			// 刷新失败保持上一次显示
		}
	}

	/// <summary>把「游戏在跑 / 没在跑」同步到托盘图标的悬停文字。</summary>
	private void UpdateTrayTooltip()
	{
		try
		{
			if (System.Windows.Application.Current?.MainWindow is Launcher.App.Views.Shell.MainWindow window)
			{
				window.StartRideTrayUpdateTooltip(IsGameRunning
					? "StartRide 启动器 · BeamNG.drive 正在运行"
					: "StartRide 启动器");
			}
		}
		catch
		{
			// 托盘不在（比如被用户关了）就不用管
		}
	}

	/// <summary>结束游戏（第一次点=进入待确认，第二次点=真的结束）。</summary>
	private void EndGame()
	{
		bool running = false;
		try
		{
			running = GameRuntimeService.IsRunning();
		}
		catch
		{
			// 探测失败按"没在跑"处理，下面走兜底分支
		}

		if (running && !endGameArmed)
		{
			endGameArmed = true;
			OnPropertyChanged("EndGameButtonText");
			statusService.Report(Strings.Settings_RuntimeEndGameWarning);
			return;
		}

		endGameArmed = false;
		OnPropertyChanged("EndGameButtonText");

		try
		{
			int ended = GameRuntimeService.EndGame();
			// 进程结束了但会话还开着 → 立刻结算，别把这段时间算漏
			PlaytimeTracker.EndSession(AppSettings.Current);
			statusService.Report(ended > 0
				? string.Format(Strings.Settings_RuntimeEndedFormat, ended)
				: Strings.Settings_RuntimeNothingToEnd);
		}
		catch (Exception ex)
		{
			statusService.Report(string.Format(Strings.Settings_RuntimeEndFailedFormat, ex.Message));
		}
		finally
		{
			RefreshGameRuntimeState();
		}
	}
}
