using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Microsoft.Extensions.Logging;
using StartRide.Core;

namespace Launcher.App.ViewModels.Settings;

/// <summary>
/// 通用设置页里 StartRide 专属的两块内容（partial 扩展，避免动原反编译文件的大段逻辑）：
///   1. 游戏安装目录 —— 自动识别本机 BeamNG.drive，写进 StartRide 设置，启动按钮立刻生效
///   2. 必需配置文件 —— 检查缺失/损坏并从云端模板补全
/// </summary>
public sealed partial class GeneralSettingsViewModel
{
	private string beamNgDirectory = string.Empty;

	private string beamNgStatusText = string.Empty;

	private bool isBeamNgDirectoryValid;

	private bool isBeamNgDirectoryBusy;

	private string gameConfigPathText = string.Empty;

	private string gameConfigStatusText = string.Empty;

	private bool isGameConfigBusy;

	private bool autoRepairGameConfig = true;

	private RelayCommand? autoDetectBeamNgDirectoryCommand;

	private RelayCommand? browseBeamNgDirectoryCommand;

	private RelayCommand? openBeamNgDirectoryCommand;

	private AsyncRelayCommand? checkAndRepairGameConfigCommand;

	private RelayCommand? openGameConfigDirectoryCommand;

	/// <summary>当前生效的 BeamNG.drive 安装目录。</summary>
	public string BeamNgDirectory
	{
		get => beamNgDirectory;
		set
		{
			if (beamNgDirectory != value)
			{
				beamNgDirectory = value;
				OnPropertyChanged("BeamNgDirectory");
			}
		}
	}

	public string BeamNgStatusText
	{
		get => beamNgStatusText;
		set
		{
			if (beamNgStatusText != value)
			{
				beamNgStatusText = value;
				OnPropertyChanged("BeamNgStatusText");
			}
		}
	}

	public bool IsBeamNgDirectoryValid
	{
		get => isBeamNgDirectoryValid;
		set
		{
			if (isBeamNgDirectoryValid != value)
			{
				isBeamNgDirectoryValid = value;
				OnPropertyChanged("IsBeamNgDirectoryValid");
			}
		}
	}

	public bool IsBeamNgDirectoryBusy
	{
		get => isBeamNgDirectoryBusy;
		set
		{
			if (isBeamNgDirectoryBusy != value)
			{
				isBeamNgDirectoryBusy = value;
				OnPropertyChanged("IsBeamNgDirectoryBusy");
			}
		}
	}

	/// <summary>必需配置文件所在目录（&lt;userpath&gt;/settings）。</summary>
	public string GameConfigPathText
	{
		get => gameConfigPathText;
		set
		{
			if (gameConfigPathText != value)
			{
				gameConfigPathText = value;
				OnPropertyChanged("GameConfigPathText");
			}
		}
	}

	public string GameConfigStatusText
	{
		get => gameConfigStatusText;
		set
		{
			if (gameConfigStatusText != value)
			{
				gameConfigStatusText = value;
				OnPropertyChanged("GameConfigStatusText");
			}
		}
	}

	public bool IsGameConfigBusy
	{
		get => isGameConfigBusy;
		set
		{
			if (isGameConfigBusy != value)
			{
				isGameConfigBusy = value;
				OnPropertyChanged("IsGameConfigBusy");
			}
		}
	}

	/// <summary>启动游戏前自动检查并补全必需配置文件。</summary>
	public bool AutoRepairGameConfig
	{
		get => autoRepairGameConfig;
		set
		{
			if (autoRepairGameConfig != value)
			{
				autoRepairGameConfig = value;
				AppSettings.Current.AutoRepairGameConfig = value;
				AppSettings.Current.Save();
				OnPropertyChanged("AutoRepairGameConfig");
			}
		}
	}

	/// <summary>每个必需文件的检查明细（界面列表）。</summary>
	public ObservableCollection<GameConfigFileItem> GameConfigFiles { get; } =
		new ObservableCollection<GameConfigFileItem>();

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand AutoDetectBeamNgDirectoryCommand =>
		autoDetectBeamNgDirectoryCommand ?? (autoDetectBeamNgDirectoryCommand = new RelayCommand(AutoDetectBeamNgDirectory));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand BrowseBeamNgDirectoryCommand =>
		browseBeamNgDirectoryCommand ?? (browseBeamNgDirectoryCommand = new RelayCommand(BrowseBeamNgDirectory));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand OpenBeamNgDirectoryCommand =>
		openBeamNgDirectoryCommand ?? (openBeamNgDirectoryCommand = new RelayCommand(OpenBeamNgDirectory));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IAsyncRelayCommand CheckAndRepairGameConfigCommand =>
		checkAndRepairGameConfigCommand ?? (checkAndRepairGameConfigCommand = new AsyncRelayCommand(CheckAndRepairGameConfigAsync));

	[GeneratedCode("HandWritten", "1.0.0.0")]
	public IRelayCommand OpenGameConfigDirectoryCommand =>
		openGameConfigDirectoryCommand ?? (openGameConfigDirectoryCommand = new RelayCommand(OpenGameConfigDirectory));

	/// <summary>构造后与设置加载时都要刷新，保证界面显示的是当前真实状态。</summary>
	private void InitializeBeamNgSection()
	{
		AutoRepairGameConfig = AppSettings.Current.AutoRepairGameConfig;
		RefreshBeamNgDirectoryState();
		RefreshGameConfigState();
	}

	/// <summary>把当前设置里的目录状态刷到界面（不探测磁盘之外的东西）。</summary>
	public void RefreshBeamNgDirectoryState()
	{
		var app = AppSettings.Current;
		BeamNgDirectory = app.GameDirectory ?? string.Empty;
		IsBeamNgDirectoryValid = AppSettings.IsBeamNgInstall(BeamNgDirectory);
		BeamNgStatusText = IsBeamNgDirectoryValid
			? string.Format(Strings.Settings_GameInstallFoundFormat, BeamNgDirectory)
			: Strings.Settings_GameInstallMissing;
	}

	/// <summary>刷新配置文件目录与明细列表。</summary>
	public void RefreshGameConfigState()
	{
		var service = new ConfigRepairService(AppSettings.Current);
		GameConfigPathText = service.SettingsDirectory;

		GameConfigFiles.Clear();
		foreach (var status in service.Inspect())
		{
			GameConfigFiles.Add(new GameConfigFileItem(status));
		}
		GameConfigStatusText = GameConfigFiles.Count > 0
			? string.Format(Strings.Settings_GameConfigCheckedFormat, GameConfigFiles.Count)
			: Strings.Settings_GameConfigNotCheckedYet;
	}

	/// <summary>自动识别：多策略探测本机 BeamNG.drive，命中即写入设置并立即可用。</summary>
	private void AutoDetectBeamNgDirectory()
	{
		if (IsBeamNgDirectoryBusy) return;
		IsBeamNgDirectoryBusy = true;
		try
		{
			var found = AppSettings.DetectGameDirectoryCandidates();
			if (found.Count == 0)
			{
				BeamNgStatusText = Strings.Settings_GameInstallDetectFailed;
				statusService.Report(Strings.Settings_GameInstallDetectFailed);
				IsBeamNgDirectoryValid = false;
				return;
			}
			ApplyGameDirectory(found[0]);
			statusService.Report(string.Format(Strings.Settings_GameInstallAppliedFormat, found[0]));
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Auto detect BeamNG directory failed.");
			statusService.Report(Strings.Settings_GameInstallDetectFailed);
		}
		finally
		{
			IsBeamNgDirectoryBusy = false;
		}
	}

	/// <summary>手动选择安装目录（必须是含 BeamNG.drive.exe 的那一层）。</summary>
	private void BrowseBeamNgDirectory()
	{
		if (IsBeamNgDirectoryBusy) return;
		try
		{
			string? picked = filePickerService.PickFolder(Strings.Settings_GameInstallLabel, BeamNgDirectory);
			if (string.IsNullOrWhiteSpace(picked)) return;

			string dir = picked!.Trim().Trim('"').Replace('/', '\\').TrimEnd('\\');
			if (!AppSettings.IsBeamNgInstall(dir))
			{
				// 用户可能选到了 Bin64 之类的子目录，往上找一层再试
				string? parent = Path.GetDirectoryName(dir);
				if (parent != null && AppSettings.IsBeamNgInstall(parent))
				{
					dir = parent;
				}
				else
				{
					statusService.Report(Strings.Settings_GameInstallInvalid);
					IsBeamNgDirectoryValid = false;
					BeamNgStatusText = Strings.Settings_GameInstallInvalid;
					return;
				}
			}

			ApplyGameDirectory(dir);
			statusService.Report(string.Format(Strings.Settings_GameInstallAppliedFormat, dir));
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Pick BeamNG directory failed.");
			statusService.Report(Strings.Settings_GameInstallInvalid);
		}
	}

	/// <summary>
	/// 写入 StartRide 设置并保存。因为全应用共用 AppSettings.Current，
	/// 保存后主页启动按钮、联机一键流程立刻用新目录，无需重启启动器。
	/// </summary>
	private void ApplyGameDirectory(string directory)
	{
		var app = AppSettings.Current;
		app.GameDirectory = directory;
		app.Save();

		BeamNgDirectory = directory;
		IsBeamNgDirectoryValid = AppSettings.IsBeamNgInstall(directory);
		BeamNgStatusText = string.Format(Strings.Settings_GameInstallFoundFormat, directory);
		RefreshGameConfigState();
	}

	private void OpenBeamNgDirectory()
	{
		if (string.IsNullOrWhiteSpace(BeamNgDirectory)) return;
		instanceFolderService.TryOpen(BeamNgDirectory);
	}

	private void OpenGameConfigDirectory()
	{
		try
		{
			string dir = new ConfigRepairService(AppSettings.Current).SettingsDirectory;
			if (Directory.Exists(dir))
			{
				instanceFolderService.TryOpen(dir);
				return;
			}
			// 目录还不存在（游戏没启动过）→ 打开它的上一级，方便用户自己看
			string? parent = Path.GetDirectoryName(dir);
			if (parent != null && Directory.Exists(parent))
			{
				instanceFolderService.TryOpen(parent);
				return;
			}
			statusService.Report(Strings.Settings_GameInstallMissing);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Open game config directory failed.");
		}
	}

	#region 切换游戏目录对话框（复用现有对话框 UI，数据源换成 BeamNG 安装目录）

	private bool isBeamNgDirectorySwitchMode;

	private static string NormalizeSwitchPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		return path!.Trim().Trim('"').Replace('/', '\\').TrimEnd('\\');
	}

	/// <summary>
	/// 打开"切换游戏目录"对话框前，把列表内容换成 BeamNG 安装目录候选：
	/// 自动识别出来的所有候选 + 当前设置值（保证当前项一定在列表里）。
	/// 这样对话框里不会再出现 .minecraft 这种 Minecraft 残留目录。
	/// </summary>
	internal void PrepareBeamNgDirectorySwitchDialog()
	{
		isBeamNgDirectorySwitchMode = true;

		string current = NormalizeSwitchPath(AppSettings.Current.GameDirectory);
		var candidates = new List<string>();

		foreach (string found in AppSettings.DetectGameDirectoryCandidates())
		{
			string normalized = NormalizeSwitchPath(found);
			if (normalized.Length > 0 && !candidates.Contains(normalized, StringComparer.OrdinalIgnoreCase))
			{
				candidates.Add(normalized);
			}
		}

		// 当前值即使没被探测到，也要显示出来（用户自己手动选过的目录）
		if (current.Length > 0 && !candidates.Contains(current, StringComparer.OrdinalIgnoreCase))
		{
			candidates.Insert(0, current);
		}

		MinecraftDirectories.Clear();
		foreach (string path in candidates)
		{
			bool isCurrent = string.Equals(path, current, StringComparison.OrdinalIgnoreCase);
			string displayName = isCurrent
				? Strings.Settings_GameInstallItemCurrent
				: Strings.Settings_GameInstallItemCandidate;
			// canRemove: 当前使用的目录不允许在列表里删掉
			MinecraftDirectories.Add(new SettingsMinecraftDirectoryItem(displayName, path, isAvailable: true, canRemove: !isCurrent));
		}
	}

	/// <summary>对话框判定"当前目录"用：BeamNG 模式取游戏安装目录，否则维持原有 Minecraft 目录语义。</summary>
	private string ResolveSwitchDialogCurrentDirectory()
	{
		return isBeamNgDirectorySwitchMode
			? NormalizeSwitchPath(AppSettings.Current.GameDirectory)
			: MinecraftDirectory;
	}

	/// <summary>对话框点"确定"后真正执行切换：BeamNG 模式写游戏安装目录，否则走原有的 Minecraft 目录切换。</summary>
	private Task<bool> ApplySwitchDialogDirectoryAsync(string directoryPath)
	{
		if (!isBeamNgDirectorySwitchMode)
		{
			return SelectMinecraftDirectoryAsync(directoryPath);
		}
		return Task.FromResult(ApplyBeamNgDirectoryFromSwitchDialog(directoryPath));
	}

	private bool ApplyBeamNgDirectoryFromSwitchDialog(string directoryPath)
	{
		string path = NormalizeSwitchPath(directoryPath);
		if (!AppSettings.IsBeamNgInstall(path))
		{
			statusService.Report(Strings.Settings_GameInstallInvalid);
			return false;
		}
		// 复用同一套写入逻辑：写 AppSettings.Current 并立即持久化，全应用马上生效
		ApplyGameDirectory(path);
		statusService.Report(string.Format(Strings.Settings_GameInstallAppliedFormat, path));
		return true;
	}

	#endregion

	/// <summary>
	/// 检查并补全（游戏文件体检）：
	///   能自动补的 —— 模组目录、联机模组包（重新打包安装）、必需配置文件（云端模板）
	///   补不了的   —— 游戏本体运行文件，明确标注"需 Steam 校验"，不做假动作
	/// </summary>
	private async Task CheckAndRepairGameConfigAsync()
	{
		if (IsGameConfigBusy) return;
		IsGameConfigBusy = true;
		GameConfigStatusText = Strings.Settings_GameHealthRepairing;
		try
		{
			var service = new GameFileHealthService(AppSettings.Current);
			var result = await Task.Run(() => service.RepairAsync(new ApiService(), null)).ConfigureAwait(true);

			GameConfigFiles.Clear();
			foreach (var item in result.Items)
			{
				GameConfigFiles.Add(new GameConfigFileItem(item));
			}
			foreach (var status in result.ConfigFiles)
			{
				GameConfigFiles.Add(new GameConfigFileItem(status));
			}

			int fixedTotal = result.Fixed + result.ConfigRepaired;
			int pending = result.Missing + result.NeedsSteam;
			if (pending > 0 && fixedTotal == 0)
			{
				GameConfigStatusText = string.Format(Strings.Settings_GameHealthProblemFormat, pending);
			}
			else
			{
				GameConfigStatusText = string.Format(
					Strings.Settings_GameHealthCheckedFormat,
					result.Checked + result.ConfigChecked,
					fixedTotal);
			}
			if (result.Error != null && fixedTotal == 0 && pending == 0)
			{
				GameConfigStatusText = string.Format(Strings.Settings_GameConfigFailedFormat, result.Error);
			}
			statusService.Report(GameConfigStatusText);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Check and repair game files failed.");
			GameConfigStatusText = string.Format(Strings.Settings_GameConfigFailedFormat, ex.Message);
			statusService.Report(GameConfigStatusText);
		}
		finally
		{
			IsGameConfigBusy = false;
		}
	}
}

/// <summary>配置文件列表项（界面绑定用）。</summary>
public sealed class GameConfigFileItem
{
	public string DisplayName { get; }

	public string RelativePath { get; }

	public string StateText { get; }

	public string Detail { get; }

	public bool IsProblem { get; }

	/// <summary>游戏文件体检项（与配置项共用同一套界面模板）。</summary>
	public GameConfigFileItem(GameFileHealthItem item)
	{
		DisplayName = item.DisplayName;
		RelativePath = item.RelativePath;
		Detail = item.Detail;
		StateText = item.State switch
		{
			GameFileState.Ok => Strings.Settings_GameConfigStatusOk,
			GameFileState.Missing => Strings.Settings_GameConfigStatusMissing,
			GameFileState.Fixed => Strings.Settings_GameHealthStatusFixed,
			GameFileState.NeedsSteam => Strings.Settings_GameHealthStatusNeedSteam,
			GameFileState.NotApplicable => Strings.Settings_GameHealthStatusNotApplicable,
			GameFileState.Failed => Strings.Settings_GameHealthStatusFailed,
			_ => Strings.Settings_GameHealthStatusNotApplicable,
		};
		IsProblem = item.IsProblem;
	}

	public GameConfigFileItem(ConfigFileStatus status)
	{
		DisplayName = status.DisplayName;
		RelativePath = status.RelativePath;
		Detail = status.Detail;
		StateText = status.State switch
		{
			ConfigFileState.Ok => Strings.Settings_GameConfigStatusOk,
			ConfigFileState.Missing => Strings.Settings_GameConfigStatusMissing,
			ConfigFileState.Broken => Strings.Settings_GameConfigStatusBroken,
			ConfigFileState.Repaired => Strings.Settings_GameConfigStatusRepaired,
			_ => Strings.Settings_GameConfigStatusOffline,
		};
		IsProblem = status.State != ConfigFileState.Ok && status.State != ConfigFileState.Repaired;
	}
}
