using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Models;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.ViewModels.Account;
using Launcher.App.ViewModels.Download;
using Launcher.App.ViewModels.GameSettings;
using Launcher.App.ViewModels.Home;
using Launcher.App.ViewModels.Multiplayer;
using Launcher.App.ViewModels.Resources;
using Launcher.App.ViewModels.Settings;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Shell;

public sealed class MainViewModel : ObservableObject
{
	private static readonly TimeSpan FloatingMessageDuration = TimeSpan.FromSeconds(2.2);

	private readonly ISettingsService settingsService;

	private readonly LauncherSessionCoordinator sessionCoordinator;

	private readonly IWindowService windowService;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger<MainViewModel> logger;

	private bool hasPrimedSettings;

	private bool hasInitialized;

	private bool isCloseConfirmed;

	private CancellationTokenSource? floatingMessageHideCancellation;

	private GameInstance? pendingJavaRequirementInstance;

	[ObservableProperty]
	private LauncherSettings settings = new LauncherSettings();

	[ObservableProperty]
	private string currentPage = "Home";

	[ObservableProperty]
	private bool isMenuExpanded;

	[ObservableProperty]
	private string statusMessage = Strings.Status_Ready;

	[ObservableProperty]
	private double progressPercent;

	[ObservableProperty]
	private string floatingMessage = string.Empty;

	[ObservableProperty]
	private bool isFloatingMessageOpen;

	[ObservableProperty]
	private bool isJavaRequirementDialogOpen;

	[ObservableProperty]
	private string javaRequirementDialogTitle = Strings.Dialog_JavaRequirementNotMetTitle;

	[ObservableProperty]
	private string javaRequirementDialogMessage = string.Empty;

	[ObservableProperty]
	private bool isJavaRequirementForceLaunchAvailable;

	[ObservableProperty]
	private bool isDownloadCloseConfirmationDialogOpen;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? initializeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelDownloadCloseConfirmationCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? confirmDownloadCloseCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? closeJavaRequirementDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? openJavaSettingsFromRequirementDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? forceLaunchFromJavaRequirementDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<NavigationItem>? navigateCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? toggleMenuCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? minimizeWindowCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? closeWindowCommand;

	public AccountPageViewModel AccountPage { get; }

	public HomePageViewModel HomePage { get; }

	public DownloadPageViewModel DownloadPage { get; }

	public DownloadTasksPageViewModel DownloadTasksPage { get; }

	public GameSettingsPageViewModel GameSettingsPage { get; }

	public MultiplayerPageViewModel MultiplayerPage { get; }

	public ResourcesPageViewModel ResourcesPage { get; }

	public SettingsPageViewModel SettingsPage { get; }

	public GameManagementViewModel GameManagement { get; }

	public LaunchStatusDialogViewModel LaunchStatusDialog { get; }

	public UserAgreementDialogViewModel UserAgreementDialog { get; }

	public MinecraftDirectoryStartupRecoveryDialogViewModel MinecraftDirectoryStartupRecoveryDialog { get; }

	public TerracottaAgreementDialogViewModel TerracottaAgreementDialog { get; }

	public LauncherBackgroundViewModel LauncherBackground { get; }

	public NavigationItem DownloadTasksNavigationItem { get; } = NavigationCatalog.CreateDownloadTasksItem();

	public ObservableCollection<NavigationItem> NavigationItems { get; } = new ObservableCollection<NavigationItem>(NavigationCatalog.CreatePrimaryItems());

	public ObservableCollection<NavigationItem> SecondaryItems { get; } = new ObservableCollection<NavigationItem>();

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public LauncherSettings Settings
	{
		get
		{
			return settings;
		}
		[MemberNotNull("settings")]
		set
		{
			if (!EqualityComparer<LauncherSettings>.Default.Equals(settings, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Settings);
				settings = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Settings);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string CurrentPage
	{
		get
		{
			return currentPage;
		}
		[MemberNotNull("currentPage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(currentPage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentPage);
				currentPage = value;
				OnCurrentPageChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentPage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsMenuExpanded
	{
		get
		{
			return isMenuExpanded;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isMenuExpanded, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsMenuExpanded);
				isMenuExpanded = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsMenuExpanded);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string StatusMessage
	{
		get
		{
			return statusMessage;
		}
		[MemberNotNull("statusMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(statusMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.StatusMessage);
				statusMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.StatusMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double ProgressPercent
	{
		get
		{
			return progressPercent;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(progressPercent, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ProgressPercent);
				progressPercent = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ProgressPercent);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string FloatingMessage
	{
		get
		{
			return floatingMessage;
		}
		[MemberNotNull("floatingMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(floatingMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FloatingMessage);
				floatingMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FloatingMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsFloatingMessageOpen
	{
		get
		{
			return isFloatingMessageOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isFloatingMessageOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsFloatingMessageOpen);
				isFloatingMessageOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsFloatingMessageOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsJavaRequirementDialogOpen
	{
		get
		{
			return isJavaRequirementDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isJavaRequirementDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsJavaRequirementDialogOpen);
				isJavaRequirementDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsJavaRequirementDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string JavaRequirementDialogTitle
	{
		get
		{
			return javaRequirementDialogTitle;
		}
		[MemberNotNull("javaRequirementDialogTitle")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(javaRequirementDialogTitle, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.JavaRequirementDialogTitle);
				javaRequirementDialogTitle = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.JavaRequirementDialogTitle);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string JavaRequirementDialogMessage
	{
		get
		{
			return javaRequirementDialogMessage;
		}
		[MemberNotNull("javaRequirementDialogMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(javaRequirementDialogMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.JavaRequirementDialogMessage);
				javaRequirementDialogMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.JavaRequirementDialogMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsJavaRequirementForceLaunchAvailable
	{
		get
		{
			return isJavaRequirementForceLaunchAvailable;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isJavaRequirementForceLaunchAvailable, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsJavaRequirementForceLaunchAvailable);
				isJavaRequirementForceLaunchAvailable = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsJavaRequirementForceLaunchAvailable);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsDownloadCloseConfirmationDialogOpen
	{
		get
		{
			return isDownloadCloseConfirmationDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isDownloadCloseConfirmationDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDownloadCloseConfirmationDialogOpen);
				isDownloadCloseConfirmationDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDownloadCloseConfirmationDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand InitializeCommand => initializeCommand ?? (initializeCommand = new AsyncRelayCommand(InitializeAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelDownloadCloseConfirmationCommand => cancelDownloadCloseConfirmationCommand ?? (cancelDownloadCloseConfirmationCommand = new RelayCommand(CancelDownloadCloseConfirmation));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ConfirmDownloadCloseCommand => confirmDownloadCloseCommand ?? (confirmDownloadCloseCommand = new RelayCommand(ConfirmDownloadClose));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CloseJavaRequirementDialogCommand => closeJavaRequirementDialogCommand ?? (closeJavaRequirementDialogCommand = new RelayCommand(CloseJavaRequirementDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand OpenJavaSettingsFromRequirementDialogCommand => openJavaSettingsFromRequirementDialogCommand ?? (openJavaSettingsFromRequirementDialogCommand = new AsyncRelayCommand(OpenJavaSettingsFromRequirementDialogAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ForceLaunchFromJavaRequirementDialogCommand => forceLaunchFromJavaRequirementDialogCommand ?? (forceLaunchFromJavaRequirementDialogCommand = new AsyncRelayCommand(ForceLaunchFromJavaRequirementDialogAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<NavigationItem> NavigateCommand => navigateCommand ?? (navigateCommand = new AsyncRelayCommand<NavigationItem>(NavigateAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ToggleMenuCommand => toggleMenuCommand ?? (toggleMenuCommand = new RelayCommand(ToggleMenu));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand MinimizeWindowCommand => minimizeWindowCommand ?? (minimizeWindowCommand = new RelayCommand(MinimizeWindow));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CloseWindowCommand => closeWindowCommand ?? (closeWindowCommand = new RelayCommand(CloseWindow));

	public MainViewModel(ISettingsService settingsService, LauncherSessionCoordinator sessionCoordinator, AccountPageViewModel accountPage, DownloadPageViewModel downloadPage, DownloadTasksPageViewModel downloadTasksPage, GameSettingsPageViewModel gameSettingsPage, MultiplayerPageViewModel multiplayerPage, ResourcesPageViewModel resourcesPage, SettingsPageViewModel settingsPage, GameManagementViewModel gameManagement, IWindowService windowService, IStatusService statusService, IFloatingMessageService floatingMessageService, IUiDispatcher uiDispatcher, IHomePageViewModelFactory homePageFactory, LaunchStatusDialogViewModel launchStatusDialog, UserAgreementDialogViewModel userAgreementDialog, MinecraftDirectoryStartupRecoveryDialogViewModel minecraftDirectoryStartupRecoveryDialog, TerracottaAgreementDialogViewModel terracottaAgreementDialog, LauncherBackgroundViewModel launcherBackground, ILogger<MainViewModel>? logger = null)
	{
		this.settingsService = settingsService;
		this.sessionCoordinator = sessionCoordinator;
		this.windowService = windowService;
		this.uiDispatcher = uiDispatcher;
		this.logger = logger ?? NullLogger<MainViewModel>.Instance;
		AccountPage = accountPage;
		DownloadPage = downloadPage;
		DownloadTasksPage = downloadTasksPage;
		GameSettingsPage = gameSettingsPage;
		MultiplayerPage = multiplayerPage;
		ResourcesPage = resourcesPage;
		SettingsPage = settingsPage;
		GameManagement = gameManagement;
		LaunchStatusDialog = launchStatusDialog;
		UserAgreementDialog = userAgreementDialog;
		MinecraftDirectoryStartupRecoveryDialog = minecraftDirectoryStartupRecoveryDialog;
		TerracottaAgreementDialog = terracottaAgreementDialog;
		LauncherBackground = launcherBackground;
		HomePage = homePageFactory.Create(AccountPage, delegate(double percent)
		{
			ProgressPercent = percent;
		}, sessionCoordinator.SelectLaunchInstanceAsync, sessionCoordinator.SetHomeLaunchMenuPinnedAsync, OpenGameSettingsForInstanceAsync);
		sessionCoordinator.Attach(HomePage);
		sessionCoordinator.NavigationRequested += SessionCoordinator_NavigationRequested;
		sessionCoordinator.ProgressChanged += delegate(double progress)
		{
			ProgressPercent = progress;
		};
		HomePage.JavaRequirementNotMet += HomePage_JavaRequirementNotMet;
		HomePage.LaunchFailureReported += HomePage_LaunchFailureReported;
		HomePage.LaunchActivityChanged += HomePage_LaunchActivityChanged;
		GameSettingsPage.LocalImportRequested += GameSettingsPage_LocalImportRequested;
		GameSettingsPage.ResourceProjectDetailsRequested += GameSettingsPage_ResourceProjectDetailsRequested;
		GameSettingsPage.MinecraftDirectorySwitchRequested += GameSettingsPage_MinecraftDirectorySwitchRequested;
		statusService.MessageReported += delegate(string message)
		{
			StatusMessage = message;
		};
		floatingMessageService.MessageRequested += ShowFloatingMessage;
		AccountPage.PropertyChanged += AccountPage_PropertyChanged;
		AccountPage.Dialog.DroppedThirdPartyAccountAdditionCompleted += AccountDialog_DroppedThirdPartyAccountAdditionCompleted;
		UpdateNavigationSelection();
	}

	public async Task PrimeAsync(LauncherSettings? initialSettings = null, MinecraftDirectoryStartupRecoveryResult? minecraftDirectoryStartupRecovery = null)
	{
		if (!hasPrimedSettings)
		{
			LauncherSettings launcherSettings = initialSettings;
			if (launcherSettings == null)
			{
				launcherSettings = await settingsService.LoadAsync();
			}
			Settings = launcherSettings;
			MinecraftDirectoryStartupRecoveryDialog.Prime(minecraftDirectoryStartupRecovery);
			LauncherBackground.ApplyEffect(Settings.LauncherBackgroundEffect, reportFailure: false);
			UserAgreementDialog.Prime(Settings);
			IsMenuExpanded = Settings.IsMenuExpanded;
			await AccountPage.PrimeAsync();
			await sessionCoordinator.PrimeAsync(Settings);
			UpdateNavigationSelection();
			UpdateAccountNavigationAvatar();
			hasPrimedSettings = true;
		}
	}

	public Task<bool> WaitForUserAgreementDecisionAsync()
	{
		return UserAgreementDialog.WaitForDecisionAsync();
	}

	[RelayCommand]
	public async Task InitializeAsync()
	{
		await PrimeAsync();
		MinecraftDirectoryStartupRecoveryDialog.ShowPending();
		await AccountPage.InitializeAsync();
		await sessionCoordinator.InitializeAsync();
		UpdateSecondaryItems();
		UpdateNavigationSelection();
		UpdateAccountNavigationAvatar();
		hasInitialized = true;
	}

	[RelayCommand]
	private void CancelDownloadCloseConfirmation()
	{
		logger.LogInformation("Launcher close canceled because downloads are running.");
		IsDownloadCloseConfirmationDialogOpen = false;
	}

	[RelayCommand]
	private void ConfirmDownloadClose()
	{
		logger.LogInformation("Launcher close confirmed while downloads are running. RunningTaskCount={RunningTaskCount}", DownloadTasksPage.RunningTaskCount);
		isCloseConfirmed = true;
		IsDownloadCloseConfirmationDialogOpen = false;
		DownloadTasksPage.CancelAllRunningTasks();
		windowService.Close();
	}

	[RelayCommand]
	private void CloseJavaRequirementDialog()
	{
		IsJavaRequirementDialogOpen = false;
		IsJavaRequirementForceLaunchAvailable = false;
		pendingJavaRequirementInstance = null;
	}

	[RelayCommand]
	private Task OpenJavaSettingsFromRequirementDialogAsync()
	{
		GameInstance gameInstance = pendingJavaRequirementInstance;
		IsJavaRequirementDialogOpen = false;
		IsJavaRequirementForceLaunchAvailable = false;
		pendingJavaRequirementInstance = null;
		LaunchSettingsMode? launchSettingsMode = gameInstance?.JavaSettingsMode;
		if (launchSettingsMode.HasValue && launchSettingsMode == LaunchSettingsMode.PerInstance)
		{
			GameSettingsPage.ShowInstanceDetails(gameInstance, "java");
			CurrentPage = "GameSettings";
		}
		else
		{
			SettingsPage.ShowJavaSection();
			CurrentPage = "Settings";
		}
		UpdateSecondaryItems();
		UpdateNavigationSelection();
		return Task.CompletedTask;
	}

	[RelayCommand]
	private async Task ForceLaunchFromJavaRequirementDialogAsync()
	{
		GameInstance gameInstance = pendingJavaRequirementInstance;
		IsJavaRequirementDialogOpen = false;
		IsJavaRequirementForceLaunchAvailable = false;
		pendingJavaRequirementInstance = null;
		if (gameInstance != null)
		{
			await HomePage.ForceLaunchIgnoringJavaRequirementAsync(gameInstance);
		}
	}

	public Task ActivateCurrentPageAsync()
	{
		return sessionCoordinator.ActivatePageAsync(CurrentPage);
	}

	public Task SyncExternalInstanceCatalogAsync()
	{
		if (!hasInitialized)
		{
			return Task.CompletedTask;
		}
		return sessionCoordinator.RefreshExternalInstanceCatalogAsync();
	}

	private void AccountPage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "SelectedAccount")
		{
			UpdateAccountNavigationAvatar();
		}
	}

	private void AccountDialog_DroppedThirdPartyAccountAdditionCompleted()
	{
		CurrentPage = "Account";
		UpdateSecondaryItems();
		UpdateNavigationSelection();
	}

	private void GameSettingsPage_LocalImportRequested()
	{
		DownloadPage.LocalImportDialog.Open();
	}

	private void GameSettingsPage_MinecraftDirectorySwitchRequested()
	{
		SettingsPage.General.OpenMinecraftDirectorySwitchDialog();
	}

	private void GameSettingsPage_ResourceProjectDetailsRequested(ResourceProjectReference reference)
	{
		ObserveShellTask(OpenResourceProjectDetailsAsync(reference), "open recognized resource project details");
	}

	private async Task OpenResourceProjectDetailsAsync(ResourceProjectReference reference)
	{
		ResourceProject project = await ResourcesPage.LoadProjectDetailsAsync(reference);
		if (project == null)
		{
			return;
		}
		CurrentPage = "Resources";
		UpdateSecondaryItems();
		UpdateNavigationSelection();
		TaskCompletionSource completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		uiDispatcher.Post(delegate
		{
			try
			{
				ResourcesPage.ShowProjectDetails(reference, project);
				completion.TrySetResult();
			}
			catch (Exception exception)
			{
				completion.TrySetException(exception);
			}
		});
		await completion.Task;
	}

	private void UpdateSecondaryItems()
	{
		SecondaryItems.Clear();
		foreach (NavigationItem item in NavigationCatalog.CreateSecondaryItems(CurrentPage))
		{
			SecondaryItems.Add(item);
		}
	}

	private void UpdateNavigationSelection()
	{
		foreach (NavigationItem navigationItem in NavigationItems)
		{
			navigationItem.IsSelected = NavigationCatalog.IsPage(navigationItem.Page, CurrentPage);
		}
		DownloadTasksNavigationItem.IsSelected = NavigationCatalog.IsPage(DownloadTasksNavigationItem.Page, CurrentPage);
	}

	private void SessionCoordinator_NavigationRequested(string page)
	{
		CurrentPage = page;
		UpdateSecondaryItems();
		UpdateNavigationSelection();
	}

	private void HomePage_JavaRequirementNotMet(object? sender, JavaRequirementNotMetEventArgs e)
	{
		pendingJavaRequirementInstance = e.Instance;
		JavaRuntimeSelectionFailureReason reason = e.Reason;
		bool flag = (uint)(reason - 5) <= 1u;
		IsJavaRequirementForceLaunchAvailable = flag;
		if (e.Reason == JavaRuntimeSelectionFailureReason.ManualRuntimeVersionTooLow)
		{
			JavaRequirementDialogTitle = Strings.Dialog_JavaManualVersionTooLowTitle;
			JavaRequirementDialogMessage = string.Format(Strings.Dialog_JavaManualVersionTooLowMessageFormat, string.IsNullOrWhiteSpace(e.Instance.Name) ? e.Instance.VersionName : e.Instance.Name, e.RequiredMajorVersion?.ToString() ?? Strings.Dialog_LaunchStatusUnknownExitCode, e.CurrentMajorVersion?.ToString() ?? Strings.Dialog_LaunchStatusUnknownExitCode);
		}
		else if (e.Reason == JavaRuntimeSelectionFailureReason.ManualRuntimeIncompatible)
		{
			JavaRequirementDialogTitle = Strings.Dialog_JavaManualVersionIncompatibleTitle;
			JavaRequirementDialogMessage = string.Format(Strings.Dialog_JavaManualVersionIncompatibleMessageFormat, string.IsNullOrWhiteSpace(e.Instance.Name) ? e.Instance.VersionName : e.Instance.Name, e.RecommendedMajorVersion?.ToString() ?? Strings.Dialog_LaunchStatusUnknownExitCode, e.CurrentVersion ?? e.CurrentMajorVersion?.ToString() ?? Strings.Dialog_LaunchStatusUnknownExitCode);
		}
		else if (e.Reason == JavaRuntimeSelectionFailureReason.AutomaticRuntimeMissing)
		{
			JavaRequirementDialogTitle = Strings.Dialog_JavaRuntimeMissingTitle;
			int? requiredMajorVersion = e.RequiredMajorVersion;
			JavaRequirementDialogMessage = ((!requiredMajorVersion.HasValue) ? Strings.Dialog_JavaRuntimeMissingMessage : string.Format(arg0: requiredMajorVersion.GetValueOrDefault(), format: Strings.Dialog_JavaCompatibilityNotMetMessageFormat));
		}
		else
		{
			JavaRequirementDialogTitle = Strings.Dialog_JavaRequirementNotMetTitle;
			int? requiredMajorVersion = e.RequiredMajorVersion;
			JavaRequirementDialogMessage = ((!requiredMajorVersion.HasValue) ? Strings.Dialog_JavaRequirementNotMetMessage : string.Format(arg0: requiredMajorVersion.GetValueOrDefault(), format: Strings.Dialog_JavaCompatibilityNotMetMessageFormat));
		}
		IsJavaRequirementDialogOpen = true;
	}

	private void HomePage_LaunchActivityChanged(object? sender, EventArgs e)
	{
		SettingsPage.General.SetGameLaunchInProgress(HomePage.IsLaunching);
	}

	private void HomePage_LaunchFailureReported(object? sender, LaunchFailureReport report)
	{
		if (!uiDispatcher.HasAccess)
		{
			uiDispatcher.Post(delegate
			{
				HomePage_LaunchFailureReported(sender, report);
			});
		}
		else
		{
			windowService.RestoreAndActivate();
			LaunchStatusDialog.Show(report);
		}
	}

	private void UpdateAccountNavigationAvatar()
	{
		NavigationItem navigationItem = NavigationItems.FirstOrDefault((NavigationItem item) => item.Page == "Account");
		if (navigationItem != null)
		{
			// StartRide：不再用 MC 皮肤头像（AvatarUrl），置空后导航项回退显示 IconKey="general/person" 人形 SVG。
			navigationItem.AvatarUrl = null;
		}
	}

	private Task OpenGameSettingsForInstanceAsync(GameInstance? instance)
	{
		GameSettingsPage.ShowInstanceDetails(instance);
		CurrentPage = "GameSettings";
		UpdateSecondaryItems();
		UpdateNavigationSelection();
		return Task.CompletedTask;
	}

	private void ShowFloatingMessage(FloatingMessageRequest request)
	{
		if (!uiDispatcher.HasAccess)
		{
			uiDispatcher.Post(delegate
			{
				ShowFloatingMessage(request);
			});
			return;
		}
		floatingMessageHideCancellation?.Cancel();
		floatingMessageHideCancellation?.Dispose();
		floatingMessageHideCancellation = null;
		if (string.IsNullOrWhiteSpace(request.Message))
		{
			IsFloatingMessageOpen = false;
			FloatingMessage = string.Empty;
			return;
		}
		FloatingMessage = request.Message;
		IsFloatingMessageOpen = true;
		if (request.AutoHide)
		{
			floatingMessageHideCancellation = new CancellationTokenSource();
			ObserveShellTask(HideFloatingMessageAfterDelayAsync(floatingMessageHideCancellation.Token), "hide the floating message");
		}
	}

	private async Task HideFloatingMessageAfterDelayAsync(CancellationToken cancellationToken)
	{
		try
		{
			await Task.Delay(FloatingMessageDuration, cancellationToken);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		uiDispatcher.Post(delegate
		{
			if (!cancellationToken.IsCancellationRequested)
			{
				IsFloatingMessageOpen = false;
			}
		});
	}

	private void ObserveShellTask(Task task, string operation)
	{
		ObserveShellTaskAsync(task, operation);
	}

	private async Task ObserveShellTaskAsync(Task task, string operation)
	{
		try
		{
			await task;
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to {Operation}.", operation);
		}
	}

	[RelayCommand]
	private async Task NavigateAsync(NavigationItem item)
	{
		bool flag = NavigationCatalog.IsPage(item.Page, "Multiplayer");
		if (flag)
		{
			flag = !(await TerracottaAgreementDialog.EnsureReadyAsync());
		}
		if (!flag)
		{
			SelectNavigationItem(item);
		}
	}

	public void SelectNavigationItem(NavigationItem item)
	{
		string text = ((item.Loader is LoaderKind) ? "Download" : item.Page);
		LoaderKind? loader = item.Loader;
		if (loader.HasValue)
		{
			LoaderKind valueOrDefault = loader.GetValueOrDefault();
			GameManagement.SelectLoader(valueOrDefault);
			CurrentPage = text;
		}
		else
		{
			CurrentPage = text;
		}
		UpdateSecondaryItems();
		UpdateNavigationSelection();
	}

	[RelayCommand]
	private void ToggleMenu()
	{
		IsMenuExpanded = !IsMenuExpanded;
		Settings.IsMenuExpanded = IsMenuExpanded;
		sessionCoordinator.SetMenuExpanded(IsMenuExpanded);
	}

	[RelayCommand]
	private void MinimizeWindow()
	{
		windowService.Minimize();
	}

	[RelayCommand]
	private void CloseWindow()
	{
		if (CanCloseWindow())
		{
			windowService.Close();
		}
	}

	public bool CanCloseWindow()
	{
		if (isCloseConfirmed || !DownloadTasksPage.HasRunningTasks)
		{
			return true;
		}
		logger.LogInformation("Launcher close requested while downloads are running. RunningTaskCount={RunningTaskCount}", DownloadTasksPage.RunningTaskCount);
		IsDownloadCloseConfirmationDialogOpen = true;
		return false;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnCurrentPageChanged(string value)
	{
		UpdateNavigationSelection();
		ObserveShellTask(ActivateCurrentPageAsync(), "activate current page");
	}
}
