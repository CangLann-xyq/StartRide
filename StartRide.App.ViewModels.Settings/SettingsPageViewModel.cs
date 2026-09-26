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
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Logging;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Download;
using StartRide.App.ViewModels.Shell;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.Settings;

public sealed class SettingsPageViewModel : ObservableObject, IDisposable
{
	private readonly SettingsPersistenceCoordinator persistence;

	private readonly bool ownsPersistence;

	[ObservableProperty]
	private SettingsSectionItem? selectedSection;

	[ObservableProperty]
	private SettingsSectionViewModelBase? currentSectionViewModel;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<SettingsSectionItem?>? selectSectionCommand;

	public ObservableCollection<SettingsSectionItem> Sections { get; }

	internal SettingsPersistenceCoordinator Persistence => persistence;

	public GeneralSettingsViewModel General { get; }

	public DownloadSettingsViewModel Download { get; }

	public LanguageSettingsViewModel Language { get; }

	public LaunchMemorySettingsViewModel LaunchMemory { get; }

	public JavaSettingsViewModel Java { get; }

	public ThemeSettingsViewModel Theme { get; }

	public SettingsFeedbackDialogViewModel Feedback { get; }

	public InfoSettingsViewModel Info { get; }

	public ControlListSettingsViewModel ControlList { get; }

	public string SectionTitle => SelectedSection?.Title ?? Strings.Settings_SectionGeneral;

	public bool IsGeneralSection
	{
		get
		{
			SettingsPageSection? settingsPageSection = SelectedSection?.Section;
			if (settingsPageSection.HasValue)
			{
				return settingsPageSection.GetValueOrDefault() == SettingsPageSection.General;
			}
			return false;
		}
	}

	public bool IsDownloadSection
	{
		get
		{
			SettingsPageSection? settingsPageSection = SelectedSection?.Section;
			if (settingsPageSection.HasValue)
			{
				return settingsPageSection == SettingsPageSection.Download;
			}
			return false;
		}
	}

	public bool IsLanguageSection
	{
		get
		{
			SettingsPageSection? settingsPageSection = SelectedSection?.Section;
			if (settingsPageSection.HasValue)
			{
				return settingsPageSection == SettingsPageSection.Language;
			}
			return false;
		}
	}

	public bool IsLaunchMemorySection
	{
		get
		{
			SettingsPageSection? settingsPageSection = SelectedSection?.Section;
			if (settingsPageSection.HasValue)
			{
				return settingsPageSection == SettingsPageSection.LaunchMemory;
			}
			return false;
		}
	}

	public bool IsJavaSection
	{
		get
		{
			SettingsPageSection? settingsPageSection = SelectedSection?.Section;
			if (settingsPageSection.HasValue)
			{
				return settingsPageSection == SettingsPageSection.Java;
			}
			return false;
		}
	}

	public bool IsThemeSection
	{
		get
		{
			SettingsPageSection? settingsPageSection = SelectedSection?.Section;
			if (settingsPageSection.HasValue)
			{
				return settingsPageSection == SettingsPageSection.Theme;
			}
			return false;
		}
	}

	public bool IsInfoSection
	{
		get
		{
			SettingsPageSection? settingsPageSection = SelectedSection?.Section;
			if (settingsPageSection.HasValue)
			{
				return settingsPageSection == SettingsPageSection.Info;
			}
			return false;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsSectionItem? SelectedSection
	{
		get
		{
			return selectedSection;
		}
		set
		{
			if (!EqualityComparer<SettingsSectionItem>.Default.Equals(selectedSection, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedSection);
				selectedSection = value;
				OnSelectedSectionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedSection);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsSectionViewModelBase? CurrentSectionViewModel
	{
		get
		{
			return currentSectionViewModel;
		}
		set
		{
			if (!EqualityComparer<SettingsSectionViewModelBase>.Default.Equals(currentSectionViewModel, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentSectionViewModel);
				currentSectionViewModel = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentSectionViewModel);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<SettingsSectionItem?> SelectSectionCommand => selectSectionCommand ?? (selectSectionCommand = new RelayCommand<SettingsSectionItem>(SelectSection));

	public event EventHandler? LaunchDefaultsChanged;

	public event EventHandler<SettingsDownloadSourceChangedEventArgs>? DownloadSourceChanged;

	public event EventHandler<SettingsMaximumDownloadConcurrencyChangedEventArgs>? MaximumDownloadConcurrencyChanged;

	public event EventHandler<SettingsDownloadSpeedLimitChangedEventArgs>? DownloadSpeedLimitChanged;

	public event EventHandler<SettingsGameDirectoryChangedEventArgs>? MinecraftDirectoryChanged;

	public SettingsPageViewModel(ISettingsService settingsService, IStatusService statusService, ISystemMemoryService systemMemoryService, IJavaRuntimeDiscoveryService javaRuntimeDiscoveryService, IMinecraftDirectoryFileSystem minecraftDirectoryFileSystem, MinecraftDirectoryManagementService minecraftDirectoryManagementService, IFilePickerService filePickerService, ICustomFileDownloadService customFileDownloadService, IInstanceFolderService instanceFolderService, IFloatingMessageService floatingMessageService, IThemeService themeService, IExternalLinkService externalLinkService, ILauncherUpdateService launcherUpdateService, ILauncherSelfUpdateService launcherSelfUpdateService, IApplicationExitService applicationExitService, IInfoReferenceProjectCatalog infoReferenceProjectCatalog, ILogger<SettingsFeedbackDialogViewModel>? feedbackDialogLogger = null, ILogger<InfoSettingsViewModel>? infoSettingsLogger = null, ILogger<SettingsPageViewModel>? logger = null, ILogger<CustomFileDownloadViewModel>? customFileDownloadLogger = null, DownloadTasksPageViewModel? downloadTasksPage = null, ILauncherLogLevelController? logLevelController = null, LauncherBackgroundViewModel? launcherBackground = null, SettingsPersistenceCoordinator? settingsPersistence = null)
	{
		ILogger<SettingsPageViewModel> logger2 = logger ?? NullLogger<SettingsPageViewModel>.Instance;
		persistence = settingsPersistence ?? new SettingsPersistenceCoordinator(settingsService, statusService, logger2);
		ownsPersistence = settingsPersistence == null;
		General = new GeneralSettingsViewModel(persistence, statusService, filePickerService, instanceFolderService, minecraftDirectoryFileSystem, minecraftDirectoryManagementService, downloadTasksPage, logLevelController, logger2);
		DownloadTasksPageViewModel downloadTasksPage2 = downloadTasksPage ?? new DownloadTasksPageViewModel();
		Download = new DownloadSettingsViewModel(persistence, new CustomFileDownloadViewModel(customFileDownloadService, filePickerService, floatingMessageService, downloadTasksPage2, customFileDownloadLogger));
		Language = new LanguageSettingsViewModel(persistence);
		LaunchMemory = new LaunchMemorySettingsViewModel(persistence, systemMemoryService);
		Java = new JavaSettingsViewModel(persistence, javaRuntimeDiscoveryService, statusService, filePickerService, floatingMessageService, () => General.MinecraftDirectory);
		Theme = new ThemeSettingsViewModel(persistence, themeService, launcherBackground);
		Feedback = new SettingsFeedbackDialogViewModel(statusService, floatingMessageService, externalLinkService, feedbackDialogLogger);
		Info = new InfoSettingsViewModel(persistence, statusService, floatingMessageService, externalLinkService, launcherUpdateService, launcherSelfUpdateService, applicationExitService, infoReferenceProjectCatalog, infoSettingsLogger);
		ControlList = new ControlListSettingsViewModel(persistence);
		Download.DownloadSourceChanged += delegate(object? _, SettingsDownloadSourceChangedEventArgs args)
		{
			DownloadSourceChanged?.Invoke(this, args);
		};
		Download.MaximumDownloadConcurrencyChanged += delegate(object? _, SettingsMaximumDownloadConcurrencyChangedEventArgs args)
		{
			MaximumDownloadConcurrencyChanged?.Invoke(this, args);
		};
		Download.DownloadSpeedLimitChanged += delegate(object? _, SettingsDownloadSpeedLimitChangedEventArgs args)
		{
			DownloadSpeedLimitChanged?.Invoke(this, args);
		};
		General.MinecraftDirectoryChanged += delegate(object? _, SettingsGameDirectoryChangedEventArgs args)
		{
			MinecraftDirectoryChanged?.Invoke(this, args);
		};
		LaunchMemory.LaunchDefaultsChanged += delegate
		{
			LaunchDefaultsChanged?.Invoke(this, EventArgs.Empty);
		};
		Java.LaunchDefaultsChanged += delegate
		{
			LaunchDefaultsChanged?.Invoke(this, EventArgs.Empty);
		};
		Sections = new ObservableCollection<SettingsSectionItem>
		{
			new SettingsSectionItem(SettingsPageSection.General, Strings.Settings_SectionGeneral, "instance_setting_page/general_setting"),
			new SettingsSectionItem(SettingsPageSection.Download, Strings.Settings_SectionDownload, "setting_page/download"),
			new SettingsSectionItem(SettingsPageSection.Language, Strings.Settings_SectionLanguage, "setting_page/earth"),
			new SettingsSectionItem(SettingsPageSection.LaunchMemory, Strings.Settings_SectionLaunchMemory, "instance_setting_page/launch"),
			new SettingsSectionItem(SettingsPageSection.Theme, Strings.Settings_SectionTheme, "setting_page/theme"),
			new SettingsSectionItem(SettingsPageSection.Info, Strings.Settings_SectionInfo, "setting_page/info"),
			new SettingsSectionItem(SettingsPageSection.Feedback, Strings.Settings_SectionFeedback, "setting_page/message")
		};
		SelectedSection = Sections[0];
	}

	public void PrimeFromSettings(LauncherSettings settings)
	{
		persistence.Prime(settings);
		General.Load(settings);
		Download.Load(settings);
		Language.Load(settings);
		LaunchMemory.Load(settings);
		Java.Load(settings);
		Theme.Load(settings);
		Info.Load(settings);
	}

	public void ShowJavaSection()
	{
		SelectSectionCore(Sections.FirstOrDefault((SettingsSectionItem section) => section.Section == SettingsPageSection.Java));
	}

	public Task FlushPendingSettingsAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return persistence.FlushAsync(cancellationToken);
	}

	public void Dispose()
	{
		General.Dispose();
		if (ownsPersistence)
		{
			persistence.Dispose();
		}
	}

	[RelayCommand]
	private void SelectSection(SettingsSectionItem? section)
	{
		SelectSectionCore(section);
	}

	private void SelectSectionCore(SettingsSectionItem? section)
	{
		if (section != null)
		{
			if (section.Section == SettingsPageSection.Feedback)
			{
				Feedback.Open();
			}
			else
			{
				SelectedSection = section;
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedSectionChanged(SettingsSectionItem? value)
	{
		foreach (SettingsSectionItem section in Sections)
		{
			section.IsSelected = section == value;
		}
		OnPropertyChanged("SectionTitle");
		OnPropertyChanged("IsGeneralSection");
		OnPropertyChanged("IsDownloadSection");
		OnPropertyChanged("IsLanguageSection");
		OnPropertyChanged("IsLaunchMemorySection");
		OnPropertyChanged("IsJavaSection");
		OnPropertyChanged("IsThemeSection");
		OnPropertyChanged("IsInfoSection");
		CurrentSectionViewModel = value?.Section switch
		{
			SettingsPageSection.General => General, 
			SettingsPageSection.Download => Download, 
			SettingsPageSection.Language => Language, 
			SettingsPageSection.LaunchMemory => LaunchMemory, 
			SettingsPageSection.Java => Java, 
			SettingsPageSection.Theme => Theme, 
			SettingsPageSection.Info => Info, 
			SettingsPageSection.ControlList => ControlList, 
			_ => General, 
		};
		ReloadCrossPageSections();
	}

	/// <summary>
	/// 「通用」与「内存与启动」两页共享同一批 AppSettings（检查文件 / 最小化 / 全屏 /
	/// 启动前命令 / 退出后命令 / 游戏参数 / 自动修复）。各页 VM 只在启动时 Load 过一次，
	/// 所以在 A 页改完切到 B 页会看到旧值 —— 这里在切页时重新读一遍源。
	/// Load 内部用 LoadState 包着，不会回写触发持久化。
	/// </summary>
	private void ReloadCrossPageSections()
	{
		if (!persistence.IsPrimed)
		{
			return;
		}
		LauncherSettings settings = persistence.Settings;
		if ((object)General != null)
		{
			General.Load(settings);
		}
		if ((object)LaunchMemory != null)
		{
			LaunchMemory.Load(settings);
		}
	}
}
