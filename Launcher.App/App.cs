using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Launcher.App.Diagnostics;
using Launcher.App.Logging;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.ViewModels.Account;
using Launcher.App.ViewModels.Download;
using Launcher.App.ViewModels.GameSettings;
using Launcher.App.ViewModels.Home;
using Launcher.App.ViewModels.Multiplayer;
using Launcher.App.ViewModels.Resources;
using Launcher.App.ViewModels.Settings;
using Launcher.App.ViewModels.Shell;
using Launcher.App.Views.Shell;
using Launcher.Application.Accounts;
using Launcher.Application.DependencyInjection;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Launcher.Infrastructure;
using Launcher.Infrastructure.DependencyInjection;
using Launcher.Infrastructure.Persistence;
using Launcher.Infrastructure.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using StartRide.Core;

namespace Launcher.App;

public partial class App : System.Windows.Application
{
	[Serializable]
	[CompilerGenerated]
	private sealed class _003C_003Ec
	{
		public static readonly _003C_003Ec _003C_003E9 = new _003C_003Ec();

		public static Action<ILoggingBuilder> _003C_003E9__6_0;

		public static Func<IServiceProvider, SettingsPersistenceCoordinator> _003C_003E9__6_1;

		public static DispatcherUnhandledExceptionEventHandler _003C_003E9__23_0;

		public static UnhandledExceptionEventHandler _003C_003E9__23_1;

		public static EventHandler<UnobservedTaskExceptionEventArgs> _003C_003E9__23_2;

		internal void _003COnStartup_003Eb__6_0(ILoggingBuilder builder)
		{
			builder.ClearProviders();
			builder.AddSerilog(Log.Logger);
		}

		internal SettingsPersistenceCoordinator _003COnStartup_003Eb__6_1(IServiceProvider serviceProvider)
		{
			return new SettingsPersistenceCoordinator(serviceProvider.GetRequiredService<ISettingsService>(), serviceProvider.GetRequiredService<IStatusService>(), serviceProvider.GetRequiredService<ILogger<SettingsPersistenceCoordinator>>());
		}

		internal void _003CRegisterUnhandledExceptionLogging_003Eb__23_0(object _, DispatcherUnhandledExceptionEventArgs args)
		{
			Log.Error(args.Exception, "Unhandled dispatcher exception.");
		}

		internal void _003CRegisterUnhandledExceptionLogging_003Eb__23_1(object _, UnhandledExceptionEventArgs args)
		{
			if (args.ExceptionObject is Exception exception)
			{
				Log.Fatal(exception, "Unhandled app domain exception. IsTerminating={IsTerminating}", args.IsTerminating);
			}
			else
			{
				Log.Fatal("Unhandled app domain exception object. IsTerminating={IsTerminating}", args.IsTerminating);
			}
		}

		internal void _003CRegisterUnhandledExceptionLogging_003Eb__23_2(object? _, UnobservedTaskExceptionEventArgs args)
		{
			Log.Error(args.Exception, "Unobserved task exception.");
		}
	}

	private readonly LauncherBootstrapPreferences bootstrapPreferences;

	private ServiceProvider? serviceProvider;

	private bool isUpdateApplyMode;

	static App()
	{
		EventManager.RegisterClassHandler(typeof(Control), FrameworkElement.LoadedEvent, new RoutedEventHandler(SuppressControlFocusVisual));
	}

	public App()
	{
		bootstrapPreferences = new LauncherBootstrapPreferences("zh-Hans", EnableDiagnosticLogging: false);
		string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
		if ((object)LauncherUpdateApplyOptions.Parse(args) == null && (object)LauncherUpdateRecoveryOptions.Parse(args) == null)
		{
			bootstrapPreferences = new JsonSettingsService().LoadLauncherBootstrapPreferences();
			ApplyLauncherCulture(bootstrapPreferences.LauncherLanguage);
		}
	}

	private static void SuppressControlFocusVisual(object sender, RoutedEventArgs e)
	{
		if (sender is Control { FocusVisualStyle: not null } control)
		{
			control.FocusVisualStyle = null;
		}
	}

	protected override async void OnStartup(StartupEventArgs e)
	{
		long startupStartedAt = Stopwatch.GetTimestamp();
		LauncherUpdateApplyOptions launcherUpdateApplyOptions = LauncherUpdateApplyOptions.Parse(e.Args);
		if ((object)launcherUpdateApplyOptions != null)
		{
			isUpdateApplyMode = true;
			int exitCode = new LauncherUpdateApplyRunner().Run(launcherUpdateApplyOptions);
			Shutdown(exitCode);
			return;
		}
		LauncherUpdateRecoveryOptions launcherUpdateRecoveryOptions = LauncherUpdateRecoveryOptions.Parse(e.Args);
		if ((object)launcherUpdateRecoveryOptions != null)
		{
			isUpdateApplyMode = true;
			int exitCode2 = new LauncherUpdateApplyRunner().RunRecovery(launcherUpdateRecoveryOptions);
			Shutdown(exitCode2);
			return;
		}
		LauncherLogLevelController logLevelController = new LauncherLogLevelController(bootstrapPreferences.EnableDiagnosticLogging);
		Log.Logger = LauncherLogConfiguration.CreateLogger(logLevelController.LevelSwitch, logLevelController.MicrosoftLevelSwitch);
		RegisterUnhandledExceptionLogging();
		try
		{
			Log.Information("Launcher startup started. ArgumentCount={ArgumentCount}", e.Args.Length);
			if (LauncherUpdateStartupCoordinator.TryStartPendingRecovery(e.Args, Environment.ProcessPath, Environment.ProcessId))
			{
				Log.Information("Pending launcher update recovery process started.");
				Shutdown(0);
				return;
			}
			ServiceCollection services = new ServiceCollection();
			services.AddLogging(delegate(ILoggingBuilder builder)
			{
				builder.ClearProviders();
				builder.AddSerilog(Log.Logger);
			});
			((IServiceCollection)services).AddSingleton((ILauncherLogLevelController)logLevelController);
			services.AddLauncherApplication();
			services.AddLauncherInfrastructure();
			// StartRide：把"检查更新"从 StartRide 的 GitHub 清单换成自有的
			// （windseek.cloud/update → 本仓库 update/ 两个通道）。
			// MS.DI 取后注册者，因此必须排在 AddLauncherInfrastructure 之后才顶得掉原实现。
			services.AddSingleton<ILauncherUpdateService, StartRideLauncherUpdateService>();
			// StartRide：联机页改用自建中继（房间码）实现顶替原 Terracotta。
			// MS.DI 取后注册者，因此这一行必须排在 AddLauncherApplication 之后。
			services.AddSingleton<IMultiplayerLobbyService, StartRideLobbyService>();
			// StartRide：无需下载 Terracotta/EasyTier，直接报告模块就绪，免掉那份第三方协议弹窗。
			services.AddSingleton<ITerracottaProvisioningService, StartRideProvisioningService>();
			// StartRide：用"本机 BeamNG.drive"这一个合成实例顶替 Minecraft 实例扫描，
			// 并把「启动游戏」接到真实拉起 BeamNG.drive.exe。
			services.AddSingleton<IGameInstanceService, StartRideInstanceService>();
			services.AddSingleton<ILaunchService, StartRideLaunchService>();
			services.AddSingleton<IStatusService, StatusService>();
			services.AddSingleton<IFloatingMessageService, FloatingMessageService>();
			services.AddSingleton<IWindowService, WindowService>();
			services.AddSingleton<IClipboardService, ClipboardService>();
			services.AddSingleton<IFilePickerService, FilePickerService>();
			services.AddSingleton<IInstanceFolderService, InstanceFolderService>();
			services.AddSingleton<ILauncherBackgroundImageLoader, LauncherBackgroundImageLoader>();
			services.AddSingleton<IExternalLinkService, ExternalLinkService>();
			services.AddSingleton<IApplicationExitService, ApplicationExitService>();
			services.AddSingleton<IInfoReferenceProjectCatalog, EmbeddedInfoReferenceProjectCatalog>();
			services.AddSingleton<IMicrosoftLoginBrowserPageProvider, MicrosoftLoginBrowserPageProvider>();
			services.AddSingleton<IAccountDialogService, AccountDialogService>();
			services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
			services.AddSingleton((IServiceProvider serviceProvider) => new SettingsPersistenceCoordinator(serviceProvider.GetRequiredService<ISettingsService>(), serviceProvider.GetRequiredService<IStatusService>(), serviceProvider.GetRequiredService<ILogger<SettingsPersistenceCoordinator>>()));
			services.AddSingleton((IServiceProvider _) => new UiThreadStallMonitor(((DispatcherObject)this).Dispatcher));
			services.AddSingleton<IThemeService, ThemeService>();
			services.AddSingleton<IHomePageViewModelFactory, HomePageViewModelFactory>();
			services.AddSingleton<LauncherSessionCoordinator>();
			services.AddSingleton<LauncherStateSyncService>();
			services.AddSingleton<LauncherShutdownService>();
			services.AddSingleton<MainWindowPlacementService>();
			services.AddSingleton<LaunchStatusDialogViewModel>();
			services.AddSingleton<UserAgreementDialogViewModel>();
			services.AddSingleton<MinecraftDirectoryStartupRecoveryDialogViewModel>();
			services.AddSingleton<TerracottaAgreementDialogViewModel>();
			services.AddSingleton<LauncherBackgroundViewModel>();
			services.AddSingleton<AccountListViewModel>();
			services.AddSingleton<AccountDialogViewModel>();
			services.AddSingleton<AccountAppearanceViewModel>();
			services.AddSingleton<AccountOfflineUuidViewModel>();
			services.AddSingleton<AccountPurchaseViewModel>();
			services.AddSingleton<AccountSkinModelDialogViewModel>();
			services.AddSingleton<AccountPageViewModel>();
			services.AddSingleton<DownloadTasksPageViewModel>();
			services.AddSingleton<DownloadLocalImportDialogViewModel>();
			services.AddSingleton<DownloadPageViewModel>();
			services.AddSingleton<GameSettingsEditDialogViewModel>();
			services.AddSingleton<GameSettingsDetailsViewModel>();
			services.AddSingleton<GameSettingsInstanceListViewModel>();
			services.AddSingleton<GameSettingsDialogsViewModel>();
			services.AddSingleton<GameSettingsPageViewModel>();
			services.AddSingleton<MultiplayerPageViewModel>();
			services.AddSingleton<ResourcesPageViewModel>();
			services.AddSingleton<SettingsPageViewModel>();
			services.AddSingleton<InstanceManagementViewModel>();
			services.AddSingleton<LoaderSelectionViewModel>();
			services.AddSingleton<LocalModsViewModel>();
			services.AddSingleton<LocalSavesViewModel>();
			services.AddSingleton<LocalResourcePacksViewModel>();
			services.AddSingleton<LocalShaderPacksViewModel>();
			services.AddSingleton<ModrinthSearchViewModel>();
			services.AddSingleton<GameManagementViewModel>();
			services.AddSingleton<MainViewModel>();
			services.AddSingleton<MainWindow>();
			serviceProvider = services.BuildServiceProvider();
			Log.Debug("Service provider built.");
			// StartRide：把浮动提示挂到 AppState，供不走 DI 的那些页面 VM 用。
			try
			{
				IFloatingMessageService toast = serviceProvider.GetRequiredService<IFloatingMessageService>();
				StartRide.Core.AppState.Current.Toast = toast.Show;
			}
			catch (Exception toastEx)
			{
				Log.Warning(toastEx, "Wire AppState toast failed.");
			}
			LauncherUpdateCacheCleaner updateCacheCleaner = serviceProvider.GetRequiredService<LauncherUpdateCacheCleaner>();
			updateCacheCleaner.CleanupStaleCache(Environment.ProcessPath);
			LauncherSettingsLoadResult launcherSettingsLoadResult = await serviceProvider.GetRequiredService<ISettingsService>().LoadWithMetadataAsync();
			LauncherSettings startupSettings = launcherSettingsLoadResult.Settings;
			logLevelController.SetDiagnosticLoggingEnabled(startupSettings.EnableDiagnosticLogging);
			ApplyLauncherCulture(startupSettings.LauncherLanguage);
			Log.Debug("Launcher culture initialized. Language={Language}", CultureInfo.CurrentUICulture.Name);
			if (launcherSettingsLoadResult.WasCreated)
			{
				startupSettings = await InitializeDefaultMinecraftDirectoryOnFirstRunAsync();
			}
			(LauncherSettings, MinecraftDirectoryStartupRecoveryResult) tuple = await RecoverInvalidMinecraftDirectoryOnStartupAsync(await RegisterDiscoveredMinecraftDirectoriesOnStartupAsync(startupSettings));
			LauncherSettings item = tuple.Item1;
			MinecraftDirectoryStartupRecoveryResult minecraftDirectoryStartupRecovery = tuple.Item2;
			startupSettings = item;
			base.OnStartup(e);
			await CleanupModpackWorkspacesOnStartupAsync();
			await CleanupResourceProjectWorkspacesOnStartupAsync();
			await RecoverPendingInstanceBackupsOnStartupAsync(serviceProvider.GetRequiredService<IInstanceBackupService>(), startupSettings.MinecraftDirectory);
			await RecoverPendingInstanceRenamesOnStartupAsync(serviceProvider.GetRequiredService<IInstanceRenameRecoveryService>());
			MainViewModel mainViewModel = serviceProvider.GetRequiredService<MainViewModel>();
			await mainViewModel.PrimeAsync(startupSettings, minecraftDirectoryStartupRecovery);
			IThemeService requiredService = serviceProvider.GetRequiredService<IThemeService>();
			requiredService.ApplyPreference(mainViewModel.Settings.Theme, mainViewModel.Settings.ThemeFollowSystem, mainViewModel.Settings.LauncherBackgroundOpacityPercent);
			requiredService.ApplyAccent("Blue");
			requiredService.ApplyBackgroundEffect(mainViewModel.Settings.LauncherBackgroundEffect, mainViewModel.Settings.EnableImageBackgroundControlBlur);
			MainWindow requiredService2 = serviceProvider.GetRequiredService<MainWindow>();
			serviceProvider.GetRequiredService<MainWindowPlacementService>().Restore(requiredService2, mainViewModel.Settings);
			requiredService2.Show();
			UiPerformanceLog.LogRenderEnvironment(TryReadSystemMemorySnapshot(serviceProvider), requiredService2);
			serviceProvider.GetRequiredService<UiThreadStallMonitor>().Start();
			Log.Information("Launcher startup completed. DurationMs={DurationMs} Language={Language} DiagnosticLogging={DiagnosticLogging}", Stopwatch.GetElapsedTime(startupStartedAt).TotalMilliseconds, CultureInfo.CurrentUICulture.Name, logLevelController.IsDiagnosticLoggingEnabled);
			CleanupPendingInstanceDeletionsOnStartupAsync(serviceProvider.GetRequiredService<IInstanceDeletionCleanupService>());
			CleanupPendingInstanceInstallsOnStartupAsync(serviceProvider.GetRequiredService<IInstanceInstallCleanupService>());
			CleanupModpackSandboxesOnStartupAsync(serviceProvider.GetRequiredService<IModpackSandboxCleanupService>());
			try
			{
				if (LauncherUpdateStartupCoordinator.TryConfirmStartup(e.Args, Environment.ProcessPath, out string updaterPath))
				{
					Log.Information("Launcher update startup confirmed.");
					if (updaterPath != null)
					{
						CleanupConfirmedUpdateCacheAsync(updateCacheCleaner, updaterPath);
					}
				}
			}
			catch (Exception exception)
			{
				Log.Error(exception, "Failed to confirm launcher update startup.");
			}
			CheckForLauncherUpdatesAfterAgreementAsync(mainViewModel);
		}
		catch (MinecraftDirectoryStartupRecoveryException ex)
		{
			Log.Fatal(ex, "Minecraft directory startup recovery failed.");
			MessageBox.Show(string.Format(Strings.Dialog_MinecraftDirectoryStartupRecoveryFailedMessageFormat, ex.DirectoryPath), Strings.Dialog_MinecraftDirectoryStartupRecoveryFailedTitle, MessageBoxButton.OK, MessageBoxImage.Hand);
			Shutdown(-1);
		}
		catch (Exception exception2)
		{
			Log.Fatal(exception2, "Launcher startup failed.");
			Shutdown(-1);
		}
	}

	private async Task<LauncherSettings> RegisterDiscoveredMinecraftDirectoriesOnStartupAsync(LauncherSettings startupSettings)
	{
		if (serviceProvider == null)
		{
			return startupSettings;
		}
		try
		{
			IMinecraftDirectoryDiscoveryService requiredService = serviceProvider.GetRequiredService<IMinecraftDirectoryDiscoveryService>();
			MinecraftDirectoryManagementService managementService = serviceProvider.GetRequiredService<MinecraftDirectoryManagementService>();
			IReadOnlyList<MinecraftDirectoryDiscovery> discoveredDirectories = await requiredService.DiscoverExistingDirectoriesAsync();
			if (!discoveredDirectories.Any((MinecraftDirectoryDiscovery discovery) => !startupSettings.MinecraftDirectories.Contains<string>(discovery.DirectoryPath, MinecraftDirectoryPath.Comparer) && !startupSettings.ExcludedMinecraftDirectories.Contains<string>(discovery.DirectoryPath, MinecraftDirectoryPath.Comparer)))
			{
				return startupSettings;
			}
			LauncherSettings launcherSettings = await serviceProvider.GetRequiredService<ISettingsService>().UpdateAsync(delegate(LauncherSettings settings)
			{
				managementService.RegisterDiscoveredDirectories(settings, discoveredDirectories, ResolveDiscoveredMinecraftDirectoryDisplayName);
			});
			Log.Information("Minecraft directories discovered during startup. DiscoveredCount={DiscoveredCount} RegisteredCount={RegisteredCount} CurrentMinecraftDirectory={CurrentMinecraftDirectory}", discoveredDirectories.Count, launcherSettings.MinecraftDirectories.Count, launcherSettings.MinecraftDirectory);
			return launcherSettings;
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to discover and register Minecraft directories during startup.");
			return startupSettings;
		}
	}

	private static string? ResolveDiscoveredMinecraftDirectoryDisplayName(MinecraftDirectoryKind kind)
	{
		if (kind != MinecraftDirectoryKind.Official)
		{
			return null;
		}
		return Strings.Settings_OfficialMinecraftDirectoryDisplayName;
	}

	private async Task<LauncherSettings> InitializeDefaultMinecraftDirectoryOnFirstRunAsync()
	{
		if (serviceProvider == null)
		{
			throw new InvalidOperationException("The launcher service provider is unavailable.");
		}
		LauncherPathProvider pathProvider = serviceProvider.GetRequiredService<LauncherPathProvider>();
		MinecraftDirectoryStartupInitializationService initializationService = serviceProvider.GetRequiredService<MinecraftDirectoryStartupInitializationService>();
		try
		{
			string initializedDirectory = string.Empty;
			ISettingsService settingsService = serviceProvider.GetRequiredService<ISettingsService>();
			LauncherSettings result = await Task.Run(() => settingsService.UpdateAsync(delegate(LauncherSettings settings)
			{
				initializedDirectory = initializationService.InitializeDefaultDirectory(settings, pathProvider.DefaultMinecraftDirectory);
			}));
			Log.Information("Default Minecraft directory initialized for the first launcher run. MinecraftDirectory={MinecraftDirectory}", initializedDirectory);
			return result;
		}
		catch (MinecraftDirectoryStartupRecoveryException)
		{
			throw;
		}
		catch (Exception innerException)
		{
			throw new MinecraftDirectoryStartupRecoveryException(pathProvider.DefaultMinecraftDirectory, "The initial Minecraft directory could not be initialized.", innerException);
		}
	}

	private async Task<(LauncherSettings Settings, MinecraftDirectoryStartupRecoveryResult? Recovery)> RecoverInvalidMinecraftDirectoryOnStartupAsync(LauncherSettings startupSettings)
	{
		if (serviceProvider == null)
		{
			return (Settings: startupSettings, Recovery: null);
		}
		IMinecraftDirectoryFileSystem fileSystem = serviceProvider.GetRequiredService<IMinecraftDirectoryFileSystem>();
		Microsoft.Extensions.Logging.ILogger probeLogger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MinecraftDirectoryStartupProbe");
		string minecraftDirectory = startupSettings.MinecraftDirectory;
		List<string> minecraftDirectories = startupSettings.MinecraftDirectories;
		int num = 0;
		string[] array = new string[1 + minecraftDirectories.Count];
		array[num] = minecraftDirectory;
		num++;
		Span<string> span = CollectionsMarshal.AsSpan(minecraftDirectories);
		span.CopyTo(new Span<string>(array).Slice(num, span.Length));
		_ = num + span.Length;
		MinecraftDirectoryAvailabilitySnapshot availability = await MinecraftDirectoryStartupProbe.ProbeAsync(fileSystem, new global::_003C_003Ez__ReadOnlyArray<string>(array), null, probeLogger);
		if (availability.DirectoryIsAccessible(startupSettings.MinecraftDirectory))
		{
			return (Settings: startupSettings, Recovery: null);
		}
		LauncherPathProvider pathProvider = serviceProvider.GetRequiredService<LauncherPathProvider>();
		MinecraftDirectoryStartupRecoveryService recoveryService = serviceProvider.GetRequiredService<MinecraftDirectoryStartupRecoveryService>();
		ISettingsService settingsService = serviceProvider.GetRequiredService<ISettingsService>();
		MinecraftDirectoryStartupRecoveryResult recovery = null;
		try
		{
			LauncherSettings updatedSettings = await Task.Run(() => settingsService.UpdateAsync(delegate(LauncherSettings settings)
			{
				recovery = recoveryService.Recover(settings, pathProvider.DefaultMinecraftDirectory, availability);
			}));
			if (!(await MinecraftDirectoryStartupProbe.IsAccessibleAsync(fileSystem, updatedSettings.MinecraftDirectory, null, probeLogger)))
			{
				throw new MinecraftDirectoryStartupRecoveryException(pathProvider.DefaultMinecraftDirectory, "The recovered Minecraft directory is not accessible.");
			}
			if ((object)recovery != null)
			{
				Log.Warning("Invalid Minecraft directory recovered during startup. InvalidMinecraftDirectory={InvalidMinecraftDirectory} SelectedMinecraftDirectory={SelectedMinecraftDirectory} UsedDefaultDirectory={UsedDefaultDirectory} CreatedDefaultDirectory={CreatedDefaultDirectory}", recovery.InvalidDirectory, recovery.SelectedDirectory, recovery.UsedDefaultDirectory, recovery.CreatedDefaultDirectory);
			}
			else
			{
				Log.Information("Missing Minecraft directory recreated in place during startup. MinecraftDirectory={MinecraftDirectory}", updatedSettings.MinecraftDirectory);
			}
			return (Settings: updatedSettings, Recovery: recovery);
		}
		catch (MinecraftDirectoryStartupRecoveryException)
		{
			throw;
		}
		catch (Exception innerException)
		{
			throw new MinecraftDirectoryStartupRecoveryException(recovery?.SelectedDirectory ?? pathProvider.DefaultMinecraftDirectory, "The recovered Minecraft directory could not be saved.", innerException);
		}
	}

	private static SystemMemorySnapshot? TryReadSystemMemorySnapshot(IServiceProvider serviceProvider)
	{
		try
		{
			return serviceProvider.GetRequiredService<ISystemMemoryService>().GetSnapshot();
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Failed to read the system memory snapshot for the render environment log.");
			return null;
		}
	}

	private static async Task CleanupConfirmedUpdateCacheAsync(LauncherUpdateCacheCleaner cacheCleaner, string updaterPath)
	{
		try
		{
			await cacheCleaner.CleanupConfirmedUpdateAsync(updaterPath).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Confirmed launcher update cache cleanup failed; startup cleanup will retry later.");
		}
	}

	private static async Task CleanupPendingInstanceDeletionsOnStartupAsync(IInstanceDeletionCleanupService cleanupService)
	{
		try
		{
			await cleanupService.CleanupPendingAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Pending instance deletion cleanup failed; startup cleanup will retry later.");
		}
	}

	private static async Task CleanupPendingInstanceInstallsOnStartupAsync(IInstanceInstallCleanupService cleanupService)
	{
		try
		{
			await cleanupService.CleanupPendingAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Pending instance installation cleanup failed; startup cleanup will retry later.");
		}
	}

	private static async Task CleanupModpackSandboxesOnStartupAsync(IModpackSandboxCleanupService cleanupService)
	{
		try
		{
			await cleanupService.CleanupStaleAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Modpack loader sandbox cleanup failed; startup cleanup will retry later.");
		}
	}

	private static async Task RecoverPendingInstanceRenamesOnStartupAsync(IInstanceRenameRecoveryService recoveryService)
	{
		try
		{
			await recoveryService.RecoverPendingAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Pending instance rename recovery failed before instance scanning.");
		}
	}

	private static async Task RecoverPendingInstanceBackupsOnStartupAsync(IInstanceBackupService backupService, string minecraftDirectory)
	{
		try
		{
			await backupService.RecoverPendingRestoresAsync(minecraftDirectory).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Pending instance backup recovery failed before instance scanning.");
		}
	}

	private static async Task CheckForLauncherUpdatesAfterAgreementAsync(MainViewModel mainViewModel)
	{
		_ = 1;
		try
		{
			// StartRide：「启动时检查启动器更新」开关以前没有任何代码读它，
			// 关掉照样每次启动都去检查 -> 这里接上。
			if (!StartRide.Core.AppSettings.Current.AutoUpdate)
			{
				return;
			}

			if (await mainViewModel.WaitForUserAgreementDecisionAsync())
			{
				await mainViewModel.SettingsPage.Info.CheckUpdatesOnStartupAsync();
			}
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Unexpected failure while running startup launcher update check.");
		}
	}

	private static void ApplyLauncherCulture(string? language)
	{
		CultureInfo.CurrentUICulture = (CultureInfo.CurrentCulture = (CultureInfo.DefaultThreadCurrentUICulture = (CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo(LauncherLanguages.Normalize(language)))));
	}

	protected override void OnExit(ExitEventArgs e)
	{
		if (isUpdateApplyMode)
		{
			base.OnExit(e);
			return;
		}
		try
		{
			Log.Information("Launcher exit started. ExitCode={ExitCode}", e.ApplicationExitCode);
			serviceProvider?.Dispose();
			LauncherLogConfiguration.PruneOldLogFiles(LauncherLogConfiguration.ResolveLogDirectory(), DateTimeOffset.Now);
			Log.Information("Launcher exit completed.");
		}
		finally
		{
			Log.CloseAndFlush();
			base.OnExit(e);
		}
	}

	private async Task CleanupModpackWorkspacesOnStartupAsync()
	{
		if (serviceProvider == null)
		{
			return;
		}
		try
		{
			await serviceProvider.GetRequiredService<IModpackWorkspaceCleanupService>().CleanupAllAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to clean modpack workspace cache on startup.");
		}
	}

	private async Task CleanupResourceProjectWorkspacesOnStartupAsync()
	{
		if (serviceProvider == null)
		{
			return;
		}
		try
		{
			await serviceProvider.GetRequiredService<IResourceProjectInstallationService>().CleanupStaleWorkspacesAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to clean resource project installation workspaces on startup.");
		}
	}

	private void RegisterUnhandledExceptionLogging()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Expected O, but got Unknown
		object obj = _003C_003Ec._003C_003E9__23_0;
		if (obj == null)
		{
			DispatcherUnhandledExceptionEventHandler val = delegate(object _, DispatcherUnhandledExceptionEventArgs args)
			{
				Log.Error(args.Exception, "Unhandled dispatcher exception.");
			};
			_003C_003Ec._003C_003E9__23_0 = val;
			obj = (object)val;
		}
		base.DispatcherUnhandledException += (DispatcherUnhandledExceptionEventHandler)obj;
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs args)
		{
			if (args.ExceptionObject is Exception exception)
			{
				Log.Fatal(exception, "Unhandled app domain exception. IsTerminating={IsTerminating}", args.IsTerminating);
			}
			else
			{
				Log.Fatal("Unhandled app domain exception object. IsTerminating={IsTerminating}", args.IsTerminating);
			}
		};
		TaskScheduler.UnobservedTaskException += delegate(object? _, UnobservedTaskExceptionEventArgs args)
		{
			Log.Error(args.Exception, "Unobserved task exception.");
		};
	}
}
