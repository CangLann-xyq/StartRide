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

/// <summary>
/// 通用设置页的 StartRide 第二批内容（partial 扩展）：
///   1. 启动行为   —— 启动参数 / 全屏 / 跳过菜单 / 渲染后端 / 物理帧率 / 启动前与退出后命令 /
///                    启动后最小化 / 关窗进托盘 / 启动时自动探测与检查
///   2. 游戏日志   —— 读 beamng.log，按 D/I/W/E 分级统计，直接看错误
///   3. 配置备份   —— &lt;userpath&gt;\settings 打包成 zip，可还原（还原前自动留底）
///   4. 磁盘占用   —— mods/replays/temp 各占多少，回放与缓存一键清理
///   5. 联机延迟   —— 中继两条通道的连通性与延迟
///
/// 这些开关以前大部分只在「内存与启动」页里能填、且只写进 LauncherSettings，
/// 而真正启动 BeamNG 的代码从来不读那份配置 —— 填了等于没填。
/// 现在统一落到 AppSettings，并且每一项都有明确的消费者（见 LaunchArgsPreviewText 的实时预览）。
/// </summary>
public sealed partial class GeneralSettingsViewModel
{
	// ---------------- 启动行为 字段 ----------------

	private bool launchCheckFilesBeforeLaunch = true;
	private bool launchSkipMenu;
	private bool launchFullScreen;
	private bool launchMinimizeLauncher;
	private bool launchCloseToTray;
	private bool launchAutoInstallMod = true;
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

	// ---------------- 游戏日志 字段 ----------------

	private string gameLogPathText = string.Empty;
	private string gameLogSummaryText = string.Empty;
	private bool isGameLogBusy;
	private string gameLogDetailText = string.Empty;

	private AsyncRelayCommand? refreshGameLogCommand;
	private RelayCommand? openGameLogFolderCommand;
	private RelayCommand? copyGameLogErrorsCommand;
	private RelayCommand? openGameLogFileCommand;

	// ---------------- 配置备份 字段 ----------------

	private string backupStatusText = string.Empty;
	private bool isBackupBusy;

	private AsyncRelayCommand? createBackupCommand;
	private AsyncRelayCommand<ConfigBackupEntry>? restoreBackupCommand;
	private AsyncRelayCommand<ConfigBackupEntry>? deleteBackupCommand;
	private AsyncRelayCommand? refreshBackupsCommand;
	private RelayCommand? openBackupFolderCommand;

	// ---------------- 磁盘占用 字段 ----------------

	private string storageSummaryText = string.Empty;
	private bool isStorageBusy;
	private string storageStatusText = string.Empty;

	private AsyncRelayCommand? refreshStorageCommand;
	private AsyncRelayCommand<StorageItem>? cleanStorageCommand;
	private AsyncRelayCommand? cleanLogBackupsCommand;
	private RelayCommand<StorageItem>? openStorageItemFolderCommand;

	// ---------------- 联机延迟 字段 ----------------

	private string relayStatusText = string.Empty;
	private bool isRelayProbing;
	private AsyncRelayCommand? probeRelayCommand;

	// ==================================================================
	//                          启动行为
	// ==================================================================

	/// <summary>启动前做一次游戏文件体检（缺什么先提示，别等进游戏才发现）。</summary>
	public bool LaunchCheckFilesBeforeLaunch
	{
		get => launchCheckFilesBeforeLaunch;
		set => SetStartRideSetting(ref launchCheckFilesBeforeLaunch, value, "LaunchCheckFilesBeforeLaunch",
			app => app.CheckFilesBeforeLaunch = value);
	}

	/// <summary>跳过游戏启动菜单（-noninteractive）。</summary>
	public bool LaunchSkipMenu
	{
		get => launchSkipMenu;
		set => SetStartRideSetting(ref launchSkipMenu, value, "LaunchSkipMenu",
			app => app.SkipLaunchMenu = value);
	}

	/// <summary>全屏启动（-fullscreen）。</summary>
	public bool LaunchFullScreen
	{
		get => launchFullScreen;
		set => SetStartRideSetting(ref launchFullScreen, value, "LaunchFullScreen",
			app => app.LaunchFullScreen = value);
	}

	/// <summary>启动游戏后把启动器最小化到任务栏。</summary>
	public bool LaunchMinimizeLauncher
	{
		get => launchMinimizeLauncher;
		set => SetStartRideSetting(ref launchMinimizeLauncher, value, "LaunchMinimizeLauncher",
			app => app.MinimizeToTray = value);
	}

	/// <summary>点关闭按钮时收进托盘而不是退出（托盘菜单里可以真退出）。</summary>
	public bool LaunchCloseToTray
	{
		get => launchCloseToTray;
		set => SetStartRideSetting(ref launchCloseToTray, value, "LaunchCloseToTray",
			app => app.CloseToTray = value);
	}

	/// <summary>每次启动前重装一次联机模组。</summary>
	public bool LaunchAutoInstallMod
	{
		get => launchAutoInstallMod;
		set => SetStartRideSetting(ref launchAutoInstallMod, value, "LaunchAutoInstallMod",
			app => app.AutoInstallMod = value);
	}

	/// <summary>强制游戏走独显（-highperformancegpu）。</summary>
	public bool LaunchForceHighPerformanceGpu
	{
		get => launchForceHighPerformanceGpu;
		set => SetStartRideSetting(ref launchForceHighPerformanceGpu, value, "LaunchForceHighPerformanceGpu",
			app => app.ForceHighPerformanceGpu = value);
	}

	/// <summary>启动器启动时自动探测游戏安装目录。</summary>
	public bool StartupAutoDetectGame
	{
		get => startupAutoDetectGame;
		set
		{
			if (SetStartRideSetting(ref startupAutoDetectGame, value, "StartupAutoDetectGame",
				    app => app.AutoDetectGame = value) && value)
			{
				// 打开就立刻跑一次，让用户马上看到效果，而不是"下次启动才生效"
				AutoDetectBeamNgDirectory();
			}
		}
	}

	/// <summary>启动时检查启动器更新。</summary>
	public bool StartupAutoUpdate
	{
		get => startupAutoUpdate;
		set => SetStartRideSetting(ref startupAutoUpdate, value, "StartupAutoUpdate",
			app => app.AutoUpdate = value);
	}

	/// <summary>启动时检查车辆模组是否完整。</summary>
	public bool StartupAutoCheckVehicleMods
	{
		get => startupAutoCheckVehicleMods;
		set => SetStartRideSetting(ref startupAutoCheckVehicleMods, value, "StartupAutoCheckVehicleMods",
			app => app.AutoCheckVehicleMods = value);
	}

	/// <summary>额外启动参数（支持带引号的路径）。</summary>
	public string LaunchExtraArgs
	{
		get => launchExtraArgs;
		set => SetStartRideSetting(ref launchExtraArgs, value ?? string.Empty, "LaunchExtraArgs",
			app => app.ExtraLaunchArgs = (value ?? string.Empty).Trim());
	}

	/// <summary>物理步进频率（-physicsfps）；留空 = 游戏默认 2000。</summary>
	public string LaunchPhysicsFpsText
	{
		get => launchPhysicsFpsText;
		set
		{
			string text = (value ?? string.Empty).Trim();
			if (launchPhysicsFpsText == text) return;

			// 只接受 0/空 或 500..10000 的整数，其余当没填
			int fps = 0;
			if (text.Length > 0 && (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out fps)
			                        || fps < 500 || fps > 10000))
			{
				// 仍在输入中的中间状态（比如只敲了个 "1"）不要回写，避免把用户的手打断
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

	/// <summary>启动游戏前执行的命令。</summary>
	public string LaunchPreLaunchCommand
	{
		get => launchPreLaunchCommand;
		set => SetStartRideSetting(ref launchPreLaunchCommand, value ?? string.Empty, "LaunchPreLaunchCommand",
			app => app.PreLaunchCommand = (value ?? string.Empty).Trim());
	}

	/// <summary>是否等启动前命令跑完再拉起游戏。</summary>
	public bool LaunchWaitForPreLaunchCommand
	{
		get => launchWaitForPreLaunchCommand;
		set => SetStartRideSetting(ref launchWaitForPreLaunchCommand, value, "LaunchWaitForPreLaunchCommand",
			app => app.WaitForPreLaunchCommand = value);
	}

	/// <summary>游戏退出后执行的命令。</summary>
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

	/// <summary>
	/// 最终会拼给 BeamNG.drive.exe 的参数预览。
	/// 「设置到底有没有生效」在这里一眼可见，不用去猜。
	/// </summary>
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

	/// <summary>构造 / 加载设置后初始化这一块。</summary>
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

	/// <summary>从 AppSettings 读一遍（不进 setter，避免初始化期间到处写盘）。</summary>
	private void ReloadStartRideLaunchSection()
	{
		var app = AppSettings.Current;

		launchCheckFilesBeforeLaunch = app.CheckFilesBeforeLaunch;
		launchSkipMenu = app.SkipLaunchMenu;
		launchFullScreen = app.LaunchFullScreen;
		launchMinimizeLauncher = app.MinimizeToTray;
		launchCloseToTray = app.CloseToTray;
		launchAutoInstallMod = app.AutoInstallMod;
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

	/// <summary>把当前设置真正拼一遍参数，展示给用户。</summary>
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

	/// <summary>统一的"写 AppSettings + 通知界面 + 刷新参数预览"。</summary>
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
		// 文本类改动频繁（每敲一个字符），参数预览跟着刷
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

	// ==================================================================
	//                          游戏日志
	// ==================================================================

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

	/// <summary>
	/// 读游戏日志并统计。日志格式自带等级字段（时间|等级|模块|消息），
	/// 所以这里不是"搜关键字猜错误"，而是按等级精确计数。
	/// </summary>
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

	/// <summary>把错误行复制到剪贴板，方便贴给群里/论坛求助。</summary>
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

	// ==================================================================
	//                          配置备份
	// ==================================================================

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

	/// <summary>整行按钮直接带条目进来，不依赖列表选中态（省掉一层样式坑）。</summary>
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

	// ==================================================================
	//                          磁盘占用
	// ==================================================================

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

	// ==================================================================
	//                          联机延迟
	// ==================================================================

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

/// <summary>渲染后端下拉项。</summary>
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

/// <summary>日志行界面项。</summary>
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

/// <summary>中继探测结果界面项。</summary>
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
