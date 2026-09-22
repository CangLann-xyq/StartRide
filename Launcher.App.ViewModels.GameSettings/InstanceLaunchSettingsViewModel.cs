using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.App.Resources;
using Launcher.App.Utilities;
using Launcher.App.ViewModels.Settings;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.GameSettings;

public sealed class InstanceLaunchSettingsViewModel : GameSettingsDetailsSectionViewModelBase, IDisposable
{
	private sealed record InstanceLaunchSettingsSnapshot(LaunchSettingsMode Mode, bool CheckFilesBeforeLaunch, bool AutoRepairMissingFiles, bool MinimizeLauncherAfterLaunch, bool LaunchFullScreen, string AutoJoinServerAddress, string PreLaunchCommand, bool WaitForPreLaunchCommand, string PostExitCommand, string JvmArguments, string GameArguments, MemorySettingsMode MemorySettingsMode, int MemoryMb)
	{
		public static InstanceLaunchSettingsSnapshot Capture(GameInstance instance)
		{
			return new InstanceLaunchSettingsSnapshot(instance.LaunchSettingsMode, instance.CheckFilesBeforeLaunch, instance.AutoRepairMissingFiles, instance.MinimizeLauncherAfterLaunch, instance.LaunchFullScreen, instance.AutoJoinServerAddress, instance.PreLaunchCommand, instance.WaitForPreLaunchCommand, instance.PostExitCommand, instance.JvmArguments, instance.GameArguments, instance.MemorySettingsMode, instance.MemoryMb);
		}

		public bool Matches(LaunchSettingsMode mode, bool checkFilesBeforeLaunch, bool autoRepairMissingFiles, bool minimizeLauncherAfterLaunch, bool launchFullScreen, string autoJoinServerAddress, string preLaunchCommand, bool waitForPreLaunchCommand, string postExitCommand, string jvmArguments, string gameArguments, MemorySettingsMode memorySettingsMode, int memoryMb)
		{
			if (Mode == mode && CheckFilesBeforeLaunch == checkFilesBeforeLaunch && AutoRepairMissingFiles == autoRepairMissingFiles && MinimizeLauncherAfterLaunch == minimizeLauncherAfterLaunch && LaunchFullScreen == launchFullScreen && string.Equals(AutoJoinServerAddress, autoJoinServerAddress, StringComparison.Ordinal) && string.Equals(PreLaunchCommand, preLaunchCommand, StringComparison.Ordinal) && WaitForPreLaunchCommand == waitForPreLaunchCommand && string.Equals(PostExitCommand, postExitCommand, StringComparison.Ordinal) && string.Equals(JvmArguments, jvmArguments, StringComparison.Ordinal) && string.Equals(GameArguments, gameArguments, StringComparison.Ordinal) && MemorySettingsMode == memorySettingsMode)
			{
				return MemoryMb == memoryMb;
			}
			return false;
		}

		public void Restore(GameInstance instance)
		{
			instance.LaunchSettingsMode = Mode;
			instance.CheckFilesBeforeLaunch = CheckFilesBeforeLaunch;
			instance.AutoRepairMissingFiles = AutoRepairMissingFiles;
			instance.MinimizeLauncherAfterLaunch = MinimizeLauncherAfterLaunch;
			instance.LaunchFullScreen = LaunchFullScreen;
			instance.AutoJoinServerAddress = AutoJoinServerAddress;
			instance.PreLaunchCommand = PreLaunchCommand;
			instance.WaitForPreLaunchCommand = WaitForPreLaunchCommand;
			instance.PostExitCommand = PostExitCommand;
			instance.JvmArguments = JvmArguments;
			instance.GameArguments = GameArguments;
			instance.MemorySettingsMode = MemorySettingsMode;
			instance.MemoryMb = MemoryMb;
		}
	}

	private static readonly TimeSpan SaveMergeDelay = TimeSpan.FromMilliseconds(100.0);

	private readonly ISystemMemoryService systemMemoryService;

	private readonly IModService modService;

	private readonly InstanceSettingsPersistenceCoordinator persistence;

	private CancellationTokenSource? modCountRefreshCancellation;

	private LauncherSettings globalSettings = new LauncherSettings();

	private GameInstance? selectedInstance;

	private bool suppressAutoSave;

	private int enabledModCount;

	[ObservableProperty]
	private bool launchCheckFilesBeforeLaunchEnabled;

	[ObservableProperty]
	private bool launchAutoRepairMissingFilesEnabled;

	[ObservableProperty]
	private bool launchMinimizeLauncherAfterLaunchEnabled;

	[ObservableProperty]
	private bool launchFullScreenEnabled;

	[ObservableProperty]
	private string launchAutoJoinServerAddress = string.Empty;

	[ObservableProperty]
	private string launchPreLaunchCommand = string.Empty;

	[ObservableProperty]
	private bool launchWaitForPreLaunchCommand = true;

	[ObservableProperty]
	private string launchPostExitCommand = string.Empty;

	[ObservableProperty]
	private string launchJvmArguments = string.Empty;

	[ObservableProperty]
	private string launchGameArguments = string.Empty;

	[ObservableProperty]
	private SettingsMemoryModeOption? selectedMemoryModeOption;

	[ObservableProperty]
	private double memoryMb = 4096.0;

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
	private GameSettingsLaunchSettingsModeOption? selectedLaunchSettingsModeOption;

	public IReadOnlyList<GameSettingsLaunchSettingsModeOption> LaunchSettingsModeOptions { get; } = new global::_003C_003Ez__ReadOnlyArray<GameSettingsLaunchSettingsModeOption>(new GameSettingsLaunchSettingsModeOption[2]
	{
		new GameSettingsLaunchSettingsModeOption(LaunchSettingsMode.UseGlobal, Strings.GameSettings_LaunchSettingsModeUseGlobal),
		new GameSettingsLaunchSettingsModeOption(LaunchSettingsMode.PerInstance, Strings.GameSettings_LaunchSettingsModePerInstance)
	});

	public ObservableCollection<SettingsMemoryModeOption> MemoryModeOptions { get; } = new ObservableCollection<SettingsMemoryModeOption>();

	public bool AreLaunchSettingsOverridesEnabled
	{
		get
		{
			LaunchSettingsMode? launchSettingsMode = SelectedLaunchSettingsModeOption?.Mode;
			if (launchSettingsMode.HasValue)
			{
				return launchSettingsMode == LaunchSettingsMode.PerInstance;
			}
			return false;
		}
	}

	public bool CanEditAutoRepairMissingFiles
	{
		get
		{
			if (AreLaunchSettingsOverridesEnabled)
			{
				return LaunchCheckFilesBeforeLaunchEnabled;
			}
			return false;
		}
	}

	public bool IsMemorySliderEnabled
	{
		get
		{
			if (AreLaunchSettingsOverridesEnabled)
			{
				MemorySettingsMode? memorySettingsMode = SelectedMemoryModeOption?.Mode;
				if (memorySettingsMode.HasValue)
				{
					return memorySettingsMode == MemorySettingsMode.Manual;
				}
				return false;
			}
			return false;
		}
	}

	public bool IsMemorySliderVisible
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

	public string MemoryText => MemorySizeTextFormatter.FormatGb(MemoryMb);

	public string AutomaticMemoryText => MemorySizeTextFormatter.FormatGb(AutomaticMemoryMb);

	public string SystemMemorySummaryText => string.Format(Strings.Settings_SystemMemorySummaryFormat, SystemAvailableMemoryText, SystemTotalMemoryText);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool LaunchCheckFilesBeforeLaunchEnabled
	{
		get
		{
			return launchCheckFilesBeforeLaunchEnabled;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(launchCheckFilesBeforeLaunchEnabled, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchCheckFilesBeforeLaunchEnabled);
				launchCheckFilesBeforeLaunchEnabled = value;
				OnLaunchCheckFilesBeforeLaunchEnabledChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchCheckFilesBeforeLaunchEnabled);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool LaunchAutoRepairMissingFilesEnabled
	{
		get
		{
			return launchAutoRepairMissingFilesEnabled;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(launchAutoRepairMissingFilesEnabled, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchAutoRepairMissingFilesEnabled);
				launchAutoRepairMissingFilesEnabled = value;
				OnLaunchAutoRepairMissingFilesEnabledChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchAutoRepairMissingFilesEnabled);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool LaunchMinimizeLauncherAfterLaunchEnabled
	{
		get
		{
			return launchMinimizeLauncherAfterLaunchEnabled;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(launchMinimizeLauncherAfterLaunchEnabled, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchMinimizeLauncherAfterLaunchEnabled);
				launchMinimizeLauncherAfterLaunchEnabled = value;
				OnLaunchMinimizeLauncherAfterLaunchEnabledChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchMinimizeLauncherAfterLaunchEnabled);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool LaunchFullScreenEnabled
	{
		get
		{
			return launchFullScreenEnabled;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(launchFullScreenEnabled, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchFullScreenEnabled);
				launchFullScreenEnabled = value;
				OnLaunchFullScreenEnabledChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchFullScreenEnabled);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LaunchAutoJoinServerAddress
	{
		get
		{
			return launchAutoJoinServerAddress;
		}
		[MemberNotNull("launchAutoJoinServerAddress")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(launchAutoJoinServerAddress, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchAutoJoinServerAddress);
				launchAutoJoinServerAddress = value;
				OnLaunchAutoJoinServerAddressChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchAutoJoinServerAddress);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LaunchPreLaunchCommand
	{
		get
		{
			return launchPreLaunchCommand;
		}
		[MemberNotNull("launchPreLaunchCommand")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(launchPreLaunchCommand, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchPreLaunchCommand);
				launchPreLaunchCommand = value;
				OnLaunchPreLaunchCommandChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchPreLaunchCommand);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool LaunchWaitForPreLaunchCommand
	{
		get
		{
			return launchWaitForPreLaunchCommand;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(launchWaitForPreLaunchCommand, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchWaitForPreLaunchCommand);
				launchWaitForPreLaunchCommand = value;
				OnLaunchWaitForPreLaunchCommandChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchWaitForPreLaunchCommand);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LaunchPostExitCommand
	{
		get
		{
			return launchPostExitCommand;
		}
		[MemberNotNull("launchPostExitCommand")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(launchPostExitCommand, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchPostExitCommand);
				launchPostExitCommand = value;
				OnLaunchPostExitCommandChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchPostExitCommand);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LaunchJvmArguments
	{
		get
		{
			return launchJvmArguments;
		}
		[MemberNotNull("launchJvmArguments")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(launchJvmArguments, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchJvmArguments);
				launchJvmArguments = value;
				OnLaunchJvmArgumentsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchJvmArguments);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LaunchGameArguments
	{
		get
		{
			return launchGameArguments;
		}
		[MemberNotNull("launchGameArguments")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(launchGameArguments, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchGameArguments);
				launchGameArguments = value;
				OnLaunchGameArgumentsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchGameArguments);
			}
		}
	}

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
	public double MemoryMb
	{
		get
		{
			return memoryMb;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(memoryMb, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MemoryMb);
				memoryMb = value;
				OnMemoryMbChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MemoryMb);
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
				OnMemorySliderMaximumMbChanged(value);
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
	public GameSettingsLaunchSettingsModeOption? SelectedLaunchSettingsModeOption
	{
		get
		{
			return selectedLaunchSettingsModeOption;
		}
		set
		{
			if (!EqualityComparer<GameSettingsLaunchSettingsModeOption>.Default.Equals(selectedLaunchSettingsModeOption, value))
			{
				GameSettingsLaunchSettingsModeOption oldValue = selectedLaunchSettingsModeOption;
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedLaunchSettingsModeOption);
				selectedLaunchSettingsModeOption = value;
				OnSelectedLaunchSettingsModeOptionChanged(oldValue, value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedLaunchSettingsModeOption);
			}
		}
	}

	internal InstanceLaunchSettingsViewModel(ISystemMemoryService systemMemoryService, IModService modService, InstanceSettingsPersistenceCoordinator persistence)
	{
		this.systemMemoryService = systemMemoryService;
		this.modService = modService;
		this.persistence = persistence;
		MemoryModeOptions.Add(new SettingsMemoryModeOption(MemorySettingsMode.Auto, Strings.Settings_MemoryModeAuto));
		MemoryModeOptions.Add(new SettingsMemoryModeOption(MemorySettingsMode.Manual, Strings.Settings_MemoryModeManual));
		SelectedMemoryModeOption = MemoryModeOptions[0];
		SelectedLaunchSettingsModeOption = LaunchSettingsModeOptions[0];
	}

	public void PrimeFromSettings(LauncherSettings launcherSettings)
	{
		globalSettings = launcherSettings;
		RefreshSystemMemorySnapshot();
		LaunchSettingsMode? launchSettingsMode = SelectedLaunchSettingsModeOption?.Mode;
		if (launchSettingsMode.HasValue && launchSettingsMode.GetValueOrDefault() == LaunchSettingsMode.UseGlobal)
		{
			ApplyGlobalLaunchSettingsToEditor();
		}
	}

	public void SetSelectedInstance(GameInstance? instance)
	{
		selectedInstance = instance;
		CancelModCountRefresh();
		enabledModCount = 0;
		LoadEditorFromInstance();
		RefreshSystemMemorySnapshot();
		RefreshEnabledModCountAsync(instance);
	}

	public override Task OnSectionActivatedAsync()
	{
		RefreshSystemMemorySnapshot();
		return Task.CompletedTask;
	}

	public void Dispose()
	{
		CancelModCountRefresh();
	}

	public void RefreshSystemMemorySnapshot()
	{
		try
		{
			SystemMemorySnapshot snapshot = systemMemoryService.GetSnapshot();
			int totalMemoryMb = MemoryAllocationCalculator.BytesToMegabytes(snapshot.TotalMemoryBytes);
			int num = MemoryAllocationCalculator.BytesToMegabytes(snapshot.AvailableMemoryBytes);
			MemorySliderMaximumMb = MemoryAllocationCalculator.CalculateMaximumMemoryMb(totalMemoryMb);
			AutomaticMemoryMb = MemoryAllocationCalculator.CalculateAutomaticMemoryMb(snapshot, selectedInstance?.Loader ?? LoaderKind.Vanilla, enabledModCount);
			SystemTotalMemoryText = MemorySizeTextFormatter.Format(totalMemoryMb);
			SystemAvailableMemoryText = MemorySizeTextFormatter.FormatGb(num);
		}
		catch (Exception)
		{
			MemorySliderMaximumMb = 32768;
			AutomaticMemoryMb = NormalizeMemoryValue(MemoryMb);
			SystemTotalMemoryText = Strings.Settings_MemoryUnavailable;
			SystemAvailableMemoryText = Strings.Settings_MemoryUnavailable;
		}
	}

	private void RestoreRequiredSelection(Action restore)
	{
		bool flag = suppressAutoSave;
		suppressAutoSave = true;
		try
		{
			restore();
		}
		finally
		{
			suppressAutoSave = flag;
		}
	}

	private void LoadEditorFromInstance()
	{
		suppressAutoSave = true;
		try
		{
			LaunchSettingsMode launchSettingsMode = selectedInstance?.LaunchSettingsMode ?? LaunchSettingsMode.UseGlobal;
			SelectedLaunchSettingsModeOption = ResolveLaunchSettingsModeOption(launchSettingsMode);
			if (launchSettingsMode == LaunchSettingsMode.UseGlobal)
			{
				ApplyGlobalLaunchSettingsToEditorCore();
				return;
			}
			SelectedMemoryModeOption = ResolveMemoryModeOption(selectedInstance?.MemorySettingsMode ?? MemorySettingsMode.Manual);
			MemoryMb = NormalizeMemoryValue(selectedInstance?.MemoryMb ?? 4096);
			LaunchCheckFilesBeforeLaunchEnabled = selectedInstance?.CheckFilesBeforeLaunch ?? true;
			LaunchAutoRepairMissingFilesEnabled = selectedInstance?.AutoRepairMissingFiles ?? true;
			LaunchMinimizeLauncherAfterLaunchEnabled = selectedInstance?.MinimizeLauncherAfterLaunch ?? false;
			LaunchFullScreenEnabled = selectedInstance?.LaunchFullScreen ?? false;
			LaunchAutoJoinServerAddress = selectedInstance?.AutoJoinServerAddress ?? string.Empty;
			LaunchPreLaunchCommand = selectedInstance?.PreLaunchCommand ?? string.Empty;
			LaunchWaitForPreLaunchCommand = selectedInstance?.WaitForPreLaunchCommand ?? true;
			LaunchPostExitCommand = selectedInstance?.PostExitCommand ?? string.Empty;
			LaunchJvmArguments = selectedInstance?.JvmArguments ?? string.Empty;
			LaunchGameArguments = selectedInstance?.GameArguments ?? string.Empty;
		}
		finally
		{
			suppressAutoSave = false;
		}
	}

	private void ApplyGlobalLaunchSettingsToEditor()
	{
		suppressAutoSave = true;
		try
		{
			ApplyGlobalLaunchSettingsToEditorCore();
		}
		finally
		{
			suppressAutoSave = false;
		}
	}

	private void ApplyGlobalLaunchSettingsToEditorCore()
	{
		LaunchCheckFilesBeforeLaunchEnabled = globalSettings.DefaultCheckFilesBeforeLaunch;
		LaunchAutoRepairMissingFilesEnabled = globalSettings.DefaultAutoRepairMissingFiles;
		LaunchMinimizeLauncherAfterLaunchEnabled = globalSettings.DefaultMinimizeLauncherAfterLaunch;
		LaunchFullScreenEnabled = globalSettings.DefaultLaunchFullScreen;
		LaunchAutoJoinServerAddress = globalSettings.DefaultAutoJoinServerAddress;
		LaunchPreLaunchCommand = globalSettings.DefaultPreLaunchCommand;
		LaunchWaitForPreLaunchCommand = globalSettings.DefaultWaitForPreLaunchCommand;
		LaunchPostExitCommand = globalSettings.DefaultPostExitCommand;
		LaunchJvmArguments = globalSettings.DefaultJvmArguments;
		LaunchGameArguments = globalSettings.DefaultGameArguments;
		SelectedMemoryModeOption = ResolveMemoryModeOption(globalSettings.DefaultMemorySettingsMode);
		MemoryMb = NormalizeMemoryValue(globalSettings.DefaultMemoryMb);
	}

	private void ApplyLaunchCheckDependency(bool checkFilesBeforeLaunch)
	{
		if (LaunchAutoRepairMissingFilesEnabled == checkFilesBeforeLaunch)
		{
			return;
		}
		suppressAutoSave = true;
		try
		{
			LaunchAutoRepairMissingFilesEnabled = checkFilesBeforeLaunch;
		}
		finally
		{
			suppressAutoSave = false;
		}
	}

	private async Task RefreshEnabledModCountAsync(GameInstance? instance)
	{
		if (instance == null || instance.Loader == LoaderKind.Vanilla)
		{
			enabledModCount = 0;
			RefreshSystemMemorySnapshot();
			return;
		}
		CancellationTokenSource cancellation = (modCountRefreshCancellation = new CancellationTokenSource());
		try
		{
			IReadOnlyList<LocalMod> source = await modService.GetModsAsync(instance, cancellation.Token);
			if (modCountRefreshCancellation == cancellation && string.Equals(selectedInstance?.Id, instance.Id, StringComparison.Ordinal))
			{
				enabledModCount = source.Count((LocalMod mod) => mod.IsEnabled);
				RefreshSystemMemorySnapshot();
			}
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
		}
		catch (Exception)
		{
			if (string.Equals(selectedInstance?.Id, instance.Id, StringComparison.Ordinal))
			{
				enabledModCount = 0;
				RefreshSystemMemorySnapshot();
			}
		}
		finally
		{
			if (Interlocked.CompareExchange(ref modCountRefreshCancellation, null, cancellation) == cancellation)
			{
				cancellation.Dispose();
			}
		}
	}

	private void CancelModCountRefresh()
	{
		CancellationTokenSource cancellationTokenSource = Interlocked.Exchange(ref modCountRefreshCancellation, null);
		if (cancellationTokenSource != null)
		{
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
		}
	}

	private SettingsMemoryModeOption ResolveMemoryModeOption(MemorySettingsMode mode)
	{
		return MemoryModeOptions.FirstOrDefault((SettingsMemoryModeOption option) => option.Mode == mode) ?? MemoryModeOptions[0];
	}

	private GameSettingsLaunchSettingsModeOption ResolveLaunchSettingsModeOption(LaunchSettingsMode mode)
	{
		return LaunchSettingsModeOptions.FirstOrDefault((GameSettingsLaunchSettingsModeOption option) => option.Mode == mode) ?? LaunchSettingsModeOptions[0];
	}

	private int NormalizeMemoryValue(double value)
	{
		return MemoryAllocationCalculator.NormalizeRecordedMemoryMb(value, MemorySliderMaximumMb);
	}

	private static string NormalizeSettingText(string? value)
	{
		return value?.Trim() ?? string.Empty;
	}

	private void ScheduleSaveUnlessSuppressed()
	{
		if (!suppressAutoSave)
		{
			ScheduleSave();
		}
	}

	private void ScheduleSave()
	{
		GameInstance gameInstance = selectedInstance;
		GameSettingsLaunchSettingsModeOption gameSettingsLaunchSettingsModeOption = SelectedLaunchSettingsModeOption;
		if (gameInstance == null || gameSettingsLaunchSettingsModeOption == null)
		{
			return;
		}
		LaunchSettingsMode mode = gameSettingsLaunchSettingsModeOption.Mode;
		if (mode != LaunchSettingsMode.PerInstance || SelectedMemoryModeOption != null)
		{
			bool checkFilesBeforeLaunch = LaunchCheckFilesBeforeLaunchEnabled;
			bool autoRepairMissingFiles = LaunchAutoRepairMissingFilesEnabled;
			bool minimizeLauncherAfterLaunch = LaunchMinimizeLauncherAfterLaunchEnabled;
			bool launchFullScreen = LaunchFullScreenEnabled;
			string autoJoinServerAddress = NormalizeSettingText(LaunchAutoJoinServerAddress);
			string preLaunchCommand = NormalizeSettingText(LaunchPreLaunchCommand);
			bool waitForPreLaunchCommand = LaunchWaitForPreLaunchCommand;
			string postExitCommand = NormalizeSettingText(LaunchPostExitCommand);
			string jvmArguments = NormalizeSettingText(LaunchJvmArguments);
			string gameArguments = NormalizeSettingText(LaunchGameArguments);
			MemorySettingsMode memorySettingsMode = ((mode == LaunchSettingsMode.UseGlobal) ? globalSettings.DefaultMemorySettingsMode : SelectedMemoryModeOption.Mode);
			int memory = ((mode == LaunchSettingsMode.UseGlobal) ? NormalizeMemoryValue(globalSettings.DefaultMemoryMb) : NormalizeMemoryValue(MemoryMb));
			persistence.Schedule("launch", gameInstance, (GameInstance target) => ApplyMutation(target, mode, checkFilesBeforeLaunch, autoRepairMissingFiles, minimizeLauncherAfterLaunch, launchFullScreen, autoJoinServerAddress, preLaunchCommand, waitForPreLaunchCommand, postExitCommand, jvmArguments, gameArguments, memorySettingsMode, memory), LoadEditorFromInstance, SaveMergeDelay);
		}
	}

	private static Action? ApplyMutation(GameInstance instance, LaunchSettingsMode mode, bool checkFilesBeforeLaunch, bool autoRepairMissingFiles, bool minimizeLauncherAfterLaunch, bool launchFullScreen, string autoJoinServerAddress, string preLaunchCommand, bool waitForPreLaunchCommand, string postExitCommand, string jvmArguments, string gameArguments, MemorySettingsMode memorySettingsMode, int memoryMb)
	{
		InstanceLaunchSettingsSnapshot original = InstanceLaunchSettingsSnapshot.Capture(instance);
		if (original.Matches(mode, checkFilesBeforeLaunch, autoRepairMissingFiles, minimizeLauncherAfterLaunch, launchFullScreen, autoJoinServerAddress, preLaunchCommand, waitForPreLaunchCommand, postExitCommand, jvmArguments, gameArguments, memorySettingsMode, memoryMb))
		{
			return null;
		}
		instance.LaunchSettingsMode = mode;
		instance.CheckFilesBeforeLaunch = checkFilesBeforeLaunch;
		instance.AutoRepairMissingFiles = autoRepairMissingFiles;
		instance.MinimizeLauncherAfterLaunch = minimizeLauncherAfterLaunch;
		instance.LaunchFullScreen = launchFullScreen;
		instance.AutoJoinServerAddress = autoJoinServerAddress;
		instance.PreLaunchCommand = preLaunchCommand;
		instance.WaitForPreLaunchCommand = waitForPreLaunchCommand;
		instance.PostExitCommand = postExitCommand;
		instance.JvmArguments = jvmArguments;
		instance.GameArguments = gameArguments;
		instance.MemorySettingsMode = memorySettingsMode;
		instance.MemoryMb = memoryMb;
		return delegate
		{
			original.Restore(instance);
		};
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLaunchCheckFilesBeforeLaunchEnabledChanged(bool value)
	{
		if (!suppressAutoSave)
		{
			ApplyLaunchCheckDependency(value);
		}
		OnPropertyChanged("CanEditAutoRepairMissingFiles");
		ScheduleSaveUnlessSuppressed();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLaunchAutoRepairMissingFilesEnabledChanged(bool value)
	{
		ScheduleSaveUnlessSuppressed();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLaunchMinimizeLauncherAfterLaunchEnabledChanged(bool value)
	{
		ScheduleSaveUnlessSuppressed();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLaunchFullScreenEnabledChanged(bool value)
	{
		ScheduleSaveUnlessSuppressed();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLaunchAutoJoinServerAddressChanged(string value)
	{
		ScheduleSaveUnlessSuppressed();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLaunchPreLaunchCommandChanged(string value)
	{
		ScheduleSaveUnlessSuppressed();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLaunchWaitForPreLaunchCommandChanged(bool value)
	{
		ScheduleSaveUnlessSuppressed();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLaunchPostExitCommandChanged(string value)
	{
		ScheduleSaveUnlessSuppressed();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLaunchJvmArgumentsChanged(string value)
	{
		ScheduleSaveUnlessSuppressed();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLaunchGameArgumentsChanged(string value)
	{
		ScheduleSaveUnlessSuppressed();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedMemoryModeOptionChanged(SettingsMemoryModeOption? oldValue, SettingsMemoryModeOption? newValue)
	{
		if (newValue == null)
		{
			if (selectedInstance != null)
			{
				RestoreRequiredSelection(delegate
				{
					InstanceLaunchSettingsViewModel instanceLaunchSettingsViewModel = this;
					SettingsMemoryModeOption settingsMemoryModeOption = oldValue;
					if (settingsMemoryModeOption == null)
					{
						InstanceLaunchSettingsViewModel instanceLaunchSettingsViewModel2 = this;
						LaunchSettingsMode? launchSettingsMode = SelectedLaunchSettingsModeOption?.Mode;
						settingsMemoryModeOption = instanceLaunchSettingsViewModel2.ResolveMemoryModeOption((launchSettingsMode.HasValue && launchSettingsMode.GetValueOrDefault() == LaunchSettingsMode.UseGlobal) ? globalSettings.DefaultMemorySettingsMode : selectedInstance.MemorySettingsMode);
					}
					instanceLaunchSettingsViewModel.SelectedMemoryModeOption = settingsMemoryModeOption;
				});
			}
			else
			{
				OnPropertyChanged("IsMemorySliderEnabled");
				OnPropertyChanged("IsMemorySliderVisible");
				OnPropertyChanged("IsAutomaticMemorySummaryVisible");
			}
		}
		else
		{
			OnPropertyChanged("IsMemorySliderEnabled");
			OnPropertyChanged("IsMemorySliderVisible");
			OnPropertyChanged("IsAutomaticMemorySummaryVisible");
			ScheduleSaveUnlessSuppressed();
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnMemoryMbChanged(double value)
	{
		double num = Math.Clamp(value, MemorySliderMinimumMb, MemorySliderMaximumMb);
		if (Math.Abs(num - value) > double.Epsilon)
		{
			MemoryMb = num;
			return;
		}
		OnPropertyChanged("MemoryText");
		ScheduleSaveUnlessSuppressed();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnMemorySliderMaximumMbChanged(int value)
	{
		if (MemoryMb > (double)value)
		{
			MemoryMb = value;
		}
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
	private void OnSelectedLaunchSettingsModeOptionChanged(GameSettingsLaunchSettingsModeOption? oldValue, GameSettingsLaunchSettingsModeOption? newValue)
	{
		if (newValue == null)
		{
			if (selectedInstance != null)
			{
				RestoreRequiredSelection(delegate
				{
					SelectedLaunchSettingsModeOption = oldValue ?? ResolveLaunchSettingsModeOption(selectedInstance.LaunchSettingsMode);
				});
			}
			else
			{
				OnPropertyChanged("AreLaunchSettingsOverridesEnabled");
				OnPropertyChanged("CanEditAutoRepairMissingFiles");
				OnPropertyChanged("IsMemorySliderEnabled");
			}
			return;
		}
		OnPropertyChanged("AreLaunchSettingsOverridesEnabled");
		OnPropertyChanged("CanEditAutoRepairMissingFiles");
		OnPropertyChanged("IsMemorySliderEnabled");
		if (!suppressAutoSave)
		{
			if (newValue.Mode == LaunchSettingsMode.UseGlobal)
			{
				ApplyGlobalLaunchSettingsToEditor();
			}
			ScheduleSave();
		}
	}
}
