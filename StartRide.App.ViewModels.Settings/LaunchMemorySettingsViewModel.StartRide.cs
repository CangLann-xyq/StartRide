using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.Utilities;
using Launcher.Domain.Models;
using StartRide.Core;

namespace StartRide.App.ViewModels.Settings;

public sealed partial class LaunchMemorySettingsViewModel
{
	private readonly StartRideHardwareService hardwareService = new StartRideHardwareService();

	private bool limitGameMemory = true;

	private string memorySlotSummaryText = string.Empty;

	private string memoryTotalSummaryText = string.Empty;

	private bool isHardwareLoading;

	private AsyncRelayCommand? refreshMemoryHardwareCommand;

	public ObservableCollection<MemoryModuleItem> MemoryModules { get; } = new ObservableCollection<MemoryModuleItem>();

	public string MemorySlotSummaryText
	{
		get => memorySlotSummaryText;
		set
		{
			if (memorySlotSummaryText != value)
			{
				memorySlotSummaryText = value;
				OnPropertyChanged("MemorySlotSummaryText");
			}
		}
	}

	public string MemoryTotalSummaryText
	{
		get => memoryTotalSummaryText;
		set
		{
			if (memoryTotalSummaryText != value)
			{
				memoryTotalSummaryText = value;
				OnPropertyChanged("MemoryTotalSummaryText");
			}
		}
	}

	public bool IsHardwareLoading
	{
		get => isHardwareLoading;
		set
		{
			if (isHardwareLoading != value)
			{
				isHardwareLoading = value;
				OnPropertyChanged("IsHardwareLoading");
			}
		}
	}

	public bool HasMemoryModules => MemoryModules.Count > 0;

	public bool LimitGameMemory
	{
		get => limitGameMemory;
		set
		{
			if (limitGameMemory != value)
			{
				limitGameMemory = value;
				OnPropertyChanged("LimitGameMemory");
				SyncStartRideMemorySettings();
			}
		}
	}

	public IAsyncRelayCommand RefreshMemoryHardwareCommand =>
		refreshMemoryHardwareCommand ?? (refreshMemoryHardwareCommand = new AsyncRelayCommand(RefreshMemoryHardwareAsync));

	private void InitializeStartRideMemory()
	{
		try
		{
			LimitGameMemory = AppSettings.Current.LimitGameMemory;
		}
		catch { }

		SyncLaunchBehaviorFromStartRide();

		UpdateMemorySummaryFromSystemService();
		_ = RefreshMemoryHardwareAsync();
	}

	private void SyncLaunchBehaviorFromStartRide()
	{
		try
		{
			var app = AppSettings.Current;
			LoadState(delegate
			{
				DefaultCheckFilesBeforeLaunch = app.CheckFilesBeforeLaunch;
				DefaultAutoRepairMissingFiles = app.AutoRepairGameConfig;
				DefaultMinimizeLauncherAfterLaunch = app.MinimizeToTray;
				DefaultLaunchFullScreen = app.LaunchFullScreen;
				DefaultPreLaunchCommand = app.PreLaunchCommand;
				DefaultWaitForPreLaunchCommand = app.WaitForPreLaunchCommand;
				DefaultPostExitCommand = app.PostExitCommand;
				DefaultGameArguments = app.GameArguments;
			});
		}
		catch { }
	}

	internal void SyncStartRideLaunchBehaviorSettings()
	{
		try
		{
			var app = AppSettings.Current;
			app.CheckFilesBeforeLaunch = DefaultCheckFilesBeforeLaunch;

			app.AutoRepairGameConfig = DefaultAutoRepairMissingFiles;
			app.MinimizeToTray = DefaultMinimizeLauncherAfterLaunch;
			app.LaunchFullScreen = DefaultLaunchFullScreen;
			app.PreLaunchCommand = NormalizeText(DefaultPreLaunchCommand) ?? "";
			app.WaitForPreLaunchCommand = DefaultWaitForPreLaunchCommand;
			app.PostExitCommand = NormalizeText(DefaultPostExitCommand) ?? "";
			app.GameArguments = NormalizeText(DefaultGameArguments) ?? "";
			app.Save();
		}
		catch { }
	}

	private void UpdateMemorySummaryFromSystemService()
	{
		try
		{
			var snapshot = systemMemoryService.GetSnapshot();
			int totalMb = (int)(snapshot.TotalMemoryBytes / 1024L / 1024L);
			int freeMb = (int)(snapshot.AvailableMemoryBytes / 1024L / 1024L);
			MemoryTotalSummaryText = string.Format(
				Strings.Settings_MemoryTotalSummaryFormat,
				MemorySizeTextFormatter.Format(totalMb),
				MemorySizeTextFormatter.FormatGb(freeMb));
		}
		catch { }
	}

	private async Task RefreshMemoryHardwareAsync()
	{
		if (IsHardwareLoading) return;
		IsHardwareLoading = true;
		try
		{
			var snapshot = await Task.Run(() => hardwareService.GetMemorySnapshot()).ConfigureAwait(true);

			MemoryModules.Clear();
			foreach (var module in snapshot.Modules)
			{
				MemoryModules.Add(new MemoryModuleItem(module));
			}

			if (MemoryModules.Count > 0)
			{
				MemorySlotSummaryText = string.Format(
					Strings.Settings_MemorySlotSummaryFormat, snapshot.TotalSlots, snapshot.PopulatedSlots);
			}
			else
			{
				MemorySlotSummaryText = string.IsNullOrWhiteSpace(snapshot.Error)
					? Strings.Settings_MemoryNoModule
					: Strings.Settings_MemoryNoModule + "（" + snapshot.Error + "）";
			}
			OnPropertyChanged("HasMemoryModules");
		}
		catch (Exception)
		{
			MemorySlotSummaryText = Strings.Settings_MemoryNoModule;
		}
		finally
		{
			IsHardwareLoading = false;
		}
	}

	internal void SyncStartRideMemorySettings()
	{
		try
		{
			var app = AppSettings.Current;
			bool automatic = SelectedMemoryModeOption?.Mode != MemorySettingsMode.Manual;
			int mb = automatic ? AutomaticMemoryMb : NormalizeMemoryValue(DefaultMemoryMb);
			if (mb <= 0) mb = 4096;

			app.LimitGameMemory = LimitGameMemory;
			app.MaxMemoryMB = mb;
			app.Save();
		}
		catch { }
	}
}

public sealed class MemoryModuleItem
{
	public string Title { get; }

	public string Subtitle { get; }

	public MemoryModuleItem(MemoryModuleInfo module)
	{
		Title = string.Format(
			Strings.Settings_MemoryModuleFormat,
			module.CapacityText,
			string.IsNullOrWhiteSpace(module.Generation) ? Strings.Settings_MemoryUnknownValue : module.Generation,
			module.SpeedText,
			module.SlotText);
		Subtitle = module.ModelText;
	}
}
