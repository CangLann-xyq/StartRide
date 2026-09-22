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
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.Utilities;
using Launcher.App.ViewModels.Account;
using Launcher.Application.Accounts;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Home;

public sealed partial class HomePageViewModel : ObservableObject
{
	private readonly ILaunchService launchService;

	private readonly AccountPageViewModel accountPage;

	private readonly IStatusService statusService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly IWindowService windowService;

	private readonly IUiDispatcher uiDispatcher;

	private readonly IAccountDialogService? accountDialogService;

	private readonly Action<double> reportProgressPercent;

	private readonly Func<GameInstance?, Task> openGameSettingsForInstance;

	private readonly ILogger<HomePageViewModel> logger;

	private LauncherSettings settings = new LauncherSettings();

	private CancellationTokenSource? launchCancellationTokenSource;

	private IDisposable? launchSpeedMeterLifetime;

	private IProgress<LauncherProgress>? launchProgress;

	[ObservableProperty]
	private bool isLaunching;

	[ObservableProperty]
	private string launchStatusMessage = string.Empty;

	[ObservableProperty]
	private double launchProgressPercent;

	[ObservableProperty]
	private string launchDownloadSpeedText = string.Empty;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelLaunchCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? openSelectedInstanceSettingsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? launchCommand;

	public HomeLaunchGameListViewModel LaunchGames { get; }

	public bool HasSelectedAccount => accountPage.SelectedAccount != null;

	public GameInstance? SelectedInstance => LaunchGames.SelectedInstance;

	public bool CanLaunchSelectedGame
	{
		get
		{
			if (HasSelectedAccount && SelectedInstance != null)
			{
				return !IsLaunching;
			}
			return false;
		}
	}

	public bool HasLaunchProgress
	{
		get
		{
			if (IsLaunching)
			{
				return !string.IsNullOrWhiteSpace(LaunchStatusMessage);
			}
			return false;
		}
	}

	public bool HasLaunchDownloadSpeedText
	{
		get
		{
			if (IsLaunching)
			{
				return !string.IsNullOrWhiteSpace(LaunchDownloadSpeedText);
			}
			return false;
		}
	}

	public ObservableCollection<HomeLaunchInstanceItem> LaunchInstances => LaunchGames.LaunchInstances;

	public bool HasLaunchInstances => LaunchGames.HasLaunchInstances;

	public bool HasNoLaunchInstances => LaunchGames.HasNoLaunchInstances;

	public HomeLaunchInstanceItem? SelectedLaunchInstanceItem => LaunchGames.SelectedLaunchInstanceItem;

	public bool HasSelectedLaunchInstance => LaunchGames.HasSelectedLaunchInstance;

	public bool IsLaunchMenuPinned => LaunchGames.IsLaunchMenuPinned;

	public IAsyncRelayCommand SelectLaunchInstanceCommand => LaunchGames.SelectLaunchInstanceCommand;

	public IAsyncRelayCommand ToggleLaunchMenuPinnedCommand => LaunchGames.ToggleLaunchMenuPinnedCommand;

	public bool CanOpenSelectedInstanceSettings
	{
		get
		{
			if (SelectedInstance != null)
			{
				return !IsLaunching;
			}
			return false;
		}
	}

	public string? HomeAvatarUrl
	{
		get
		{
			LauncherAccount selectedAccount = accountPage.SelectedAccount;
			if (selectedAccount == null)
			{
				return null;
			}
			if (!string.IsNullOrWhiteSpace(selectedAccount.AvatarSource))
			{
				return selectedAccount.AvatarSource;
			}
			if (!selectedAccount.IsMicrosoft || string.IsNullOrWhiteSpace(selectedAccount.Uuid))
			{
				return "https://minotar.net/avatar/Steve/576.png";
			}
			return "https://crafatar.com/avatars/" + selectedAccount.Uuid + "?size=576&overlay";
		}
	}

	public string HomeAccountDisplayName => accountPage.SelectedAccount?.DisplayName ?? Strings.Home_NoAccountSelected;

	/// <summary>账户昵称首字母（大写），用于 StartRide 的圆形首字母头像。</summary>
	public string HomeAccountInitial
	{
		get
		{
			string? name = accountPage.SelectedAccount?.DisplayName;
			if (string.IsNullOrWhiteSpace(name))
			{
				return "S";
			}
			return name.Trim().Substring(0, 1).ToUpperInvariant();
		}
	}

	public string HomeVersionDisplayName
	{
		get
		{
			if (SelectedInstance == null)
			{
				return Strings.Home_NoVersionSelected;
			}
			if (!string.IsNullOrWhiteSpace(SelectedInstance.Name))
			{
				// StartRide 只有官方正式版这一条通道，版本名后面带上「官方正式版」，
				// 免得用户以为还有快照版/测试版之类的特殊版本可选。
				return SelectedInstance.Name + " · " + Strings.Home_VersionOfficialSuffix;
			}
			if (!string.IsNullOrWhiteSpace(SelectedInstance.VersionName))
			{
				return SelectedInstance.VersionName;
			}
			if (!string.IsNullOrWhiteSpace(SelectedInstance.MinecraftVersion))
			{
				return SelectedInstance.MinecraftVersion;
			}
			return Strings.Home_NoVersionSelected;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLaunching
	{
		get
		{
			return isLaunching;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLaunching, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLaunching);
				isLaunching = value;
				OnIsLaunchingChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLaunching);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LaunchStatusMessage
	{
		get
		{
			return launchStatusMessage;
		}
		[MemberNotNull("launchStatusMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(launchStatusMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchStatusMessage);
				launchStatusMessage = value;
				OnLaunchStatusMessageChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchStatusMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double LaunchProgressPercent
	{
		get
		{
			return launchProgressPercent;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(launchProgressPercent, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchProgressPercent);
				launchProgressPercent = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchProgressPercent);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LaunchDownloadSpeedText
	{
		get
		{
			return launchDownloadSpeedText;
		}
		[MemberNotNull("launchDownloadSpeedText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(launchDownloadSpeedText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LaunchDownloadSpeedText);
				launchDownloadSpeedText = value;
				OnLaunchDownloadSpeedTextChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LaunchDownloadSpeedText);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelLaunchCommand => cancelLaunchCommand ?? (cancelLaunchCommand = new RelayCommand(CancelLaunch, () => IsLaunching));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand OpenSelectedInstanceSettingsCommand => openSelectedInstanceSettingsCommand ?? (openSelectedInstanceSettingsCommand = new AsyncRelayCommand(OpenSelectedInstanceSettingsAsync, () => CanOpenSelectedInstanceSettings));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand LaunchCommand => launchCommand ?? (launchCommand = new AsyncRelayCommand(LaunchAsync, () => CanLaunchSelectedGame));

	public event EventHandler<JavaRequirementNotMetEventArgs>? JavaRequirementNotMet;

	public event EventHandler<LaunchFailureReport>? LaunchFailureReported;

	public event EventHandler? LaunchActivityChanged;

	[RelayCommand(CanExecute = "IsLaunching")]
	private void CancelLaunch()
	{
		CancellationTokenSource cancellationTokenSource = launchCancellationTokenSource;
		if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
		{
			GameInstance selectedInstance = SelectedInstance;
			logger.LogInformation("Launch cancellation requested. InstanceId={InstanceId} InstanceName={InstanceName} VersionName={VersionName}", selectedInstance?.Id, selectedInstance?.Name, selectedInstance?.VersionName);
			cancellationTokenSource.Cancel();
		}
	}

	[RelayCommand(CanExecute = "CanOpenSelectedInstanceSettings")]
	private Task OpenSelectedInstanceSettingsAsync()
	{
		return openGameSettingsForInstance(SelectedInstance);
	}

	private IProgress<LauncherProgress> CreateProgress()
	{
		return launchProgress ?? throw new InvalidOperationException("Launch progress has not been initialized.");
	}

	private void ReportLaunchProgress(LauncherProgress progress)
	{
		if (IsLaunching)
		{
			if ((object)progress.DownloadSpeedTelemetry != null)
			{
				LaunchDownloadSpeedText = LauncherProgressTextFormatter.FormatDownloadSpeed(progress.DownloadSpeedTelemetry);
				return;
			}
			string message = (LaunchStatusMessage = FormatLaunchProgress(progress));
			LaunchProgressPercent = MergeLaunchProgressPercent(LaunchProgressPercent, progress.Percent);
			statusService.Report(message);
			reportProgressPercent(LaunchProgressPercent);
		}
	}

	internal static double MergeLaunchProgressPercent(double current, double? reported)
	{
		if (reported.HasValue)
		{
			double valueOrDefault = reported.GetValueOrDefault();
			return Math.Clamp(Math.Max(current, valueOrDefault), 0.0, 100.0);
		}
		return Math.Clamp(current, 0.0, 100.0);
	}

	public HomePageViewModel(ILaunchService launchService, AccountPageViewModel accountPage, IStatusService statusService, IFloatingMessageService floatingMessageService, IWindowService windowService, IUiDispatcher uiDispatcher, Action<double> reportProgressPercent, Func<GameInstance, Task<bool>> selectLaunchInstance, Func<bool, Task<bool>> setLaunchMenuPinned, Func<GameInstance?, Task> openGameSettingsForInstance, ILogger<HomePageViewModel>? logger = null, IAccountDialogService? accountDialogService = null)
	{
		this.launchService = launchService;
		this.accountPage = accountPage;
		this.statusService = statusService;
		this.floatingMessageService = floatingMessageService;
		this.windowService = windowService;
		this.uiDispatcher = uiDispatcher;
		this.accountDialogService = accountDialogService;
		this.reportProgressPercent = reportProgressPercent;
		this.openGameSettingsForInstance = openGameSettingsForInstance;
		this.logger = logger ?? NullLogger<HomePageViewModel>.Instance;
		LaunchGames = new HomeLaunchGameListViewModel(statusService, selectLaunchInstance, setLaunchMenuPinned);
		LaunchGames.PropertyChanged += LaunchGames_PropertyChanged;
		accountPage.PropertyChanged += delegate(object? _, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "SelectedAccount")
			{
				NotifyAccountStateChanged();
			}
		};
		// StartRide：首页底部"游戏运行中/累计游玩 + 结束游戏"那一行（见 HomePageViewModel.StartRide.cs）
		InitializeGameRuntime();
	}

	public void Initialize(LauncherSettings launcherSettings, GameInstance? instance)
	{
		settings = launcherSettings;
		LaunchGames.SetLaunchMenuPinned(launcherSettings.IsHomeLaunchMenuPinned);
		SetSelectedInstance(instance);
		NotifyAccountStateChanged();
		NotifyInstanceStateChanged();
	}

	public void SetSettings(LauncherSettings launcherSettings)
	{
		settings = launcherSettings;
		LaunchGames.SetLaunchMenuPinned(launcherSettings.IsHomeLaunchMenuPinned);
	}

	public void SetSelectedInstance(GameInstance? instance)
	{
		LaunchGames.SetSelectedInstance(instance);
	}

	public void SetLaunchInstances(IEnumerable<GameInstance> instances)
	{
		LaunchGames.SetLaunchInstances(instances);
	}

	public bool ApplyInstanceCatalog(IEnumerable<GameInstance> instances, long catalogRevision)
	{
		return LaunchGames.ApplyInstanceCatalog(instances, catalogRevision);
	}

	private void ReportLaunchWarnings(GameLaunchSession session)
	{
		foreach (LaunchWarningKind item in session.Warnings.Distinct())
		{
			string text = ((item != LaunchWarningKind.OfflineSkinUnavailable) ? null : Strings.Status_LaunchOfflineSkinUnavailable);
			string text2 = text;
			if (text2 != null)
			{
				statusService.Report(text2);
				floatingMessageService.Show(text2);
			}
		}
	}

	private void ObserveGameExit(GameLaunchSession session)
	{
		Task.Run(async delegate
		{
			try
			{
				LaunchExitResult launchExitResult = await session.ExitTask;
				if (launchExitResult.IsFailure && (object)launchExitResult.FailureReport != null && session.TryMarkExitHandled())
				{
					ReportLaunchFailure(launchExitResult.FailureReport);
				}
			}
			catch (Exception exception)
			{
				logger.LogWarning(exception, "Failed to observe Minecraft process exit. InstanceId={InstanceId}", session.InstanceId);
			}
		});
	}

	private void ReportLaunchFailure(LaunchFailureReport report)
	{
		if (!uiDispatcher.HasAccess)
		{
			uiDispatcher.Post(delegate
			{
				ReportLaunchFailure(report);
			});
		}
		else
		{
			statusService.Report(GetLaunchFailureStatus(report.Kind));
			LaunchFailureReported?.Invoke(this, report);
		}
	}

	private static string GetLaunchFailureStatus(LaunchFailureKind kind)
	{
		return kind switch
		{
			LaunchFailureKind.StartupProcessExited => Strings.Status_LaunchProcessExited, 
			LaunchFailureKind.RuntimeAbnormalExit => Strings.Status_LaunchRuntimeAbnormalExit, 
			LaunchFailureKind.StartupAbnormalExit => Strings.Status_LaunchAbnormalExit, 
			_ => Strings.Status_LaunchFailed, 
		};
	}

	private static bool IsAutomaticJavaRuntimeDiscoveryFailure(JavaRuntimeSelectionFailureReason reason)
	{
		if ((uint)(reason - 1) <= 1u)
		{
			return true;
		}
		return false;
	}

	private static bool ShouldShowJavaRequirementDialog(JavaRuntimeSelectionFailureReason reason)
	{
		bool flag = IsAutomaticJavaRuntimeDiscoveryFailure(reason);
		if (!flag)
		{
			bool flag2 = (uint)(reason - 5) <= 1u;
			flag = flag2;
		}
		return flag;
	}

	[RelayCommand(CanExecute = "CanLaunchSelectedGame")]
	private async Task LaunchAsync()
	{
		await LaunchCoreAsync(null, null);
	}

	public Task ForceLaunchIgnoringJavaRequirementAsync(GameInstance instance)
	{
		return LaunchCoreAsync(new LaunchRequestOptions(IgnoreJavaVersionRequirement: true), instance);
	}

	private async Task LaunchCoreAsync(LaunchRequestOptions? options, GameInstance? forcedInstance)
	{
		LauncherAccount selectedAccount = accountPage.SelectedAccount;
		GameInstance launchInstance = forcedInstance ?? SelectedInstance;
		if (IsLaunching || launchInstance == null || selectedAccount == null)
		{
			statusService.Report(Strings.Status_NoLaunchableInstance);
			return;
		}
		try
		{
			GameLaunchSession gameLaunchSession = await StartGameSessionAsync(launchInstance, selectedAccount, options);
			if (gameLaunchSession != null)
			{
				ObserveGameExit(gameLaunchSession);
				ReportLaunchWarnings(gameLaunchSession);
				if (ShouldMinimizeLauncherAfterLaunch(launchInstance))
				{
					windowService.Minimize();
				}
			}
		}
		catch (OperationCanceledException exception) when (launchCancellationTokenSource?.IsCancellationRequested ?? false)
		{
			logger.LogDebug(exception, "Launch cancellation completed. InstanceId={InstanceId} InstanceName={InstanceName}", launchInstance.Id, launchInstance.Name);
			statusService.Report(Strings.Status_LaunchCanceled);
			floatingMessageService.Show(Strings.Status_LaunchCanceled);
		}
		catch (LaunchAccountSessionException ex)
		{
			string message = ex.Reason switch
			{
				LaunchAccountSessionFailureReason.AuthenticationNotConfigured => Strings.Status_MicrosoftLoginNotConfigured, 
				LaunchAccountSessionFailureReason.AuthenticationApplicationNotAuthorized => Strings.Status_MicrosoftApplicationNotAuthorized, 
				LaunchAccountSessionFailureReason.GameOwnershipRequired => Strings.Status_MinecraftJavaOwnershipRequired, 
				LaunchAccountSessionFailureReason.AuthenticationServerUnavailable => Strings.Status_MicrosoftAuthenticationServerUnavailable, 
				LaunchAccountSessionFailureReason.CredentialStorageFailed => Strings.Status_MicrosoftCredentialStorageFailed, 
				_ => Strings.Status_LaunchAccountUnavailable, 
			};
			statusService.Report(message);
			floatingMessageService.Show(message);
		}
		catch (LaunchFailedException ex2)
		{
			if (ex2.InnerException is JavaRuntimeSelectionException ex3 && ShouldShowJavaRequirementDialog(ex3.Reason))
			{
				JavaRequirementNotMet?.Invoke(this, new JavaRequirementNotMetEventArgs(ex3.RequiredMajorVersion, ex3.Reason, launchInstance, ex3.CurrentMajorVersion, ex3.CurrentVersion, ex3.RecommendedMajorVersion));
				statusService.Report(Strings.Status_JavaSelectionFailed);
			}
			else
			{
				ReportLaunchFailure(ex2.Report);
			}
		}
		catch (LaunchProcessExitedException ex4)
		{
			ReportLaunchFailure(ex4.Report);
		}
		catch (InstanceRepairException)
		{
			statusService.Report(Strings.Status_LaunchInstanceRepairFailed);
		}
		catch (JavaRuntimeSelectionException ex6)
		{
			if (ShouldShowJavaRequirementDialog(ex6.Reason))
			{
				JavaRequirementNotMet?.Invoke(this, new JavaRequirementNotMetEventArgs(ex6.RequiredMajorVersion, ex6.Reason, launchInstance, ex6.CurrentMajorVersion, ex6.CurrentVersion, ex6.RecommendedMajorVersion));
			}
			statusService.Report(Strings.Status_JavaSelectionFailed);
		}
		catch (Exception)
		{
			statusService.Report(Strings.Status_LaunchFailed);
		}
		finally
		{
			ResetLaunchProgress();
		}
	}

	private async Task<GameLaunchSession?> StartGameSessionAsync(GameInstance launchInstance, LauncherAccount account, LaunchRequestOptions? options)
	{
		CancellationTokenSource cancellationTokenSource = BeginLaunchProgress();
		try
		{
			return await launchService.LaunchAsync(launchInstance, account, settings, CreateProgress(), options, cancellationTokenSource.Token);
		}
		catch (LaunchAccountSessionException ex) when (ex.Reason == LaunchAccountSessionFailureReason.ReauthenticationRequired && (account.IsThirdParty || account.IsMicrosoft) && accountDialogService != null)
		{
			return await RetryAfterReauthenticationAsync(launchInstance, account, options, cancellationTokenSource.Token);
		}
	}

	private async Task<GameLaunchSession?> RetryAfterReauthenticationAsync(GameInstance launchInstance, LauncherAccount account, LaunchRequestOptions? options, CancellationToken cancellationToken)
	{
		statusService.Report(account.IsThirdParty ? Strings.Status_ThirdPartyReauthenticationRequired : Strings.Status_MicrosoftReauthenticationRequired);
		if (!((!account.IsThirdParty) ? (await accountDialogService.ShowMicrosoftReauthenticationDialogAsync(account)) : (await accountDialogService.ShowThirdPartyReauthenticationDialogAsync(account))))
		{
			statusService.Report(Strings.Status_LaunchCanceled);
			floatingMessageService.Show(Strings.Status_LaunchCanceled);
			return null;
		}
		LauncherAccount selectedAccount = accountPage.SelectedAccount;
		if (selectedAccount == null || !string.Equals(selectedAccount.Id, account.Id, StringComparison.Ordinal))
		{
			throw new LaunchAccountSessionException(LaunchAccountSessionFailureReason.ReauthenticationRequired, "The reauthenticated account is no longer selected.");
		}
		return await launchService.LaunchAsync(launchInstance, selectedAccount, settings, CreateProgress(), options, cancellationToken);
	}

	private void LaunchGames_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
		case "SelectedInstance":
			OnPropertyChanged("SelectedInstance");
			NotifyInstanceStateChanged();
			break;
		case "HasLaunchInstances":
			OnPropertyChanged("HasLaunchInstances");
			break;
		case "HasNoLaunchInstances":
			OnPropertyChanged("HasNoLaunchInstances");
			break;
		case "SelectedLaunchInstanceItem":
			OnPropertyChanged("SelectedLaunchInstanceItem");
			break;
		case "HasSelectedLaunchInstance":
			OnPropertyChanged("HasSelectedLaunchInstance");
			break;
		case "IsLaunchMenuPinned":
			OnPropertyChanged("IsLaunchMenuPinned");
			break;
		}
	}

	private void NotifyAccountStateChanged()
	{
		OnPropertyChanged("HasSelectedAccount");
		OnPropertyChanged("HomeAvatarUrl");
		OnPropertyChanged("HomeAccountDisplayName");
		OnPropertyChanged("HomeAccountInitial");
		OnPropertyChanged("CanLaunchSelectedGame");
		LaunchCommand.NotifyCanExecuteChanged();
	}

	private void NotifyInstanceStateChanged()
	{
		OnPropertyChanged("HomeVersionDisplayName");
		OnPropertyChanged("CanLaunchSelectedGame");
		OnPropertyChanged("CanOpenSelectedInstanceSettings");
		LaunchCommand.NotifyCanExecuteChanged();
		OpenSelectedInstanceSettingsCommand.NotifyCanExecuteChanged();
	}

	private CancellationTokenSource BeginLaunchProgress()
	{
		launchCancellationTokenSource?.Dispose();
		launchSpeedMeterLifetime?.Dispose();
		launchCancellationTokenSource = new CancellationTokenSource();
		IProgress<LauncherProgress> progress = new Progress<LauncherProgress>(ReportLaunchProgress);
		launchProgress = DownloadSpeedTaskProgress.Create(progress.Report, progress.Report, out IDisposable lifetime);
		launchSpeedMeterLifetime = lifetime;
		IsLaunching = true;
		LaunchProgressPercent = 0.0;
		LaunchDownloadSpeedText = string.Empty;
		LaunchStatusMessage = Strings.Status_LaunchPreparing;
		statusService.Report(LaunchStatusMessage);
		reportProgressPercent(LaunchProgressPercent);
		return launchCancellationTokenSource;
	}

	private void ResetLaunchProgress()
	{
		launchSpeedMeterLifetime?.Dispose();
		launchSpeedMeterLifetime = null;
		launchProgress = null;
		launchCancellationTokenSource?.Dispose();
		launchCancellationTokenSource = null;
		LaunchProgressPercent = 0.0;
		reportProgressPercent(0.0);
		LaunchDownloadSpeedText = string.Empty;
		LaunchStatusMessage = string.Empty;
		IsLaunching = false;
	}

	private bool ShouldMinimizeLauncherAfterLaunch(GameInstance instance)
	{
		if (instance.LaunchSettingsMode != LaunchSettingsMode.UseGlobal)
		{
			return instance.MinimizeLauncherAfterLaunch;
		}
		// 全局默认统一以 StartRide 配置为准：设置页两个入口（通用 / 内存与启动）
		// 都写 AppSettings.MinimizeToTray，读同一份就不会再出现"勾了没反应"。
		return StartRide.Core.AppSettings.Current.MinimizeToTray;
	}

	internal static string FormatLaunchProgress(LauncherProgress progress)
	{
		switch (progress.Stage)
		{
		case "Launch.CheckingInstance":
			return Strings.Status_LaunchCheckingInstance;
		case "Launch.RepairingMetadata":
			return Strings.Status_LaunchRepairingMetadata;
		case "Launch.RepairingLoaderInstaller":
			return Strings.Status_LaunchRepairingLoaderInstaller;
		case "Launch.RunningLoaderInstaller":
			return Strings.Status_LaunchRunningLoaderInstaller;
		case "Launch.FinalizingLoaderVersion":
			return Strings.Status_LaunchFinalizingLoaderVersion;
		case "Launch.PublishingLoaderArtifacts":
			return Strings.Status_LaunchPublishingLoaderArtifacts;
		case "Launch.RevalidatingFiles":
			return Strings.Status_LaunchRevalidatingFiles;
		case "Launch.RepairingJar":
			return Strings.Status_LaunchRepairingJar;
		case "Launch.RepairingLibraries":
			return Strings.Status_LaunchRepairingLibraries;
		case "Launch.RepairingAssets":
			return Strings.Status_LaunchRepairingAssets;
		case "Launch.RepairingLogging":
			return Strings.Status_LaunchRepairingLogging;
		case "Install.CheckingJava":
			return Strings.Status_LaunchCheckingJava;
		case "Install.DownloadingJava":
			return Strings.Status_InstallDownloadingJava;
		case "Launch.CheckingJava":
			return Strings.Status_LaunchCheckingJava;
		case "Launch.DownloadingJava":
			return Strings.Status_InstallDownloadingJava;
		case "Launch.RunningPreLaunchCommand":
			return Strings.Status_LaunchRunningPreLaunchCommand;
		case "Launch.PreparingOfflineSkin":
			return Strings.Status_LaunchPreparingOfflineSkin;
		case "Launch.PreparingProcess":
			return Strings.Status_LaunchPreparingProcess;
		case "Launch.StartingProcess":
			return Strings.Status_LaunchStartingProcess;
		case "Files":
			return Strings.Status_LaunchCheckingFiles;
		case "Bytes":
			return Strings.Status_LaunchDownloadingFiles;
		default:
			if (!string.IsNullOrWhiteSpace(progress.Message))
			{
				return progress.Message;
			}
			return Strings.Status_LaunchPreparing;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsLaunchingChanged(bool value)
	{
		OnPropertyChanged("CanLaunchSelectedGame");
		OnPropertyChanged("HasLaunchProgress");
		OnPropertyChanged("HasLaunchDownloadSpeedText");
		OnPropertyChanged("CanOpenSelectedInstanceSettings");
		LaunchCommand.NotifyCanExecuteChanged();
		CancelLaunchCommand.NotifyCanExecuteChanged();
		OpenSelectedInstanceSettingsCommand.NotifyCanExecuteChanged();
		LaunchActivityChanged?.Invoke(this, EventArgs.Empty);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLaunchStatusMessageChanged(string value)
	{
		OnPropertyChanged("HasLaunchProgress");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLaunchDownloadSpeedTextChanged(string value)
	{
		OnPropertyChanged("HasLaunchDownloadSpeedText");
	}
}
