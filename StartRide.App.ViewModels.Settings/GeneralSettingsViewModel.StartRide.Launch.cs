using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using Microsoft.Extensions.Logging;
using StartRide.Core;

namespace StartRide.App.ViewModels.Settings;

public sealed partial class GeneralSettingsViewModel
{

	private bool launchCheckFilesBeforeLaunch = true;
	private bool launchSkipMenu;
	private bool launchFullScreen;
	private bool launchMinimizeLauncher;
	private bool launchCloseToTray;
	private bool launchAutoInstallMod = true;
	private bool launchRemoveModOnLeave = true;
	private bool launchIsolateConflictingMods = true;
	private bool launchNameTagEnabled = true;
	// 玩法规则（警匪追逐 / 德比 / 捉迷藏）
	private int launchCaptureHoldSeconds = 5;
	private int launchCaptureStillSpeed = 8;
	private int launchDerbyDamageLimit = 8000;
	private int launchResetLimit = 3;
	private int launchResetCooldownSeconds = 10;
	// 玩法规则（捉迷藏）
	private int launchHideSeconds = 30;
	private int launchHideRoundSeconds = 240;
	private int launchFindRadius = 12;
	private int launchFindHoldSeconds = 2;
	private bool launchForceHighPerformanceGpu = true;
	private bool startupAutoDetectGame = true;
	private bool startupAutoUpdate = true;
	private bool startupAutoCheckVehicleMods = true;
	private string launchExtraArgs = string.Empty;
	private string launchPhysicsFpsText = string.Empty;
	private string launchPreLaunchCommand = string.Empty;
	private bool launchWaitForPreLaunchCommand;
	private string launchPostExitCommand = string.Empty;
	private string launchArgsPreviewText = string.Empty;
	private SettingsGraphicsBackendOption? selectedGraphicsBackendOption;

	private RelayCommand? refreshLaunchArgsPreviewCommand;

	private string gameLogPathText = string.Empty;
	private string gameLogSummaryText = string.Empty;
	private bool isGameLogBusy;
	private string gameLogDetailText = string.Empty;

	private AsyncRelayCommand? refreshGameLogCommand;
	private RelayCommand? openGameLogFolderCommand;
	private RelayCommand? copyGameLogErrorsCommand;
	private RelayCommand? openGameLogFileCommand;

	private string backupStatusText = string.Empty;
	private bool isBackupBusy;

	private AsyncRelayCommand? createBackupCommand;
	private AsyncRelayCommand<ConfigBackupEntry>? restoreBackupCommand;
	private AsyncRelayCommand<ConfigBackupEntry>? deleteBackupCommand;
	private AsyncRelayCommand? refreshBackupsCommand;
	private RelayCommand? openBackupFolderCommand;

	private string storageSummaryText = string.Empty;
	private bool isStorageBusy;
	private string storageStatusText = string.Empty;

	private AsyncRelayCommand? refreshStorageCommand;
	private AsyncRelayCommand<StorageItem>? cleanStorageCommand;
	private AsyncRelayCommand? cleanLogBackupsCommand;
	private RelayCommand<StorageItem>? openStorageItemFolderCommand;

	private string relayStatusText = string.Empty;
	private bool isRelayProbing;
	private AsyncRelayCommand? probeRelayCommand;

	public bool LaunchCheckFilesBeforeLaunch
	{
		get => launchCheckFilesBeforeLaunch;
		set => SetStartRideSetting(ref launchCheckFilesBeforeLaunch, value, "LaunchCheckFilesBeforeLaunch",
			app => app.CheckFilesBeforeLaunch = value);
	}

	public bool LaunchSkipMenu
	{
		get => launchSkipMenu;
		set => SetStartRideSetting(ref launchSkipMenu, value, "LaunchSkipMenu",
			app => app.SkipLaunchMenu = value);
	}

	public bool LaunchFullScreen
	{
		get => launchFullScreen;
		set => SetStartRideSetting(ref launchFullScreen, value, "LaunchFullScreen",
			app => app.LaunchFullScreen = value);
	}

	public bool LaunchMinimizeLauncher
	{
		get => launchMinimizeLauncher;
		set => SetStartRideSetting(ref launchMinimizeLauncher, value, "LaunchMinimizeLauncher",
			app => app.MinimizeToTray = value);
	}

	public bool LaunchCloseToTray
	{
		get => launchCloseToTray;
		set => SetStartRideSetting(ref launchCloseToTray, value, "LaunchCloseToTray",
			app => app.CloseToTray = value);
	}

	public bool LaunchAutoInstallMod
	{
		get => launchAutoInstallMod;
		set => SetStartRideSetting(ref launchAutoInstallMod, value, "LaunchAutoInstallMod",
			app => app.AutoInstallMod = value);
	}

	public bool LaunchRemoveModOnLeave
	{
		get => launchRemoveModOnLeave;
		set => SetStartRideSetting(ref launchRemoveModOnLeave, value, "LaunchRemoveModOnLeave",
			app => app.RemoveModOnLeave = value);
	}

	/// <summary>
	/// 联机时临时隔离第三方联机模组（BeamMP / BeamLink 等）。
	/// 开着时进房前把它们置为不启用、退出后原样恢复；关着时只由游戏内模组弹窗提醒。
	/// </summary>
	public bool LaunchIsolateConflictingMods
	{
		get => launchIsolateConflictingMods;
		set => SetStartRideSetting(ref launchIsolateConflictingMods, value, "LaunchIsolateConflictingMods",
			app => app.IsolateConflictingMods = value);
	}

	/// <summary>
	/// 联机时在远端玩家车顶显示悬浮名牌（昵称 + 距离），世界坐标常显，随距离渐隐。
	/// </summary>
	public bool LaunchNameTagEnabled
	{
		get => launchNameTagEnabled;
		set => SetStartRideSetting(ref launchNameTagEnabled, value, "LaunchNameTagEnabled",
			app => app.NameTagEnabled = value);
	}

	// ------------------------------------------------------------------
	// 玩法规则（警匪追逐 / 德比）
	// ------------------------------------------------------------------

	/// <summary>警匪追逐：警察要贴住强盗多少秒才算抓住。</summary>
	public int LaunchCaptureHoldSeconds
	{
		get => launchCaptureHoldSeconds;
		set
		{
			int clamped = Math.Clamp(value, 1, 60);
			SetStartRideSetting(ref launchCaptureHoldSeconds, clamped, "LaunchCaptureHoldSeconds",
				app => app.CaptureHoldMs = clamped * 1000);
		}
	}

	/// <summary>警匪：强盗低于此速度（km/h）才算「近乎静止」，抓捕才会开始计时。</summary>
	public int LaunchCaptureStillSpeed
	{
		get => launchCaptureStillSpeed;
		set
		{
			int clamped = Math.Clamp(value, 0, 60);
			SetStartRideSetting(ref launchCaptureStillSpeed, clamped, "LaunchCaptureStillSpeed",
				app => app.CaptureStillSpeed = clamped);
		}
	}

	/// <summary>德比：车辆损伤达到这个值就淘汰。</summary>
	public int LaunchDerbyDamageLimit
	{
		get => launchDerbyDamageLimit;
		set
		{
			int clamped = Math.Clamp(value, 1000, 100000);
			SetStartRideSetting(ref launchDerbyDamageLimit, clamped, "LaunchDerbyDamageLimit",
				app => app.DerbyDamageLimit = clamped);
		}
	}

	/// <summary>德比：每局允许的原地复位次数（0 = 禁止）。</summary>
	public int LaunchResetLimit
	{
		get => launchResetLimit;
		set
		{
			int clamped = Math.Clamp(value, 0, 20);
			SetStartRideSetting(ref launchResetLimit, clamped, "LaunchResetLimit",
				app => app.ResetLimit = clamped);
		}
	}

	/// <summary>德比：两次原地复位之间的冷却（秒）。</summary>
	public int LaunchResetCooldownSeconds
	{
		get => launchResetCooldownSeconds;
		set
		{
			int clamped = Math.Clamp(value, 0, 120);
			SetStartRideSetting(ref launchResetCooldownSeconds, clamped, "LaunchResetCooldownSeconds",
				app => app.ResetCooldownMs = clamped * 1000);
		}
	}

	// ------------------------------------------------------------------
	// 玩法规则（捉迷藏）
	// ------------------------------------------------------------------

	/// <summary>捉迷藏：躲藏期时长（秒），这段时间搜索者被冻结。</summary>
	public int LaunchHideSeconds
	{
		get => launchHideSeconds;
		set
		{
			int clamped = Math.Clamp(value, 5, 300);
			SetStartRideSetting(ref launchHideSeconds, clamped, "LaunchHideSeconds",
				app => app.HideSeconds = clamped);
		}
	}

	/// <summary>捉迷藏：搜索期时长（秒），归零时还有人没被找到则躲藏方获胜。</summary>
	public int LaunchHideRoundSeconds
	{
		get => launchHideRoundSeconds;
		set
		{
			int clamped = Math.Clamp(value, 30, 1800);
			SetStartRideSetting(ref launchHideRoundSeconds, clamped, "LaunchHideRoundSeconds",
				app => app.HideRoundSeconds = clamped);
		}
	}

	/// <summary>捉迷藏：发现半径（米），搜索者进入这个范围开始计时。</summary>
	public int LaunchFindRadius
	{
		get => launchFindRadius;
		set
		{
			int clamped = Math.Clamp(value, 2, 100);
			SetStartRideSetting(ref launchFindRadius, clamped, "LaunchFindRadius",
				app => app.FindRadius = clamped);
		}
	}

	/// <summary>捉迷藏：搜索者要贴住躲藏者多少秒才算找到。</summary>
	public int LaunchFindHoldSeconds
	{
		get => launchFindHoldSeconds;
		set
		{
			int clamped = Math.Clamp(value, 1, 30);
			SetStartRideSetting(ref launchFindHoldSeconds, clamped, "LaunchFindHoldSeconds",
				app => app.FindHoldMs = clamped * 1000);
		}
	}

	public bool LaunchForceHighPerformanceGpu
	{
		get => launchForceHighPerformanceGpu;
		set => SetStartRideSetting(ref launchForceHighPerformanceGpu, value, "LaunchForceHighPerformanceGpu",
			app => app.ForceHighPerformanceGpu = value);
	}

	public bool StartupAutoDetectGame
	{
		get => startupAutoDetectGame;
		set
		{
			if (SetStartRideSetting(ref startupAutoDetectGame, value, "StartupAutoDetectGame",
				    app => app.AutoDetectGame = value) && value)
			{
				AutoDetectBeamNgDirectory();
			}
		}
	}

	public bool StartupAutoUpdate
	{
		get => startupAutoUpdate;
		set => SetStartRideSetting(ref startupAutoUpdate, value, "StartupAutoUpdate",
			app => app.AutoUpdate = value);
	}

	public bool StartupAutoCheckVehicleMods
	{
		get => startupAutoCheckVehicleMods;
		set => SetStartRideSetting(ref startupAutoCheckVehicleMods, value, "StartupAutoCheckVehicleMods",
			app => app.AutoCheckVehicleMods = value);
	}

	public string LaunchExtraArgs
	{
		get => launchExtraArgs;
		set => SetStartRideSetting(ref launchExtraArgs, value ?? string.Empty, "LaunchExtraArgs",
			app => app.ExtraLaunchArgs = (value ?? string.Empty).Trim());
	}

	public string LaunchPhysicsFpsText
	{
		get => launchPhysicsFpsText;
		set
		{
			string text = (value ?? string.Empty).Trim();
			if (launchPhysicsFpsText == text) return;

			int fps = 0;
			if (text.Length > 0 && (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out fps)
			                        || fps < 500 || fps > 10000))
			{
				if (text.Length > 0 && text.All(char.IsDigit))
				{
					launchPhysicsFpsText = text;
					OnPropertyChanged("LaunchPhysicsFpsText");
				}
				return;
			}

			launchPhysicsFpsText = text;
			OnPropertyChanged("LaunchPhysicsFpsText");

			var app = AppSettings.Current;
			if (app.PhysicsFps != fps)
			{
				app.PhysicsFps = fps;
				app.Save();
			}
			RefreshLaunchArgsPreview();
		}
	}

	public string LaunchPreLaunchCommand
	{
		get => launchPreLaunchCommand;
		set => SetStartRideSetting(ref launchPreLaunchCommand, value ?? string.Empty, "LaunchPreLaunchCommand",
			app => app.PreLaunchCommand = (value ?? string.Empty).Trim());
	}

	public bool LaunchWaitForPreLaunchCommand
	{
		get => launchWaitForPreLaunchCommand;
		set => SetStartRideSetting(ref launchWaitForPreLaunchCommand, value, "LaunchWaitForPreLaunchCommand",
			app => app.WaitForPreLaunchCommand = value);
	}

	public string LaunchPostExitCommand
	{
		get => launchPostExitCommand;
		set => SetStartRideSetting(ref launchPostExitCommand, value ?? string.Empty, "LaunchPostExitCommand",
			app => app.PostExitCommand = (value ?? string.Empty).Trim());
	}

	public ObservableCollection<SettingsGraphicsBackendOption> GraphicsBackendOptions { get; } =
		new ObservableCollection<SettingsGraphicsBackendOption>();

	public SettingsGraphicsBackendOption? SelectedGraphicsBackendOption
	{
		get => selectedGraphicsBackendOption;
		set
		{
			if (selectedGraphicsBackendOption == value) return;
			selectedGraphicsBackendOption = value;
			OnPropertyChanged("SelectedGraphicsBackendOption");

			var app = AppSettings.Current;
			string key = value?.Key ?? "";
			if (app.GraphicsBackend != key)
			{
				app.GraphicsBackend = key;
				app.Save();
			}
			RefreshLaunchArgsPreview();
		}
	}

	public string LaunchArgsPreviewText
	{
		get => launchArgsPreviewText;
		set
		{
			if (launchArgsPreviewText != value)
			{
				launchArgsPreviewText = value;
				OnPropertyChanged("LaunchArgsPreviewText");
			}
		}
	}

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand RefreshLaunchArgsPreviewCommand =>
		refreshLaunchArgsPreviewCommand ?? (refreshLaunchArgsPreviewCommand = new RelayCommand(RefreshLaunchArgsPreview));

	private void InitializeStartRideLaunchSection()
	{
		GraphicsBackendOptions.Clear();
		GraphicsBackendOptions.Add(new SettingsGraphicsBackendOption("", Strings.Settings_LaunchGfxDefault));
		GraphicsBackendOptions.Add(new SettingsGraphicsBackendOption("dx11", Strings.Settings_LaunchGfxDx11));
		GraphicsBackendOptions.Add(new SettingsGraphicsBackendOption("d3d12", Strings.Settings_LaunchGfxD3D12));
		GraphicsBackendOptions.Add(new SettingsGraphicsBackendOption("vk", Strings.Settings_LaunchGfxVulkan));

		ReloadStartRideLaunchSection();
		InitializeGameLogSection();
		InitializeBackupSection();
		InitializeStorageSection();
		InitializeRelaySection();
	}

	private void ReloadStartRideLaunchSection()
	{
		var app = AppSettings.Current;

		launchCheckFilesBeforeLaunch = app.CheckFilesBeforeLaunch;
		launchSkipMenu = app.SkipLaunchMenu;
		launchFullScreen = app.LaunchFullScreen;
		launchMinimizeLauncher = app.MinimizeToTray;
		launchCloseToTray = app.CloseToTray;
		launchAutoInstallMod = app.AutoInstallMod;
		launchRemoveModOnLeave = app.RemoveModOnLeave;
		launchIsolateConflictingMods = app.IsolateConflictingMods;
		launchNameTagEnabled = app.NameTagEnabled;
		launchCaptureHoldSeconds = Math.Clamp(app.CaptureHoldMs / 1000, 1, 60);
		launchCaptureStillSpeed = Math.Clamp(app.CaptureStillSpeed, 0, 60);
		launchDerbyDamageLimit = Math.Clamp(app.DerbyDamageLimit, 1000, 100000);
		launchResetLimit = Math.Clamp(app.ResetLimit, 0, 20);
		launchResetCooldownSeconds = Math.Clamp(app.ResetCooldownMs / 1000, 0, 120);
		launchHideSeconds = Math.Clamp(app.HideSeconds, 5, 300);
		launchHideRoundSeconds = Math.Clamp(app.HideRoundSeconds, 30, 1800);
		launchFindRadius = Math.Clamp(app.FindRadius, 2, 100);
		launchFindHoldSeconds = Math.Clamp(app.FindHoldMs / 1000, 1, 30);
		launchForceHighPerformanceGpu = app.ForceHighPerformanceGpu;
		startupAutoDetectGame = app.AutoDetectGame;
		startupAutoUpdate = app.AutoUpdate;
		startupAutoCheckVehicleMods = app.AutoCheckVehicleMods;
		launchExtraArgs = app.ExtraLaunchArgs ?? string.Empty;
		launchPreLaunchCommand = app.PreLaunchCommand ?? string.Empty;
		launchWaitForPreLaunchCommand = app.WaitForPreLaunchCommand;
		launchPostExitCommand = app.PostExitCommand ?? string.Empty;
		launchPhysicsFpsText = app.PhysicsFps > 0
			? app.PhysicsFps.ToString(CultureInfo.InvariantCulture)
			: string.Empty;

		string gfx = GameLauncher.NormalizeGraphicsBackend(app.GraphicsBackend);
		selectedGraphicsBackendOption = GraphicsBackendOptions.FirstOrDefault(o => o.Key == gfx)
		                                ?? GraphicsBackendOptions[0];

		OnPropertyChanged("LaunchCheckFilesBeforeLaunch");
		OnPropertyChanged("LaunchSkipMenu");
		OnPropertyChanged("LaunchFullScreen");
		OnPropertyChanged("LaunchMinimizeLauncher");
		OnPropertyChanged("LaunchCloseToTray");
		OnPropertyChanged("LaunchAutoInstallMod");
		OnPropertyChanged("LaunchRemoveModOnLeave");
		OnPropertyChanged("LaunchIsolateConflictingMods");
		OnPropertyChanged("LaunchNameTagEnabled");
		OnPropertyChanged("LaunchCaptureHoldSeconds");
		OnPropertyChanged("LaunchCaptureStillSpeed");
		OnPropertyChanged("LaunchDerbyDamageLimit");
		OnPropertyChanged("LaunchResetLimit");
		OnPropertyChanged("LaunchResetCooldownSeconds");
		OnPropertyChanged("LaunchHideSeconds");
		OnPropertyChanged("LaunchHideRoundSeconds");
		OnPropertyChanged("LaunchFindRadius");
		OnPropertyChanged("LaunchFindHoldSeconds");
		OnPropertyChanged("LaunchForceHighPerformanceGpu");
		OnPropertyChanged("StartupAutoDetectGame");
		OnPropertyChanged("StartupAutoUpdate");
		OnPropertyChanged("StartupAutoCheckVehicleMods");
		OnPropertyChanged("LaunchExtraArgs");
		OnPropertyChanged("LaunchPreLaunchCommand");
		OnPropertyChanged("LaunchWaitForPreLaunchCommand");
		OnPropertyChanged("LaunchPostExitCommand");
		OnPropertyChanged("LaunchPhysicsFpsText");
		OnPropertyChanged("SelectedGraphicsBackendOption");

		RefreshLaunchArgsPreview();
	}

	internal void RefreshLaunchArgsPreview()
	{
		try
		{
			var args = GameLauncher.BuildLaunchArguments(AppSettings.Current);
			string exe = "BeamNG.drive.exe";
			LaunchArgsPreviewText = args.Count == 0
				? exe + " " + Strings.Settings_LaunchArgsPreviewNone
				: exe + " " + string.Join(" ", args.Select(Quote));
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Build launch args preview failed.");
			LaunchArgsPreviewText = string.Empty;
		}
	}

	private static string Quote(string arg) => arg.Contains(' ') ? "\"" + arg + "\"" : arg;

	private bool SetStartRideSetting(ref bool field, bool value, string propertyName, Action<AppSettings> apply)
	{
		if (field == value) return false;
		field = value;
		OnPropertyChanged(propertyName);
		PersistStartRide(apply);
		return true;
	}

	private bool SetStartRideSetting(ref string field, string value, string propertyName, Action<AppSettings> apply)
	{
		if (field == value) return false;
		field = value;
		OnPropertyChanged(propertyName);
		PersistStartRide(apply);
		return true;
	}

	/// <summary>玩法规则的数值项（抓捕秒数 / 德比阈值 / 复位次数与冷却）用这个重载。</summary>
	private bool SetStartRideSetting(ref int field, int value, string propertyName, Action<AppSettings> apply)
	{
		if (field == value) return false;
		field = value;
		OnPropertyChanged(propertyName);
		PersistStartRide(apply);
		return true;
	}

	private void PersistStartRide(Action<AppSettings> apply)
	{
		try
		{
			var app = AppSettings.Current;
			apply(app);
			app.Save();
			RefreshLaunchArgsPreview();
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Persist StartRide launch setting failed.");
		}
	}

	public ObservableCollection<GameLogItem> GameLogProblems { get; } = new ObservableCollection<GameLogItem>();

	public string GameLogPathText
	{
		get => gameLogPathText;
		set { if (gameLogPathText != value) { gameLogPathText = value; OnPropertyChanged("GameLogPathText"); } }
	}

	public string GameLogSummaryText
	{
		get => gameLogSummaryText;
		set { if (gameLogSummaryText != value) { gameLogSummaryText = value; OnPropertyChanged("GameLogSummaryText"); } }
	}

	public string GameLogDetailText
	{
		get => gameLogDetailText;
		set { if (gameLogDetailText != value) { gameLogDetailText = value; OnPropertyChanged("GameLogDetailText"); } }
	}

	public bool IsGameLogBusy
	{
		get => isGameLogBusy;
		set { if (isGameLogBusy != value) { isGameLogBusy = value; OnPropertyChanged("IsGameLogBusy"); } }
	}

	public bool HasGameLogProblems => GameLogProblems.Count > 0;

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IAsyncRelayCommand RefreshGameLogCommand =>
		refreshGameLogCommand ?? (refreshGameLogCommand = new AsyncRelayCommand(RefreshGameLogAsync));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand OpenGameLogFolderCommand =>
		openGameLogFolderCommand ?? (openGameLogFolderCommand = new RelayCommand(OpenGameLogFolder));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand OpenGameLogFileCommand =>
		openGameLogFileCommand ?? (openGameLogFileCommand = new RelayCommand(OpenGameLogFile));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand CopyGameLogErrorsCommand =>
		copyGameLogErrorsCommand ?? (copyGameLogErrorsCommand = new RelayCommand(CopyGameLogErrors));

	private void InitializeGameLogSection()
	{
		GameLogPathText = new GameLogService(AppSettings.Current).ResolveActiveLogPath();
		GameLogSummaryText = Strings.Settings_GameLogNotScanned;
		_ = RefreshGameLogAsync();
	}

	private async Task RefreshGameLogAsync()
	{
		if (IsGameLogBusy) return;
		IsGameLogBusy = true;
		try
		{
			var service = new GameLogService(AppSettings.Current);
			string path = service.ResolveActiveLogPath();
			GameLogPathText = path;

			var summary = await Task.Run(() =>
			{
				var s = service.Analyze(path, out List<GameLogLine> problems);
				return (s, problems);
			}).ConfigureAwait(true);

			GameLogProblems.Clear();
			foreach (var line in summary.problems)
			{
				GameLogProblems.Add(new GameLogItem(line));
			}
			OnPropertyChanged("HasGameLogProblems");

			var info = summary.s;
			if (!info.Exists)
			{
				GameLogSummaryText = Strings.Settings_GameLogNotFound;
				GameLogDetailText = path;
				return;
			}

			string when = info.ModifiedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-";
			GameLogSummaryText = string.Format(
				Strings.Settings_GameLogSummaryFormat,
				info.TotalLines,
				info.ErrorCount,
				info.WarningCount,
				FileSizeFormatter.Format(info.SizeBytes));

			GameLogDetailText = string.Format(
				Strings.Settings_GameLogDetailFormat,
				info.GameVersion.Length > 0 ? info.GameVersion : "—",
				when);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Refresh game log failed.");
			GameLogSummaryText = string.Format(Strings.Settings_GameLogFailedFormat, ex.Message);
		}
		finally
		{
			IsGameLogBusy = false;
		}
	}

	private void OpenGameLogFolder()
	{
		try
		{
			var service = new GameLogService(AppSettings.Current);
			string dir = service.LogDirectory;
			if (!Directory.Exists(dir)) dir = Path.GetDirectoryName(service.ResolveActiveLogPath()) ?? dir;
			if (Directory.Exists(dir)) instanceFolderService.TryOpen(dir);
			else statusService.Report(Strings.Settings_GameLogNotFound);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Open game log folder failed.");
		}
	}

	private void OpenGameLogFile()
	{
		try
		{
			string path = new GameLogService(AppSettings.Current).ResolveActiveLogPath();
			if (!File.Exists(path))
			{
				OpenGameLogFolder();
				return;
			}
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Open game log file failed.");
		}
	}

	private void CopyGameLogErrors()
	{
		try
		{
			var errors = GameLogProblems.Where(p => p.IsProblem).ToList();
			var source = errors.Count > 0 ? errors : GameLogProblems.ToList();
			if (source.Count == 0)
			{
				statusService.Report(Strings.Settings_GameLogNoProblemToCopy);
				return;
			}

			string text = string.Join(Environment.NewLine, source.Select(p => p.Raw));
			Clipboard.SetText(text);
			statusService.Report(string.Format(Strings.Settings_GameLogCopiedFormat, source.Count));
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Copy game log errors failed.");
			statusService.Report(Strings.Settings_GameLogCopyFailed);
		}
	}

	public ObservableCollection<ConfigBackupEntry> Backups { get; } = new ObservableCollection<ConfigBackupEntry>();

	public bool HasBackups => Backups.Count > 0;

	public string BackupStatusText
	{
		get => backupStatusText;
		set { if (backupStatusText != value) { backupStatusText = value; OnPropertyChanged("BackupStatusText"); } }
	}

	public bool IsBackupBusy
	{
		get => isBackupBusy;
		set { if (isBackupBusy != value) { isBackupBusy = value; OnPropertyChanged("IsBackupBusy"); } }
	}

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IAsyncRelayCommand CreateBackupCommand =>
		createBackupCommand ?? (createBackupCommand = new AsyncRelayCommand(CreateBackupAsync));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IAsyncRelayCommand<ConfigBackupEntry> RestoreBackupCommand =>
		restoreBackupCommand ?? (restoreBackupCommand = new AsyncRelayCommand<ConfigBackupEntry>(
			entry => RestoreBackupAsync(entry), entry => entry != null));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IAsyncRelayCommand<ConfigBackupEntry> DeleteBackupCommand =>
		deleteBackupCommand ?? (deleteBackupCommand = new AsyncRelayCommand<ConfigBackupEntry>(
			entry => DeleteBackupAsync(entry), entry => entry != null));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IAsyncRelayCommand RefreshBackupsCommand =>
		refreshBackupsCommand ?? (refreshBackupsCommand = new AsyncRelayCommand(RefreshBackupsAsync));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand OpenBackupFolderCommand =>
		openBackupFolderCommand ?? (openBackupFolderCommand = new RelayCommand(OpenBackupFolder));

	private void InitializeBackupSection()
	{
		BackupStatusText = Strings.Settings_BackupNotCreatedYet;
		_ = RefreshBackupsAsync();
	}

	private async Task RefreshBackupsAsync()
	{
		try
		{
			var service = new ConfigBackupService(AppSettings.Current);
			var list = await Task.Run(() => service.ListBackups()).ConfigureAwait(true);

			Backups.Clear();
			foreach (var b in list) Backups.Add(b);
			OnPropertyChanged("HasBackups");

			if (Backups.Count > 0)
			{
				long total = Backups.Sum(b => b.SizeBytes);
				BackupStatusText = string.Format(Strings.Settings_BackupCountFormat, Backups.Count, FileSizeFormatter.Format(total));
			}
			else
			{
				BackupStatusText = Strings.Settings_BackupNotCreatedYet;
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Refresh backups failed.");
		}
	}

	private async Task CreateBackupAsync()
	{
		if (IsBackupBusy) return;
		IsBackupBusy = true;
		BackupStatusText = Strings.Settings_BackupWorking;
		try
		{
			var service = new ConfigBackupService(AppSettings.Current);
			var result = await Task.Run(() => service.CreateBackup()).ConfigureAwait(true);

			BackupStatusText = result.Success
				? string.Format(Strings.Settings_BackupCreatedFormat, Path.GetFileName(result.Path), result.FileCount)
				: string.Format(Strings.Settings_BackupFailedFormat, result.Message);

			if (result.Success) statusService.Report(BackupStatusText);
			await RefreshBackupsAsync().ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Create backup failed.");
			BackupStatusText = string.Format(Strings.Settings_BackupFailedFormat, ex.Message);
		}
		finally
		{
			IsBackupBusy = false;
		}
	}

	private async Task RestoreBackupAsync(ConfigBackupEntry? entry)
	{
		if (IsBackupBusy || entry == null) return;

		if (MessageBox.Show(
			    string.Format(Strings.Settings_BackupRestoreConfirmMessage, entry.FileName),
			    Strings.Settings_BackupRestoreConfirmTitle,
			    MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
		{
			return;
		}

		IsBackupBusy = true;
		BackupStatusText = Strings.Settings_BackupRestoring;
		try
		{
			var service = new ConfigBackupService(AppSettings.Current);
			var result = await Task.Run(() => service.RestoreBackup(entry.Path)).ConfigureAwait(true);

			BackupStatusText = result.Success
				? result.Message
				: string.Format(Strings.Settings_BackupFailedFormat, result.Message);
			statusService.Report(BackupStatusText);

			if (result.Success) await RefreshBackupsAsync().ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Restore backup failed.");
			BackupStatusText = string.Format(Strings.Settings_BackupFailedFormat, ex.Message);
		}
		finally
		{
			IsBackupBusy = false;
		}
	}

	private async Task DeleteBackupAsync(ConfigBackupEntry? entry)
	{
		if (entry == null) return;

		if (MessageBox.Show(
			    string.Format(Strings.Settings_BackupDeleteConfirmMessage, entry.FileName),
			    Strings.Settings_BackupDeleteConfirmTitle,
			    MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
		{
			return;
		}

		try
		{
			var service = new ConfigBackupService(AppSettings.Current);
			bool ok = await Task.Run(() => service.DeleteBackup(entry.Path)).ConfigureAwait(true);
			BackupStatusText = ok ? Strings.Settings_BackupDeleted : Strings.Settings_BackupDeleteFailed;
			await RefreshBackupsAsync().ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Delete backup failed.");
		}
	}

	private void OpenBackupFolder()
	{
		try
		{
			string dir = ConfigBackupService.BackupDirectory;
			Directory.CreateDirectory(dir);
			instanceFolderService.TryOpen(dir);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Open backup folder failed.");
		}
	}

	public ObservableCollection<StorageItem> StorageItems { get; } = new ObservableCollection<StorageItem>();

	public string StorageSummaryText
	{
		get => storageSummaryText;
		set { if (storageSummaryText != value) { storageSummaryText = value; OnPropertyChanged("StorageSummaryText"); } }
	}

	public string StorageStatusText
	{
		get => storageStatusText;
		set { if (storageStatusText != value) { storageStatusText = value; OnPropertyChanged("StorageStatusText"); } }
	}

	public bool IsStorageBusy
	{
		get => isStorageBusy;
		set { if (isStorageBusy != value) { isStorageBusy = value; OnPropertyChanged("IsStorageBusy"); } }
	}

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IAsyncRelayCommand RefreshStorageCommand =>
		refreshStorageCommand ?? (refreshStorageCommand = new AsyncRelayCommand(RefreshStorageAsync));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IAsyncRelayCommand<StorageItem> CleanStorageCommand =>
		cleanStorageCommand ?? (cleanStorageCommand = new AsyncRelayCommand<StorageItem>(
			item => CleanStorageAsync(item), item => item?.CanClean == true));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IAsyncRelayCommand CleanLogBackupsCommand =>
		cleanLogBackupsCommand ?? (cleanLogBackupsCommand = new AsyncRelayCommand(CleanLogBackupsAsync));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand<StorageItem> OpenStorageItemFolderCommand =>
		openStorageItemFolderCommand ?? (openStorageItemFolderCommand = new RelayCommand<StorageItem>(OpenStorageItemFolder));

	private void InitializeStorageSection()
	{
		StorageSummaryText = Strings.Settings_StorageNotScanned;
	}

	private async Task RefreshStorageAsync()
	{
		if (IsStorageBusy) return;
		IsStorageBusy = true;
		try
		{
			var service = new StorageUsageService(AppSettings.Current);
			var items = await Task.Run(() => service.Scan()).ConfigureAwait(true);

			StorageItems.Clear();
			foreach (var item in items) StorageItems.Add(item);

			long total = items.Sum(i => i.SizeBytes);
			long cleanable = items.Where(i => i.CanClean).Sum(i => i.SizeBytes);
			StorageSummaryText = string.Format(
				Strings.Settings_StorageSummaryFormat,
				FileSizeFormatter.Format(total),
				FileSizeFormatter.Format(cleanable));
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Scan storage usage failed.");
			StorageSummaryText = string.Format(Strings.Settings_StorageFailedFormat, ex.Message);
		}
		finally
		{
			IsStorageBusy = false;
		}
	}

	private async Task CleanStorageAsync(StorageItem? target)
	{
		if (target == null || !target.CanClean) return;

		if (MessageBox.Show(
			    string.Format(Strings.Settings_StorageCleanConfirmMessage, target.DisplayName, target.SizeText),
			    Strings.Settings_StorageCleanConfirmTitle,
			    MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
		{
			return;
		}

		IsStorageBusy = true;
		try
		{
			var service = new StorageUsageService(AppSettings.Current);
			var result = await Task.Run(() => service.Clean(target.Key)).ConfigureAwait(true);
			StorageStatusText = result.Message;
			statusService.Report(result.Message);
			await RefreshStorageAsync().ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Clean storage failed.");
			StorageStatusText = string.Format(Strings.Settings_StorageFailedFormat, ex.Message);
		}
		finally
		{
			IsStorageBusy = false;
		}
	}

	private async Task CleanLogBackupsAsync()
	{
		IsStorageBusy = true;
		try
		{
			var service = new StorageUsageService(AppSettings.Current);
			var result = await Task.Run(() => service.CleanLogBackups()).ConfigureAwait(true);
			StorageStatusText = result.Message;
			statusService.Report(result.Message);
			await RefreshStorageAsync().ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Clean log backups failed.");
			StorageStatusText = string.Format(Strings.Settings_StorageFailedFormat, ex.Message);
		}
		finally
		{
			IsStorageBusy = false;
		}
	}

	private void OpenStorageItemFolder(StorageItem? target)
	{
		try
		{
			if (target == null) return;
			if (Directory.Exists(target.Path)) instanceFolderService.TryOpen(target.Path);
			else statusService.Report(Strings.Settings_StorageFolderMissing);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Open storage folder failed.");
		}
	}

	public ObservableCollection<RelayProbeItem> RelayProbes { get; } = new ObservableCollection<RelayProbeItem>();

	public string RelayStatusText
	{
		get => relayStatusText;
		set { if (relayStatusText != value) { relayStatusText = value; OnPropertyChanged("RelayStatusText"); } }
	}

	public bool IsRelayProbing
	{
		get => isRelayProbing;
		set { if (isRelayProbing != value) { isRelayProbing = value; OnPropertyChanged("IsRelayProbing"); } }
	}

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IAsyncRelayCommand ProbeRelayCommand =>
		probeRelayCommand ?? (probeRelayCommand = new AsyncRelayCommand(ProbeRelayAsync));

	private void InitializeRelaySection()
	{
		var app = AppSettings.Current;
		RelayStatusText = app.LastRelayLatencyMs >= 0
			? string.Format(Strings.Settings_RelayLastResultFormat, app.LastRelayLatencyMs)
			: Strings.Settings_RelayNotTested;
	}

	private async Task ProbeRelayAsync()
	{
		if (IsRelayProbing) return;
		IsRelayProbing = true;
		RelayStatusText = Strings.Settings_RelayTesting;
		try
		{
			var service = new RelayLatencyService(AppSettings.Current);
			var results = await service.ProbeAllAsync().ConfigureAwait(true);

			RelayProbes.Clear();
			foreach (var r in results) RelayProbes.Add(new RelayProbeItem(r));

			var best = results.FirstOrDefault(r => r.Reachable);
			RelayStatusText = best != null
				? string.Format(Strings.Settings_RelayResultFormat, best.LatencyText + " · " + best.Endpoint)
				: Strings.Settings_RelayAllUnreachable;
			statusService.Report(RelayStatusText);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Probe relay failed.");
			RelayStatusText = string.Format(Strings.Settings_RelayFailedFormat, ex.Message);
		}
		finally
		{
			IsRelayProbing = false;
		}
	}
}

public sealed class SettingsGraphicsBackendOption
{
	public string Key { get; }
	public string Title { get; }

	public SettingsGraphicsBackendOption(string key, string title)
	{
		Key = key;
		Title = title;
	}
}

public sealed class GameLogItem
{
	public string Raw { get; }
	public string LevelText { get; }
	public string Module { get; }
	public string Message { get; }
	public bool IsProblem { get; }
	public bool IsWarning { get; }
	public string TimeText { get; }

	public GameLogItem(GameLogLine line)
	{
		Raw = line.Raw;
		LevelText = line.LevelText;
		Module = line.Module;
		Message = line.Message;
		IsProblem = line.IsProblem;
		IsWarning = line.IsWarning;
		TimeText = line.Seconds >= 0 ? line.Seconds.ToString("0.00", CultureInfo.InvariantCulture) + "s" : "";
	}
}

public sealed class RelayProbeItem
{
	public string Name { get; }
	public string Endpoint { get; }
	public string LatencyText { get; }
	public string Detail { get; }
	public bool Reachable { get; }

	public RelayProbeItem(RelayProbeResult result)
	{
		Name = result.Name;
		Endpoint = result.Endpoint;
		LatencyText = result.LatencyText;
		Detail = result.Detail;
		Reachable = result.Reachable;
	}
}
