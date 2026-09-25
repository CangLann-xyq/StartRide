using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using StartRide.App.Models;
using StartRide.App.Resources;
using StartRide.App.ViewModels.Download;
using StartRide.App.ViewModels.GameSettings;
using StartRide.App.ViewModels.Home;
using StartRide.App.ViewModels.Resources;
using StartRide.App.ViewModels.Settings;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.Services;

public sealed class LauncherSessionCoordinator : IDisposable
{
	private readonly IDownloadSpeedLimitState downloadSpeedLimitState;

	private readonly IDownloadConcurrencyLimitState downloadConcurrencyLimitState;

	private readonly ISettingsService settingsService;

	private readonly IStatusService statusService;

	private readonly DownloadPageViewModel downloadPage;

	private readonly GameSettingsPageViewModel gameSettingsPage;

	private readonly ResourcesPageViewModel resourcesPage;

	private readonly SettingsPageViewModel settingsPage;

	private readonly GameManagementViewModel gameManagement;

	private readonly LauncherStateSyncService stateSyncService;

	private readonly ILogger<LauncherSessionCoordinator> logger;

	private readonly SemaphoreSlim instanceSynchronizationLock = new SemaphoreSlim(1, 1);

	private readonly SettingsPersistenceCoordinator settingsPersistence;

	private HomePageViewModel? homePage;

	private LauncherSettings? settings;

	private string currentPage = "Home";

	private bool isAttached;

	private bool isInitialized;

	public event Action<string>? NavigationRequested;

	public event Action<double>? ProgressChanged;

	public LauncherSessionCoordinator(IDownloadSpeedLimitState downloadSpeedLimitState, IDownloadConcurrencyLimitState downloadConcurrencyLimitState, ISettingsService settingsService, IStatusService statusService, DownloadPageViewModel downloadPage, GameSettingsPageViewModel gameSettingsPage, ResourcesPageViewModel resourcesPage, SettingsPageViewModel settingsPage, GameManagementViewModel gameManagement, LauncherStateSyncService stateSyncService, ILogger<LauncherSessionCoordinator>? logger = null, SettingsPersistenceCoordinator? settingsPersistence = null)
	{
		this.downloadSpeedLimitState = downloadSpeedLimitState;
		this.downloadConcurrencyLimitState = downloadConcurrencyLimitState;
		this.settingsService = settingsService;
		this.statusService = statusService;
		this.downloadPage = downloadPage;
		this.gameSettingsPage = gameSettingsPage;
		this.resourcesPage = resourcesPage;
		this.settingsPage = settingsPage;
		this.gameManagement = gameManagement;
		this.stateSyncService = stateSyncService;
		this.logger = logger ?? NullLogger<LauncherSessionCoordinator>.Instance;
		this.settingsPersistence = settingsPersistence ?? settingsPage.Persistence;
	}

	public void Attach(HomePageViewModel homePage)
	{
		ArgumentNullException.ThrowIfNull(homePage, "homePage");
		if (isAttached)
		{
			throw new InvalidOperationException("The launcher session coordinator is already attached.");
		}
		this.homePage = homePage;
		downloadPage.InstanceInstalled += DownloadPage_InstanceInstalled;
		resourcesPage.ModpackImported += DownloadPage_InstanceInstalled;
		resourcesPage.ModpackManualDownloadsRequested += ResourcesPage_ModpackManualDownloadsRequested;
		gameManagement.PropertyChanged += GameManagement_PropertyChanged;
		gameSettingsPage.LaunchInstanceRequested += GameSettingsPage_LaunchInstanceRequested;
		gameSettingsPage.OnlineModInstallRequested += GameSettingsPage_OnlineModInstallRequested;
		gameSettingsPage.InstancesChanged += GameSettingsPage_InstancesChanged;
		gameSettingsPage.InstanceListActivated += GameSettingsPage_InstanceListActivated;
		settingsPage.LaunchDefaultsChanged += SettingsPage_LaunchDefaultsChanged;
		settingsPage.DownloadSourceChanged += SettingsPage_DownloadSourceChanged;
		settingsPage.MaximumDownloadConcurrencyChanged += SettingsPage_MaximumDownloadConcurrencyChanged;
		settingsPage.DownloadSpeedLimitChanged += SettingsPage_DownloadSpeedLimitChanged;
		settingsPage.MinecraftDirectoryChanged += SettingsPage_MinecraftDirectoryChanged;
		isAttached = true;
	}

	public async Task PrimeAsync(LauncherSettings settings)
	{
		this.settings = settings;
		downloadSpeedLimitState.SetDownloadSpeedLimitMbPerSecond(settings.DownloadSpeedLimitMbPerSecond);
		downloadConcurrencyLimitState.SetMaximumDownloadConcurrency(settings.MaximumDownloadConcurrency);
		await gameManagement.PrimeInstancesAsync(settings);
		homePage?.SetSettings(settings);
		SynchronizeHomeInstances();
		homePage?.Initialize(settings, gameManagement.SelectedInstance);
		downloadPage.PrimeFromSettings(settings);
		gameSettingsPage.PrimeFromSettings(settings);
		settingsPage.PrimeFromSettings(settings);
	}

	public async Task InitializeAsync()
	{
		if (!isInitialized && settings != null)
		{
			homePage?.SetSettings(settings);
			await gameManagement.InitializeAsync(settings);
			if (homePage != null)
			{
				SynchronizeHomeInstances();
				homePage.Initialize(settings, gameManagement.SelectedInstance);
			}
			isInitialized = true;
			if (NavigationCatalog.IsPage(currentPage, "GameSettings"))
			{
				SynchronizeGameSettingsInstances();
			}
		}
	}

	public async Task ActivatePageAsync(string page)
	{
		currentPage = page;
		bool isGameSettingsPage = NavigationCatalog.IsPage(page, "GameSettings");
		bool num = NavigationCatalog.IsPage(page, "Download");
		gameSettingsPage.SetShellPageActive(isGameSettingsPage);
		if (num && settings != null)
		{
			await downloadPage.EnsureVersionsLoadedAsync();
		}
		if (!isInitialized)
		{
			if (isGameSettingsPage && settings != null)
			{
				SynchronizeGameSettingsInstances();
			}
		}
		else
		{
			SynchronizeVisibleInstanceProjection();
		}
	}

	public async Task RefreshExternalInstanceCatalogAsync()
	{
		if (!isInitialized)
		{
			return;
		}
		await instanceSynchronizationLock.WaitAsync();
		try
		{
			await gameManagement.RefreshInstancesAsync();
			SynchronizeVisibleInstanceProjection();
		}
		finally
		{
			instanceSynchronizationLock.Release();
		}
	}

	public Task<bool> SelectLaunchInstanceAsync(GameInstance instance)
	{
		return gameManagement.SelectLaunchInstanceAsync(instance);
	}

	public async Task<bool> SetHomeLaunchMenuPinnedAsync(bool isPinned)
	{
		if (settings == null)
		{
			return false;
		}
		bool previousValue = settings.IsHomeLaunchMenuPinned;
		if (previousValue == isPinned)
		{
			return true;
		}
		settings.IsHomeLaunchMenuPinned = isPinned;
		try
		{
			await settingsService.UpdateAsync(delegate(LauncherSettings latest)
			{
				latest.IsHomeLaunchMenuPinned = isPinned;
			});
			logger.LogInformation("Home launch menu pin preference saved. IsPinned={IsPinned}", isPinned);
			return true;
		}
		catch (Exception exception)
		{
			settings.IsHomeLaunchMenuPinned = previousValue;
			logger.LogWarning(exception, "Failed to save home launch menu pin preference. IsPinned={IsPinned}", isPinned);
			return false;
		}
	}

	public void SetMenuExpanded(bool isExpanded)
	{
		settingsPersistence.Update(delegate(LauncherSettings latest)
		{
			latest.IsMenuExpanded = isExpanded;
		});
	}

	public void Dispose()
	{
		if (isAttached)
		{
			downloadPage.InstanceInstalled -= DownloadPage_InstanceInstalled;
			resourcesPage.ModpackImported -= DownloadPage_InstanceInstalled;
			resourcesPage.ModpackManualDownloadsRequested -= ResourcesPage_ModpackManualDownloadsRequested;
			gameManagement.PropertyChanged -= GameManagement_PropertyChanged;
			gameSettingsPage.LaunchInstanceRequested -= GameSettingsPage_LaunchInstanceRequested;
			gameSettingsPage.OnlineModInstallRequested -= GameSettingsPage_OnlineModInstallRequested;
			gameSettingsPage.InstancesChanged -= GameSettingsPage_InstancesChanged;
			gameSettingsPage.InstanceListActivated -= GameSettingsPage_InstanceListActivated;
			settingsPage.LaunchDefaultsChanged -= SettingsPage_LaunchDefaultsChanged;
			settingsPage.DownloadSourceChanged -= SettingsPage_DownloadSourceChanged;
			settingsPage.MaximumDownloadConcurrencyChanged -= SettingsPage_MaximumDownloadConcurrencyChanged;
			settingsPage.DownloadSpeedLimitChanged -= SettingsPage_DownloadSpeedLimitChanged;
			settingsPage.MinecraftDirectoryChanged -= SettingsPage_MinecraftDirectoryChanged;
			instanceSynchronizationLock.Dispose();
			isAttached = false;
		}
	}

	private void SynchronizeHomeInstances()
	{
		homePage?.ApplyInstanceCatalog(gameManagement.Instances, gameManagement.InstanceCatalogRevision);
		homePage?.SetSelectedInstance(gameManagement.SelectedInstance);
	}

	private void SynchronizeGameSettingsInstances()
	{
		if (!gameSettingsPage.IsDetailsStep)
		{
			gameSettingsPage.ApplyInstanceCatalog(gameManagement.Instances, gameManagement.InstanceCatalogRevision);
		}
	}

	private void SynchronizeVisibleInstanceProjection()
	{
		if (NavigationCatalog.IsPage(currentPage, "Home"))
		{
			SynchronizeHomeInstances();
		}
		else if (NavigationCatalog.IsPage(currentPage, "GameSettings"))
		{
			SynchronizeGameSettingsInstances();
		}
	}

	private void GameManagement_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "SelectedInstance")
		{
			homePage?.SetSelectedInstance(gameManagement.SelectedInstance);
		}
		if (e.PropertyName == "ProgressPercent")
		{
			ProgressChanged?.Invoke(gameManagement.ProgressPercent);
		}
	}

	private void DownloadPage_InstanceInstalled(object? sender, GameInstance instance)
	{
		Observe(HandleInstalledInstanceAsync(instance), "synchronize an installed instance");
	}

	private async Task HandleInstalledInstanceAsync(GameInstance instance)
	{
		stateSyncService.AcknowledgeLocalStateChange();
		await gameManagement.ApplyUpdatedInstanceAsync(instance);
		SynchronizeVisibleInstanceProjection();
		try
		{
			if (!(await gameManagement.SelectLaunchInstanceAsync(instance)))
			{
				gameManagement.SelectedInstance = instance;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to persist installed instance selection. InstanceId={InstanceId}", instance.Id);
			gameManagement.SelectedInstance = instance;
		}
		stateSyncService.AcknowledgeLocalStateChange();
		SynchronizeVisibleInstanceProjection();
	}

	private void ResourcesPage_ModpackManualDownloadsRequested(object? sender, ResourcesModpackManualDownloadsRequestedEventArgs args)
	{
		downloadPage.ModpackManualDownloadsDialog.Show(args.Instance, args.ManualDownloads);
	}

	private void GameSettingsPage_LaunchInstanceRequested(GameInstance instance)
	{
		Observe(HandleGameSettingsLaunchRequestAsync(instance), "select a launch instance from game settings");
	}

	private async Task HandleGameSettingsLaunchRequestAsync(GameInstance instance)
	{
		try
		{
			if (!(await gameManagement.SelectLaunchInstanceAsync(instance)))
			{
				statusService.Report(Strings.Status_LaunchInstanceSelectionFailed);
				return;
			}
			NavigationRequested?.Invoke("Home");
			statusService.Report(string.Format(Strings.Status_LaunchInstanceSelectedFormat, instance.Name));
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to select launch instance from game settings. InstanceId={InstanceId}", instance.Id);
			statusService.Report(Strings.Status_LaunchInstanceSelectionFailed);
		}
	}

	private void GameSettingsPage_OnlineModInstallRequested(GameInstance instance)
	{
		Observe(OpenResourcesModsForInstanceAsync(instance), "open online resources for an instance");
	}

	private void GameSettingsPage_InstanceListActivated()
	{
		if (NavigationCatalog.IsPage(currentPage, "GameSettings"))
		{
			SynchronizeGameSettingsInstances();
		}
	}

	private async Task OpenResourcesModsForInstanceAsync(GameInstance instance)
	{
		NavigationRequested?.Invoke("Resources");
		await resourcesPage.OpenModsForInstanceAsync(instance);
	}

	private void GameSettingsPage_InstancesChanged(GameSettingsInstancesChangedEventArgs args)
	{
		Observe(HandleGameSettingsInstancesChangedAsync(args), "apply a game settings instance change");
	}

	private async Task HandleGameSettingsInstancesChangedAsync(GameSettingsInstancesChangedEventArgs args)
	{
		stateSyncService.AcknowledgeLocalStateChange();
		if (args.Kind == GameSettingsInstancesChangedKind.Updated && args.UpdatedInstance != null)
		{
			await gameManagement.ApplyUpdatedInstanceAsync(args.UpdatedInstance);
			SynchronizeVisibleInstanceProjection();
		}
		else if (args.Kind != GameSettingsInstancesChangedKind.Deleted || string.IsNullOrWhiteSpace(args.DeletedInstanceId))
		{
			await SynchronizeInstancesFromGameSettingsAsync();
		}
		else
		{
			await gameManagement.RemoveInstanceAsync(args.DeletedInstanceId);
			SynchronizeVisibleInstanceProjection();
		}
	}

	private async Task SynchronizeInstancesFromGameSettingsAsync()
	{
		try
		{
			await gameManagement.RefreshInstancesAsync();
			SynchronizeVisibleInstanceProjection();
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to synchronize instances from game settings.");
			statusService.Report(Strings.Status_LoadInstancesFailed);
		}
	}

	private void SettingsPage_LaunchDefaultsChanged(object? sender, EventArgs e)
	{
		if (settings != null)
		{
			homePage?.SetSettings(settings);
			gameSettingsPage.PrimeFromSettings(settings);
		}
	}

	private void SettingsPage_DownloadSourceChanged(object? sender, SettingsDownloadSourceChangedEventArgs e)
	{
		if (settings != null)
		{
			settings.DownloadSourcePreference = e.Preference;
		}
		downloadPage.ApplyDownloadSourcePreference(e.Preference);
		gameManagement.ApplyDownloadSourcePreference(e.Preference);
	}

	private void SettingsPage_MaximumDownloadConcurrencyChanged(object? sender, SettingsMaximumDownloadConcurrencyChangedEventArgs e)
	{
		if (settings != null)
		{
			settings.MaximumDownloadConcurrency = e.MaximumDownloadConcurrency;
		}
		downloadConcurrencyLimitState.SetMaximumDownloadConcurrency(e.MaximumDownloadConcurrency);
	}

	private void SettingsPage_DownloadSpeedLimitChanged(object? sender, SettingsDownloadSpeedLimitChangedEventArgs e)
	{
		if (settings != null)
		{
			settings.DownloadSpeedLimitMbPerSecond = e.DownloadSpeedLimitMbPerSecond;
		}
		downloadSpeedLimitState.SetDownloadSpeedLimitMbPerSecond(e.DownloadSpeedLimitMbPerSecond);
		downloadPage.ApplyDownloadSpeedLimit(e.DownloadSpeedLimitMbPerSecond);
		gameManagement.ApplyDownloadSpeedLimit(e.DownloadSpeedLimitMbPerSecond);
	}

	private void SettingsPage_MinecraftDirectoryChanged(object? sender, SettingsGameDirectoryChangedEventArgs e)
	{
		if (settings != null)
		{
			settings.MinecraftDirectory = e.MinecraftDirectory;
			homePage?.SetSettings(settings);
			downloadPage.ApplyMinecraftDirectory(e.MinecraftDirectory);
			gameSettingsPage.PrimeFromSettings(settings);
			stateSyncService.AcknowledgeLocalStateChange();
			Observe(RefreshMinecraftDirectoryInstancesAsync(), "refresh instances after changing the Minecraft directory");
		}
	}

	private async Task RefreshMinecraftDirectoryInstancesAsync()
	{
		_ = 2;
		try
		{
			await gameManagement.RefreshInstancesAsync();
			await downloadPage.InstanceOptions.RefreshNameAvailabilityAsync();
			LauncherSettings launcherSettings = await settingsService.LoadAsync();
			if (settings != null && MinecraftDirectoryPath.Equals(settings.MinecraftDirectory, launcherSettings.MinecraftDirectory))
			{
				settings.DefaultInstanceId = launcherSettings.DefaultInstanceId;
			}
			SynchronizeVisibleInstanceProjection();
			stateSyncService.AcknowledgeLocalStateChange();
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to refresh instances after changing the Minecraft directory.");
			statusService.Report(Strings.Status_LoadInstancesFailed);
		}
	}

	private void Observe(Task task, string operation)
	{
		ObserveAsync(task, operation);
	}

	private async Task ObserveAsync(Task task, string operation)
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
}
