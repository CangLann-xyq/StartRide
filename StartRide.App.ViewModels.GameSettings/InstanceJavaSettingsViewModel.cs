using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Settings;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class InstanceJavaSettingsViewModel : GameSettingsDetailsSectionViewModelBase, IDisposable
{
	private static readonly TimeSpan SaveMergeDelay = TimeSpan.FromMilliseconds(100.0);

	private readonly InstanceSettingsPersistenceCoordinator persistence;

	private LauncherSettings globalSettings = new LauncherSettings();

	private GameInstance? selectedInstance;

	private bool suppressAutoSave;

	[ObservableProperty]
	private GameSettingsLaunchSettingsModeOption? selectedInstanceJavaSettingsModeOption;

	public IReadOnlyList<GameSettingsLaunchSettingsModeOption> LaunchSettingsModeOptions { get; } = new global::_003C_003Ez__ReadOnlyArray<GameSettingsLaunchSettingsModeOption>(new GameSettingsLaunchSettingsModeOption[2]
	{
		new GameSettingsLaunchSettingsModeOption(LaunchSettingsMode.UseGlobal, Strings.GameSettings_LaunchSettingsModeUseGlobal),
		new GameSettingsLaunchSettingsModeOption(LaunchSettingsMode.PerInstance, Strings.GameSettings_LaunchSettingsModePerInstance)
	});

	public JavaSettingsEditorViewModel InstanceJavaSettings { get; }

	public ObservableCollection<SettingsJavaSelectionOption> InstanceJavaSelectionOptions => InstanceJavaSettings.JavaSelectionOptions;

	public ObservableCollection<SettingsJavaRuntimeItem> InstanceJavaRuntimes => InstanceJavaSettings.JavaRuntimes;

	public SettingsJavaSelectionOption? SelectedInstanceJavaSelectionOption
	{
		get
		{
			return InstanceJavaSettings.SelectedJavaSelectionOption;
		}
		set
		{
			InstanceJavaSettings.SelectedJavaSelectionOption = value;
		}
	}

	public SettingsJavaRuntimeItem? SelectedInstanceJavaRuntime
	{
		get
		{
			return InstanceJavaSettings.SelectedJavaRuntime;
		}
		set
		{
			InstanceJavaSettings.SelectedJavaRuntime = value;
		}
	}

	public bool AreInstanceJavaSettingsOverridesEnabled
	{
		get
		{
			LaunchSettingsMode? launchSettingsMode = SelectedInstanceJavaSettingsModeOption?.Mode;
			if (launchSettingsMode.HasValue)
			{
				return launchSettingsMode == LaunchSettingsMode.PerInstance;
			}
			return false;
		}
	}

	public bool IsInstanceJavaManualSelection => InstanceJavaSettings.IsJavaManualSelection;

	public bool CanInteractWithInstanceJavaRuntimeList
	{
		get
		{
			if (AreInstanceJavaSettingsOverridesEnabled)
			{
				return IsInstanceJavaManualSelection;
			}
			return false;
		}
	}

	public bool IsInstanceJavaRuntimeScanRunning => InstanceJavaSettings.IsJavaRuntimeScanRunning;

	public string InstanceJavaRuntimeListMessage => InstanceJavaSettings.JavaRuntimeListMessage;

	public bool HasInstanceJavaRuntimeListMessage => InstanceJavaSettings.HasJavaRuntimeListMessage;

	public IAsyncRelayCommand RefreshInstanceJavaRuntimesCommand => InstanceJavaSettings.RefreshJavaRuntimesCommand;

	public IAsyncRelayCommand ImportInstanceJavaRuntimeCommand => InstanceJavaSettings.ImportJavaRuntimeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public GameSettingsLaunchSettingsModeOption? SelectedInstanceJavaSettingsModeOption
	{
		get
		{
			return selectedInstanceJavaSettingsModeOption;
		}
		set
		{
			if (!EqualityComparer<GameSettingsLaunchSettingsModeOption>.Default.Equals(selectedInstanceJavaSettingsModeOption, value))
			{
				GameSettingsLaunchSettingsModeOption oldValue = selectedInstanceJavaSettingsModeOption;
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedInstanceJavaSettingsModeOption);
				selectedInstanceJavaSettingsModeOption = value;
				OnSelectedInstanceJavaSettingsModeOptionChanged(oldValue, value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedInstanceJavaSettingsModeOption);
			}
		}
	}

	internal InstanceJavaSettingsViewModel(InstanceSettingsPersistenceCoordinator persistence, IJavaRuntimeDiscoveryService javaRuntimeDiscoveryService, IStatusService statusService, IFilePickerService filePickerService, IFloatingMessageService floatingMessageService)
	{
		this.persistence = persistence;
		InstanceJavaSettings = new JavaSettingsEditorViewModel(javaRuntimeDiscoveryService, statusService, filePickerService, floatingMessageService, () => globalSettings.MinecraftDirectory);
		InstanceJavaSettings.IsEditorEnabled = false;
		InstanceJavaSettings.PropertyChanged += InstanceJavaSettings_PropertyChanged;
		InstanceJavaSettings.JavaSelectionChanged += InstanceJavaSettings_JavaSelectionChanged;
		SelectedInstanceJavaSettingsModeOption = LaunchSettingsModeOptions[0];
	}

	public void PrimeFromSettings(LauncherSettings launcherSettings)
	{
		globalSettings = launcherSettings;
		LaunchSettingsMode? launchSettingsMode = SelectedInstanceJavaSettingsModeOption?.Mode;
		if (launchSettingsMode.HasValue && launchSettingsMode.GetValueOrDefault() == LaunchSettingsMode.UseGlobal)
		{
			LoadEditorFromInstance();
		}
	}

	public void SetSelectedInstance(GameInstance? instance)
	{
		selectedInstance = instance;
		LoadEditorFromInstance();
		if (InstanceJavaRuntimes.Count == 0)
		{
			InstanceJavaSettings.RefreshJavaRuntimesForDisplayAsync();
		}
	}

	public override Task OnSectionActivatedAsync()
	{
		return InstanceJavaSettings.RefreshJavaRuntimesForDisplayAsync();
	}

	public void Dispose()
	{
		InstanceJavaSettings.PropertyChanged -= InstanceJavaSettings_PropertyChanged;
		InstanceJavaSettings.JavaSelectionChanged -= InstanceJavaSettings_JavaSelectionChanged;
	}

	private void InstanceJavaSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
		case "SelectedJavaSelectionOption":
			OnPropertyChanged("SelectedInstanceJavaSelectionOption");
			break;
		case "SelectedJavaRuntime":
			OnPropertyChanged("SelectedInstanceJavaRuntime");
			break;
		case "IsJavaRuntimeScanRunning":
			OnPropertyChanged("IsInstanceJavaRuntimeScanRunning");
			break;
		case "JavaRuntimeListMessage":
			OnPropertyChanged("InstanceJavaRuntimeListMessage");
			OnPropertyChanged("HasInstanceJavaRuntimeListMessage");
			break;
		case "HasJavaRuntimeListMessage":
			OnPropertyChanged("HasInstanceJavaRuntimeListMessage");
			break;
		case "IsJavaManualSelection":
			OnPropertyChanged("IsInstanceJavaManualSelection");
			OnPropertyChanged("CanInteractWithInstanceJavaRuntimeList");
			break;
		}
	}

	private void InstanceJavaSettings_JavaSelectionChanged(object? sender, EventArgs e)
	{
		if (!suppressAutoSave)
		{
			ScheduleSave();
		}
	}

	private void ScheduleSave()
	{
		GameInstance gameInstance = selectedInstance;
		GameSettingsLaunchSettingsModeOption gameSettingsLaunchSettingsModeOption = SelectedInstanceJavaSettingsModeOption;
		if (gameInstance == null || gameSettingsLaunchSettingsModeOption == null)
		{
			return;
		}
		LaunchSettingsMode javaSettingsMode = gameSettingsLaunchSettingsModeOption.Mode;
		if (javaSettingsMode == LaunchSettingsMode.PerInstance && InstanceJavaSettings.SelectedJavaSelectionOption == null)
		{
			return;
		}
		JavaSelectionMode javaSelectionMode = ((javaSettingsMode == LaunchSettingsMode.UseGlobal) ? gameInstance.JavaSelectionMode : InstanceJavaSettings.SelectedMode);
		string selectedJavaExecutablePath = ((javaSettingsMode == LaunchSettingsMode.UseGlobal) ? gameInstance.SelectedJavaExecutablePath : NormalizeExecutablePath(InstanceJavaSettings.SelectedExecutablePath));
		persistence.Schedule("java", gameInstance, delegate(GameInstance target)
		{
			LaunchSettingsMode originalMode = target.JavaSettingsMode;
			JavaSelectionMode originalSelectionMode = target.JavaSelectionMode;
			string originalExecutablePath = target.SelectedJavaExecutablePath;
			if (originalMode == javaSettingsMode && originalSelectionMode == javaSelectionMode && string.Equals(originalExecutablePath, selectedJavaExecutablePath, StringComparison.Ordinal))
			{
				return (Action?)null;
			}
			target.JavaSettingsMode = javaSettingsMode;
			target.JavaSelectionMode = javaSelectionMode;
			target.SelectedJavaExecutablePath = selectedJavaExecutablePath;
			return delegate
			{
				target.JavaSettingsMode = originalMode;
				target.JavaSelectionMode = originalSelectionMode;
				target.SelectedJavaExecutablePath = originalExecutablePath;
			};
		}, LoadEditorFromInstance, SaveMergeDelay);
	}

	private void LoadEditorFromInstance()
	{
		suppressAutoSave = true;
		try
		{
			LaunchSettingsMode mode = selectedInstance?.JavaSettingsMode ?? LaunchSettingsMode.UseGlobal;
			SelectedInstanceJavaSettingsModeOption = ResolveLaunchSettingsModeOption(mode);
			LoadJavaSelectionForMode();
		}
		finally
		{
			suppressAutoSave = false;
		}
	}

	private void LoadJavaSelectionForMode()
	{
		LaunchSettingsMode? launchSettingsMode = SelectedInstanceJavaSettingsModeOption?.Mode;
		bool flag = launchSettingsMode.HasValue && launchSettingsMode == LaunchSettingsMode.PerInstance;
		JavaSelectionMode mode = ((!flag) ? globalSettings.JavaSelectionMode : (selectedInstance?.JavaSelectionMode ?? JavaSelectionMode.Auto));
		string selectedJavaExecutablePath = ((!flag) ? globalSettings.SelectedJavaExecutablePath : selectedInstance?.SelectedJavaExecutablePath);
		InstanceJavaSettings.IsEditorEnabled = flag;
		InstanceJavaSettings.LoadSelection(mode, selectedJavaExecutablePath);
		OnPropertyChanged("IsInstanceJavaManualSelection");
		OnPropertyChanged("CanInteractWithInstanceJavaRuntimeList");
	}

	private GameSettingsLaunchSettingsModeOption ResolveLaunchSettingsModeOption(LaunchSettingsMode mode)
	{
		return LaunchSettingsModeOptions.FirstOrDefault((GameSettingsLaunchSettingsModeOption option) => option.Mode == mode) ?? LaunchSettingsModeOptions[0];
	}

	private void RestoreRequiredModeSelection(GameSettingsLaunchSettingsModeOption? previousSelection)
	{
		bool flag = suppressAutoSave;
		suppressAutoSave = true;
		try
		{
			SelectedInstanceJavaSettingsModeOption = previousSelection ?? ResolveLaunchSettingsModeOption(selectedInstance?.JavaSettingsMode ?? LaunchSettingsMode.UseGlobal);
		}
		finally
		{
			suppressAutoSave = flag;
		}
	}

	private static string? NormalizeExecutablePath(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		return null;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedInstanceJavaSettingsModeOptionChanged(GameSettingsLaunchSettingsModeOption? oldValue, GameSettingsLaunchSettingsModeOption? newValue)
	{
		if (newValue == null)
		{
			if (selectedInstance != null)
			{
				RestoreRequiredModeSelection(oldValue);
				return;
			}
			OnPropertyChanged("AreInstanceJavaSettingsOverridesEnabled");
			OnPropertyChanged("CanInteractWithInstanceJavaRuntimeList");
			return;
		}
		OnPropertyChanged("AreInstanceJavaSettingsOverridesEnabled");
		OnPropertyChanged("CanInteractWithInstanceJavaRuntimeList");
		if (!suppressAutoSave)
		{
			LoadJavaSelectionForMode();
			ScheduleSave();
			InstanceJavaSettings.RefreshJavaRuntimesForDisplayAsync();
		}
	}
}
