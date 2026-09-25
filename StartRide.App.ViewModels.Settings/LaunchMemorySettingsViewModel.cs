using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Resources;
using StartRide.App.Utilities;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Settings;

public sealed partial class LaunchMemorySettingsViewModel : SettingsSectionViewModelBase
{
	private readonly ISystemMemoryService systemMemoryService;

	private bool synchronizingLaunchCheck;

	[ObservableProperty]
	private SettingsMemoryModeOption? selectedMemoryModeOption;

	[ObservableProperty]
	private double defaultMemoryMb = 4096.0;

	[ObservableProperty]
	private int memorySliderMinimumMb = 1024;

	[ObservableProperty]
	private int memorySliderMaximumMb = 32768;

	[ObservableProperty]
	private string systemTotalMemoryText = string.Empty;

	[ObservableProperty]
	private string systemAvailableMemoryText = string.Empty;

	[ObservableProperty]
	private int automaticMemoryMb = 4096;

	[ObservableProperty]
	private bool defaultCheckFilesBeforeLaunch = true;

	[ObservableProperty]
	private bool defaultAutoRepairMissingFiles = true;

	[ObservableProperty]
	private bool defaultMinimizeLauncherAfterLaunch;

	[ObservableProperty]
	private bool defaultLaunchFullScreen;

	[ObservableProperty]
	private string defaultAutoJoinServerAddress = string.Empty;

	[ObservableProperty]
	private string defaultPreLaunchCommand = string.Empty;

	[ObservableProperty]
	private bool defaultWaitForPreLaunchCommand = true;

	[ObservableProperty]
	private string defaultPostExitCommand = string.Empty;

	[ObservableProperty]
	private string defaultJvmArguments = string.Empty;

	[ObservableProperty]
	private string defaultGameArguments = string.Empty;

	public ObservableCollection<SettingsMemoryModeOption> MemoryModeOptions { get; }

	public bool IsMemorySliderEnabled
	{
		get
		{
			MemorySettingsMode? memorySettingsMode = SelectedMemoryModeOption?.Mode;
			if (memorySettingsMode.HasValue)
			{
				return memorySettingsMode == MemorySettingsMode.Manual;
			}
			return false;
		}
	}

	public bool IsMemorySliderVisible => IsMemorySliderEnabled;

	public bool IsAutomaticMemorySummaryVisible
	{
		get
		{
			MemorySettingsMode? memorySettingsMode = SelectedMemoryModeOption?.Mode;
			if (memorySettingsMode.HasValue)
			{
				return memorySettingsMode.GetValueOrDefault() == MemorySettingsMode.Auto;
			}
			return false;
		}
	}

	public string DefaultMemoryText => MemorySizeTextFormatter.FormatGb(DefaultMemoryMb);

	public string AutomaticMemoryText => MemorySizeTextFormatter.FormatGb(AutomaticMemoryMb);

	public string SystemMemorySummaryText => string.Format(Strings.Settings_SystemMemorySummaryFormat, SystemAvailableMemoryText, SystemTotalMemoryText);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsMemoryModeOption? SelectedMemoryModeOption
	{
		get
		{
			return selectedMemoryModeOption;
		}
		set
		{
			if (!EqualityComparer<SettingsMemoryModeOption>.Default.Equals(selectedMemoryModeOption, value))
			{
				SettingsMemoryModeOption oldValue = selectedMemoryModeOption;
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedMemoryModeOption);
				selectedMemoryModeOption = value;
				OnSelectedMemoryModeOptionChanged(oldValue, value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedMemoryModeOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double DefaultMemoryMb
	{
		get
		{
			return defaultMemoryMb;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(defaultMemoryMb, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DefaultMemoryMb);
				defaultMemoryMb = value;
				OnDefaultMemoryMbChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DefaultMemoryMb);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int MemorySliderMinimumMb
	{
		get
		{
			return memorySliderMinimumMb;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(memorySliderMinimumMb, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MemorySliderMinimumMb);
				memorySliderMinimumMb = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MemorySliderMinimumMb);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int MemorySliderMaximumMb
	{
		get
		{
			return memorySliderMaximumMb;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(memorySliderMaximumMb, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MemorySliderMaximumMb);
				memorySliderMaximumMb = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MemorySliderMaximumMb);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string SystemTotalMemoryText
	{
		get
		{
			return systemTotalMemoryText;
		}
		[MemberNotNull("systemTotalMemoryText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(systemTotalMemoryText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SystemTotalMemoryText);
				systemTotalMemoryText = value;
				OnSystemTotalMemoryTextChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SystemTotalMemoryText);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string SystemAvailableMemoryText
	{
		get
		{
			return systemAvailableMemoryText;
		}
		[MemberNotNull("systemAvailableMemoryText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(systemAvailableMemoryText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SystemAvailableMemoryText);
				systemAvailableMemoryText = value;
				OnSystemAvailableMemoryTextChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SystemAvailableMemoryText);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int AutomaticMemoryMb
	{
		get
		{
			return automaticMemoryMb;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(automaticMemoryMb, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AutomaticMemoryMb);
				automaticMemoryMb = value;
				OnAutomaticMemoryMbChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AutomaticMemoryMb);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool DefaultCheckFilesBeforeLaunch
	{
		get
		{
			return defaultCheckFilesBeforeLaunch;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(defaultCheckFilesBeforeLaunch, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DefaultCheckFilesBeforeLaunch);
				defaultCheckFilesBeforeLaunch = value;
				OnDefaultCheckFilesBeforeLaunchChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DefaultCheckFilesBeforeLaunch);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool DefaultAutoRepairMissingFiles
	{
		get
		{
			return defaultAutoRepairMissingFiles;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(defaultAutoRepairMissingFiles, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DefaultAutoRepairMissingFiles);
				defaultAutoRepairMissingFiles = value;
				OnDefaultAutoRepairMissingFilesChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DefaultAutoRepairMissingFiles);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool DefaultMinimizeLauncherAfterLaunch
	{
		get
		{
			return defaultMinimizeLauncherAfterLaunch;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(defaultMinimizeLauncherAfterLaunch, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DefaultMinimizeLauncherAfterLaunch);
				defaultMinimizeLauncherAfterLaunch = value;
				OnDefaultMinimizeLauncherAfterLaunchChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DefaultMinimizeLauncherAfterLaunch);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool DefaultLaunchFullScreen
	{
		get
		{
			return defaultLaunchFullScreen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(defaultLaunchFullScreen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DefaultLaunchFullScreen);
				defaultLaunchFullScreen = value;
				OnDefaultLaunchFullScreenChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DefaultLaunchFullScreen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string DefaultAutoJoinServerAddress
	{
		get
		{
			return defaultAutoJoinServerAddress;
		}
		[MemberNotNull("defaultAutoJoinServerAddress")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(defaultAutoJoinServerAddress, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DefaultAutoJoinServerAddress);
				defaultAutoJoinServerAddress = value;
				OnDefaultAutoJoinServerAddressChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DefaultAutoJoinServerAddress);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string DefaultPreLaunchCommand
	{
		get
		{
			return defaultPreLaunchCommand;
		}
		[MemberNotNull("defaultPreLaunchCommand")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(defaultPreLaunchCommand, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DefaultPreLaunchCommand);
				defaultPreLaunchCommand = value;
				OnDefaultPreLaunchCommandChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DefaultPreLaunchCommand);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool DefaultWaitForPreLaunchCommand
	{
		get
		{
			return defaultWaitForPreLaunchCommand;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(defaultWaitForPreLaunchCommand, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DefaultWaitForPreLaunchCommand);
				defaultWaitForPreLaunchCommand = value;
				OnDefaultWaitForPreLaunchCommandChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DefaultWaitForPreLaunchCommand);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string DefaultPostExitCommand
	{
		get
		{
			return defaultPostExitCommand;
		}
		[MemberNotNull("defaultPostExitCommand")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(defaultPostExitCommand, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DefaultPostExitCommand);
				defaultPostExitCommand = value;
				OnDefaultPostExitCommandChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DefaultPostExitCommand);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string DefaultJvmArguments
	{
		get
		{
			return defaultJvmArguments;
		}
		[MemberNotNull("defaultJvmArguments")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(defaultJvmArguments, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DefaultJvmArguments);
				defaultJvmArguments = value;
				OnDefaultJvmArgumentsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DefaultJvmArguments);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string DefaultGameArguments
	{
		get
		{
			return defaultGameArguments;
		}
		[MemberNotNull("defaultGameArguments")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(defaultGameArguments, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DefaultGameArguments);
				defaultGameArguments = value;
				OnDefaultGameArgumentsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DefaultGameArguments);
			}
		}
	}

	public event EventHandler? LaunchDefaultsChanged;

	internal LaunchMemorySettingsViewModel(SettingsPersistenceCoordinator persistence, ISystemMemoryService systemMemoryService)
		: base(persistence)
	{
		this.systemMemoryService = systemMemoryService;
		MemoryModeOptions = new ObservableCollection<SettingsMemoryModeOption>
		{
			new SettingsMemoryModeOption(MemorySettingsMode.Auto, Strings.Settings_MemoryModeAuto),
			new SettingsMemoryModeOption(MemorySettingsMode.Manual, Strings.Settings_MemoryModeManual)
		};
		selectedMemoryModeOption = MemoryModeOptions[0];
	}

	public void Load(LauncherSettings settings)
	{
		RefreshSystemMemorySnapshot();
		LoadState(delegate
		{
			SelectedMemoryModeOption = MemoryModeOptions.FirstOrDefault((SettingsMemoryModeOption option) => option.Mode == settings.DefaultMemorySettingsMode) ?? MemoryModeOptions[0];
			DefaultMemoryMb = NormalizeMemoryValue(settings.DefaultMemoryMb);
			DefaultCheckFilesBeforeLaunch = settings.DefaultCheckFilesBeforeLaunch;
			DefaultAutoRepairMissingFiles = settings.DefaultAutoRepairMissingFiles;
			DefaultMinimizeLauncherAfterLaunch = settings.DefaultMinimizeLauncherAfterLaunch;
			DefaultLaunchFullScreen = settings.DefaultLaunchFullScreen;
			DefaultAutoJoinServerAddress = settings.DefaultAutoJoinServerAddress;
			DefaultPreLaunchCommand = settings.DefaultPreLaunchCommand;
			DefaultWaitForPreLaunchCommand = settings.DefaultWaitForPreLaunchCommand;
			DefaultPostExitCommand = settings.DefaultPostExitCommand;
			DefaultJvmArguments = settings.DefaultJvmArguments;
			DefaultGameArguments = settings.DefaultGameArguments;
		});
		// BeamNG 侧的内存上限是真实作用到进程上的，值也必须跟着这里走
		InitializeStartRideMemory();
	}

	public void RefreshSystemMemorySnapshot()
	{
		try
		{
			SystemMemorySnapshot snapshot = systemMemoryService.GetSnapshot();
			int num = MemoryAllocationCalculator.BytesToMegabytes(snapshot.TotalMemoryBytes);
			int num2 = MemoryAllocationCalculator.BytesToMegabytes(snapshot.AvailableMemoryBytes);
			MemorySliderMaximumMb = MemoryAllocationCalculator.CalculateMaximumMemoryMb(num);
			AutomaticMemoryMb = MemoryAllocationCalculator.CalculateAutomaticMemoryMb(snapshot);
			SystemTotalMemoryText = MemorySizeTextFormatter.Format(num);
			SystemAvailableMemoryText = MemorySizeTextFormatter.FormatGb(num2);
		}
		catch (Exception)
		{
			MemorySliderMaximumMb = 32768;
			AutomaticMemoryMb = NormalizeMemoryValue(base.Settings.DefaultMemoryMb);
			SystemTotalMemoryText = Strings.Settings_MemoryUnavailable;
			SystemAvailableMemoryText = Strings.Settings_MemoryUnavailable;
		}
	}

	private void PersistAndNotify()
	{
		if (base.CanPersist && !synchronizingLaunchCheck && SelectedMemoryModeOption != null)
		{
			Persist(delegate(LauncherSettings settings)
			{
				settings.DefaultMemorySettingsMode = SelectedMemoryModeOption.Mode;
				settings.DefaultMemoryMb = NormalizeMemoryValue(DefaultMemoryMb);
				settings.DefaultCheckFilesBeforeLaunch = DefaultCheckFilesBeforeLaunch;
				settings.DefaultAutoRepairMissingFiles = DefaultAutoRepairMissingFiles;
				settings.DefaultMinimizeLauncherAfterLaunch = DefaultMinimizeLauncherAfterLaunch;
				settings.DefaultLaunchFullScreen = DefaultLaunchFullScreen;
				settings.DefaultAutoJoinServerAddress = NormalizeText(DefaultAutoJoinServerAddress);
				settings.DefaultPreLaunchCommand = NormalizeText(DefaultPreLaunchCommand);
				settings.DefaultWaitForPreLaunchCommand = DefaultWaitForPreLaunchCommand;
				settings.DefaultPostExitCommand = NormalizeText(DefaultPostExitCommand);
				settings.DefaultJvmArguments = NormalizeText(DefaultJvmArguments);
				settings.DefaultGameArguments = NormalizeText(DefaultGameArguments);
			});
			SyncStartRideMemorySettings();
			// 启动行为开关（检查文件/最小化/全屏/启动前命令/退出后命令/游戏参数）
			// 以前只写进 LauncherSettings，启动流程读的是 AppSettings → 这里同步过去才真生效
			SyncStartRideLaunchBehaviorSettings();
			LaunchDefaultsChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private int NormalizeMemoryValue(double memoryMb)
	{
		return MemoryAllocationCalculator.NormalizeRecordedMemoryMb(memoryMb, MemorySliderMaximumMb);
	}

	private static string NormalizeText(string? value)
	{
		return value?.Trim() ?? string.Empty;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedMemoryModeOptionChanged(SettingsMemoryModeOption? oldValue, SettingsMemoryModeOption? newValue)
	{
		if (newValue == null)
		{
			LoadState(delegate
			{
				SelectedMemoryModeOption = oldValue ?? MemoryModeOptions[0];
			});
			return;
		}
		OnPropertyChanged("IsMemorySliderEnabled");
		OnPropertyChanged("IsMemorySliderVisible");
		OnPropertyChanged("IsAutomaticMemorySummaryVisible");
		PersistAndNotify();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDefaultMemoryMbChanged(double value)
	{
		double num = Math.Clamp(value, MemorySliderMinimumMb, MemorySliderMaximumMb);
		if (Math.Abs(num - value) > double.Epsilon)
		{
			DefaultMemoryMb = num;
			return;
		}
		OnPropertyChanged("DefaultMemoryText");
		PersistAndNotify();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSystemTotalMemoryTextChanged(string value)
	{
		OnPropertyChanged("SystemMemorySummaryText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSystemAvailableMemoryTextChanged(string value)
	{
		OnPropertyChanged("SystemMemorySummaryText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAutomaticMemoryMbChanged(int value)
	{
		OnPropertyChanged("AutomaticMemoryText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDefaultCheckFilesBeforeLaunchChanged(bool value)
	{
		if (base.CanPersist && !synchronizingLaunchCheck && DefaultAutoRepairMissingFiles != value)
		{
			synchronizingLaunchCheck = true;
			DefaultAutoRepairMissingFiles = value;
			synchronizingLaunchCheck = false;
		}
		PersistAndNotify();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDefaultAutoRepairMissingFilesChanged(bool value)
	{
		PersistAndNotify();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDefaultMinimizeLauncherAfterLaunchChanged(bool value)
	{
		PersistAndNotify();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDefaultLaunchFullScreenChanged(bool value)
	{
		PersistAndNotify();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDefaultAutoJoinServerAddressChanged(string value)
	{
		PersistAndNotify();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDefaultPreLaunchCommandChanged(string value)
	{
		PersistAndNotify();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDefaultWaitForPreLaunchCommandChanged(bool value)
	{
		PersistAndNotify();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDefaultPostExitCommandChanged(string value)
	{
		PersistAndNotify();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDefaultJvmArgumentsChanged(string value)
	{
		PersistAndNotify();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDefaultGameArgumentsChanged(string value)
	{
		PersistAndNotify();
	}
}
