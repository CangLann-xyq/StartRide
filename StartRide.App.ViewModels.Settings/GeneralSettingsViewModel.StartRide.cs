using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using Microsoft.Extensions.Logging;
using StartRide.Core;

namespace StartRide.App.ViewModels.Settings;

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

	public ObservableCollection<GameConfigFileItem> GameConfigFiles { get; } =
		new ObservableCollection<GameConfigFileItem>();

	private bool highlightCaptureEnabled = true;
	private bool highlightAutoRecord = true;
	private bool highlightInSinglePlayer;

	public bool HighlightCaptureEnabled
	{
		get => highlightCaptureEnabled;
		set
		{
			if (highlightCaptureEnabled != value)
			{
				highlightCaptureEnabled = value;
				AppSettings.Current.HighlightCaptureEnabled = value;
				AppSettings.Current.Save();
				OnPropertyChanged("HighlightCaptureEnabled");
				PushHighlightConfig();
			}
		}
	}

	public bool HighlightAutoRecord
	{
		get => highlightAutoRecord;
		set
		{
			if (highlightAutoRecord != value)
			{
				highlightAutoRecord = value;
				AppSettings.Current.HighlightAutoRecord = value;
				AppSettings.Current.Save();
				OnPropertyChanged("HighlightAutoRecord");
				PushHighlightConfig();
			}
		}
	}

	public bool HighlightInSinglePlayer
	{
		get => highlightInSinglePlayer;
		set
		{
			if (highlightInSinglePlayer != value)
			{
				highlightInSinglePlayer = value;
				AppSettings.Current.HighlightInSinglePlayer = value;
				AppSettings.Current.Save();
				OnPropertyChanged("HighlightInSinglePlayer");
				PushHighlightConfig();
			}
		}
	}

	private void PushHighlightConfig()
	{
		try
		{
			HighlightStore.PushModConfig(AppSettings.Current);
		}
		catch
		{
		}
	}

	public IRelayCommand OpenHighlightsFolderCommand =>
		openHighlightsFolderCommand ?? (openHighlightsFolderCommand = new RelayCommand(OpenHighlightsFolder));

	private RelayCommand? openHighlightsFolderCommand;

	private void OpenHighlightsFolder()
	{
		try
		{
			string dir = HighlightStore.ResolveDirectory(AppSettings.Current.ResolveReplaysDirectory());
			if (string.IsNullOrWhiteSpace(dir) || !System.IO.Directory.Exists(dir))
			{
				System.Windows.MessageBox.Show(
					"还没有高光记录。启动游戏并跑一局，这里就会出现截图与记录文件。",
					"StartRide",
					System.Windows.MessageBoxButton.OK,
					System.Windows.MessageBoxImage.Information);
				return;
			}
			System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("无法打开目录：" + ex.Message, "StartRide", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
		}
	}

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

	private void InitializeBeamNgSection()
	{
		AutoRepairGameConfig = AppSettings.Current.AutoRepairGameConfig;
		RefreshBeamNgDirectoryState();
		RefreshGameConfigState();
	}

	public void RefreshBeamNgDirectoryState()
	{
		var app = AppSettings.Current;
		BeamNgDirectory = app.GameDirectory ?? string.Empty;
		IsBeamNgDirectoryValid = AppSettings.IsBeamNgInstall(BeamNgDirectory);
		BeamNgStatusText = IsBeamNgDirectoryValid
			? string.Format(Strings.Settings_GameInstallFoundFormat, BeamNgDirectory)
			: Strings.Settings_GameInstallMissing;
	}

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

	private bool isBeamNgDirectorySwitchMode;

	private static string NormalizeSwitchPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		return path!.Trim().Trim('"').Replace('/', '\\').TrimEnd('\\');
	}

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
			MinecraftDirectories.Add(new SettingsGameDirectoryItem(displayName, path, isAvailable: true, canRemove: !isCurrent));
		}
	}

	private string ResolveSwitchDialogCurrentDirectory()
	{
		return isBeamNgDirectorySwitchMode
			? NormalizeSwitchPath(AppSettings.Current.GameDirectory)
			: MinecraftDirectory;
	}

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
		ApplyGameDirectory(path);
		statusService.Report(string.Format(Strings.Settings_GameInstallAppliedFormat, path));
		return true;
	}

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

public sealed class GameConfigFileItem
{
	public string DisplayName { get; }

	public string RelativePath { get; }

	public string StateText { get; }

	public string Detail { get; }

	public bool IsProblem { get; }

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
