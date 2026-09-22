using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Microsoft.Extensions.Logging;
using StartRide.Core;

namespace Launcher.App.ViewModels.Settings;

/// <summary>
/// 通用设置页里 StartRide 的「运行状态」与「诊断包」两块（partial 扩展）。
///
/// 运行状态：游戏在不在跑、跑了多久、一共玩了多少；残留进程可以一键结束
/// （游戏崩溃后常见"已经在运行"假象，用不着去任务管理器找进程）。
/// 诊断包：日志 + 设置 + 体检结果 + 环境信息打成 zip，出问题直接发出去。
/// </summary>
public sealed partial class GeneralSettingsViewModel
{
	private bool isGameRunning;

	private string gameRuntimeStatusText = string.Empty;

	private string playtimeSummaryText = string.Empty;

	private string diagnosticsStatusText = string.Empty;

	private bool isDiagnosticsBusy;

	private bool endGameArmed;

	private bool runtimeInitialized;

	private RelayCommand? refreshGameRuntimeCommand;

	private RelayCommand? endGameCommand;

	private RelayCommand? openDiagnosticsFolderCommand;

	private AsyncRelayCommand? exportDiagnosticsCommand;

	/// <summary>BeamNG.drive 是否在运行。</summary>
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

	/// <summary>游戏进程状态文字。</summary>
	public string GameRuntimeStatusText
	{
		get => gameRuntimeStatusText;
		private set
		{
			if (gameRuntimeStatusText != value)
			{
				gameRuntimeStatusText = value;
				OnPropertyChanged("GameRuntimeStatusText");
			}
		}
	}

	/// <summary>游玩统计文字。</summary>
	public string PlaytimeSummaryText
	{
		get => playtimeSummaryText;
		private set
		{
			if (playtimeSummaryText != value)
			{
				playtimeSummaryText = value;
				OnPropertyChanged("PlaytimeSummaryText");
			}
		}
	}

	/// <summary>诊断包状态文字（最近导出路径 / 失败原因）。</summary>
	public string DiagnosticsStatusText
	{
		get => diagnosticsStatusText;
		private set
		{
			if (diagnosticsStatusText != value)
			{
				diagnosticsStatusText = value;
				OnPropertyChanged("DiagnosticsStatusText");
			}
		}
	}

	/// <summary>正在打包诊断包。</summary>
	public bool IsDiagnosticsBusy
	{
		get => isDiagnosticsBusy;
		private set
		{
			if (isDiagnosticsBusy != value)
			{
				isDiagnosticsBusy = value;
				OnPropertyChanged("IsDiagnosticsBusy");
			}
		}
	}

	/// <summary>「结束游戏进程」按钮文字：游戏在跑时第一下变成"再点一次确认结束"。</summary>
	public string EndGameButtonText =>
		endGameArmed ? Strings.Settings_RuntimeEndGameConfirmButton : Strings.Settings_RuntimeEndGameButton;

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand RefreshGameRuntimeCommand =>
		refreshGameRuntimeCommand ?? (refreshGameRuntimeCommand = new RelayCommand(RefreshGameRuntimeState));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand EndGameCommand =>
		endGameCommand ?? (endGameCommand = new RelayCommand(EndGame));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand OpenDiagnosticsFolderCommand =>
		openDiagnosticsFolderCommand ?? (openDiagnosticsFolderCommand = new RelayCommand(OpenDiagnosticsFolder));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IAsyncRelayCommand ExportDiagnosticsCommand =>
		exportDiagnosticsCommand ?? (exportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync));

	/// <summary>设置页 Load 时调用：初始化一次 + 每次进页面刷新状态。</summary>
	private void InitializeRuntimeSection()
	{
		runtimeInitialized = true;
		RefreshGameRuntimeState();
	}

	/// <summary>刷新运行状态与游玩统计（进设置页、点刷新、结束游戏后都会走这里）。</summary>
	public void RefreshGameRuntimeState()
	{
		try
		{
			var app = AppSettings.Current;
			bool running = GameRuntimeService.IsRunning();
			IsGameRunning = running;

			if (running)
			{
				DateTimeOffset? start = PlaytimeTracker.TryGetSessionStart(app) ?? GameRuntimeService.TryGetStartTime();
				var length = start.HasValue ? DateTimeOffset.Now - start.Value : (TimeSpan?)null;
				GameRuntimeStatusText = length.HasValue && length.Value >= TimeSpan.Zero
					? string.Format(Strings.Settings_RuntimeRunningFormat, PlaytimeTracker.FormatDuration(length.Value))
					: Strings.Settings_RuntimeNotRunning;
				endGameArmed = false;
			}
			else
			{
				GameRuntimeStatusText = Strings.Settings_RuntimeNotRunning;
			}
			OnPropertyChanged("EndGameButtonText");

			PlaytimeSummaryText = app.LaunchCount > 0
				? string.Format(
					Strings.Settings_RuntimePlaytimeFormat,
					PlaytimeTracker.FormatDuration(PlaytimeTracker.GetTotalPlaytime(app)),
					app.LaunchCount,
					PlaytimeTracker.FormatLastLaunchAt(app))
				: Strings.Settings_RuntimePlaytimeNone;

			string last = app.LastDiagnosticsBundlePath;
			DiagnosticsStatusText = (!string.IsNullOrWhiteSpace(last) && File.Exists(last))
				? string.Format(Strings.Settings_DiagnosticsLastExportFormat, Path.GetFileName(last))
				: Strings.Settings_DiagnosticsNotExportedYet;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Refresh game runtime state failed.");
		}
	}

	/// <summary>结束游戏进程：在跑则要求再点一次确认；没在跑就直接清残留。</summary>
	private void EndGame()
	{
		bool running;
		try
		{
			running = GameRuntimeService.IsRunning();
		}
		catch
		{
			running = false;
		}

		if (running && !endGameArmed)
		{
			// 真的在跑且还没确认过：先拦一下，防误点丢进度
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
			PlaytimeTracker.EndSession(AppSettings.Current);
			statusService.Report(ended > 0
				? string.Format(Strings.Settings_RuntimeEndedFormat, ended)
				: Strings.Settings_RuntimeNothingToEnd);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "End game process failed.");
			statusService.Report(string.Format(Strings.Settings_RuntimeEndFailedFormat, ex.Message));
		}
		finally
		{
			RefreshGameRuntimeState();
		}
	}

	private void OpenDiagnosticsFolder()
	{
		try
		{
			string dir = DiagnosticsBundleService.DiagnosticsDirectory;
			Directory.CreateDirectory(dir);
			instanceFolderService.TryOpen(dir);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Open diagnostics folder failed.");
			statusService.Report(string.Format(Strings.Settings_DiagnosticsFailedFormat, ex.Message));
		}
	}

	private async Task ExportDiagnosticsAsync()
	{
		if (IsDiagnosticsBusy)
		{
			return;
		}

		IsDiagnosticsBusy = true;
		DiagnosticsStatusText = Strings.Settings_DiagnosticsExporting;
		try
		{
			var app = AppSettings.Current;
			string logDirectory = LauncherLogDirectory;
			string path = await Task.Run(() => DiagnosticsBundleService.Export(app, logDirectory)).ConfigureAwait(true);

			DiagnosticsStatusText = string.Format(
				Strings.Settings_DiagnosticsExportedFormat,
				FileSizeFormatter.Format(new FileInfo(path).Length));			statusService.Report(DiagnosticsStatusText);

			// 打完包直接把目录打开，用户拖出去就能发
			try
			{
				instanceFolderService.TryOpen(Path.GetDirectoryName(path) ?? DiagnosticsBundleService.DiagnosticsDirectory);
			}
			catch
			{
				// 打开目录失败不影响导出结果
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Export diagnostics bundle failed.");
			DiagnosticsStatusText = string.Format(Strings.Settings_DiagnosticsFailedFormat, ex.Message);
			statusService.Report(DiagnosticsStatusText);
		}
		finally
		{
			IsDiagnosticsBusy = false;
		}
	}
}
