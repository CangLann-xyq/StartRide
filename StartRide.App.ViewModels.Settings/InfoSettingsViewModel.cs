using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Models;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Shell;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.Settings;

public sealed class InfoSettingsViewModel : SettingsSectionViewModelBase
{
	private enum UpdateCheckPresentation
	{
		Manual,
		StartupSilent
	}

	private readonly IStatusService statusService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly IExternalLinkService externalLinkService;

	private readonly LegalReaderViewModel legalReader;

	private readonly ILauncherUpdateService launcherUpdateService;

	private readonly ILauncherSelfUpdateService launcherSelfUpdateService;

	private readonly IApplicationExitService applicationExitService;

	private readonly ILogger<InfoSettingsViewModel> logger;

	private LauncherUpdateInfo? availableUpdate;

	private string? updateDialogReleasePageUrl;

	private string? updateDialogDownloadUrl;

	private bool isUpdateCheckRunning;

	[ObservableProperty]
	private SettingsUpdateChannelOption? selectedUpdateChannelOption;

	[ObservableProperty]
	private bool isUpdateAvailableDialogOpen;

	[ObservableProperty]
	private string updateDialogVersionText = string.Empty;

	[ObservableProperty]
	private string updateDialogMessage = string.Empty;

	private string updateDialogChangelog = string.Empty;

	private string updateProgressText = string.Empty;

	private double updateProgressPercent;

	private bool isUpdateResultDialogOpen;

	private string updateResultTitle = string.Empty;

	private string updateResultMessage = string.Empty;

	private bool updateResultIsFailure;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? closeUpdateResultCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? retryUpdateCommand;

	[ObservableProperty]
	private bool isCheckingUpdates;

	[ObservableProperty]
	private bool isStartingUpdate;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openGithubRepositoryCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openProjectHomeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openFeedbackCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<InfoReferenceProjectItem?>? openReferenceProjectCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<LegalDocumentItem?>? openLegalDocumentCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? checkUpdatesCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openUpdateChangelogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelUpdateDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmUpdateCommand;

	public string LauncherVersionText { get; }

	public IReadOnlyList<InfoReferenceProjectItem> ReferenceProjects { get; }

	public IReadOnlyList<LegalDocumentItem> LegalDocuments { get; }

	public ObservableCollection<SettingsUpdateChannelOption> UpdateChannelOptions { get; }

	public string CheckUpdatesButtonText
	{
		get
		{
			if (!IsCheckingUpdates)
			{
				return Strings.Settings_CheckUpdatesButton;
			}
			return Strings.Status_CheckingUpdates;
		}
	}

	public string ConfirmUpdateButtonText
	{
		get
		{
			if (!IsStartingUpdate)
			{
				return Strings.Dialog_UpdateButton;
			}
			return string.IsNullOrEmpty(updateProgressText) ? Strings.Status_DownloadingLauncherUpdate : updateProgressText;
		}
	}

	private string updateDialogTitle = string.Empty;

	public string UpdateDialogTitle
	{
		get => updateDialogTitle;
		set => SetProperty(ref updateDialogTitle, value);
	}

	public string UpdateProgressText
	{
		get => updateProgressText;
		set
		{
			if (SetProperty(ref updateProgressText, value))
			{
				OnPropertyChanged("ConfirmUpdateButtonText");
			}
		}
	}

	public double UpdateProgressPercent
	{
		get => updateProgressPercent;
		set => SetProperty(ref updateProgressPercent, value);
	}

	public bool IsUpdateResultDialogOpen
	{
		get => isUpdateResultDialogOpen;
		set => SetProperty(ref isUpdateResultDialogOpen, value);
	}

	public string UpdateResultTitle
	{
		get => updateResultTitle;
		set => SetProperty(ref updateResultTitle, value);
	}

	public string UpdateResultMessage
	{
		get => updateResultMessage;
		set => SetProperty(ref updateResultMessage, value);
	}

	public bool UpdateResultIsFailure
	{
		get => updateResultIsFailure;
		set => SetProperty(ref updateResultIsFailure, value);
	}

	public IRelayCommand CloseUpdateResultCommand => closeUpdateResultCommand ?? (closeUpdateResultCommand = new RelayCommand(CloseUpdateResult));

	public IAsyncRelayCommand RetryUpdateCommand => retryUpdateCommand ?? (retryUpdateCommand = new AsyncRelayCommand(RetryUpdateAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsUpdateChannelOption? SelectedUpdateChannelOption
	{
		get
		{
			return selectedUpdateChannelOption;
		}
		set
		{
			if (!EqualityComparer<SettingsUpdateChannelOption>.Default.Equals(selectedUpdateChannelOption, value))
			{
				SettingsUpdateChannelOption oldValue = selectedUpdateChannelOption;
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedUpdateChannelOption);
				selectedUpdateChannelOption = value;
				OnSelectedUpdateChannelOptionChanged(oldValue, value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedUpdateChannelOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsUpdateAvailableDialogOpen
	{
		get
		{
			return isUpdateAvailableDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isUpdateAvailableDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsUpdateAvailableDialogOpen);
				isUpdateAvailableDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsUpdateAvailableDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string UpdateDialogVersionText
	{
		get
		{
			return updateDialogVersionText;
		}
		[MemberNotNull("updateDialogVersionText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(updateDialogVersionText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.UpdateDialogVersionText);
				updateDialogVersionText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.UpdateDialogVersionText);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string UpdateDialogMessage
	{
		get
		{
			return updateDialogMessage;
		}
		[MemberNotNull("updateDialogMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(updateDialogMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.UpdateDialogMessage);
				updateDialogMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.UpdateDialogMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsCheckingUpdates
	{
		get
		{
			return isCheckingUpdates;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isCheckingUpdates, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsCheckingUpdates);
				isCheckingUpdates = value;
				OnIsCheckingUpdatesChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsCheckingUpdates);
			}
		}
	}

	public string UpdateDialogChangelog
	{
		get
		{
			return updateDialogChangelog;
		}
		private set
		{
			if (!EqualityComparer<string>.Default.Equals(updateDialogChangelog, value))
			{
				OnPropertyChanging("UpdateDialogChangelog");
				updateDialogChangelog = value;
				OnPropertyChanged("UpdateDialogChangelog");
				OnPropertyChanged("HasUpdateDialogChangelog");
			}
		}
	}

	public bool HasUpdateDialogChangelog => !string.IsNullOrWhiteSpace(updateDialogChangelog);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsStartingUpdate
	{
		get
		{
			return isStartingUpdate;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isStartingUpdate, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsStartingUpdate);
				isStartingUpdate = value;
				OnIsStartingUpdateChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsStartingUpdate);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenGithubRepositoryCommand => openGithubRepositoryCommand ?? (openGithubRepositoryCommand = new RelayCommand(OpenGithubRepository));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenProjectHomeCommand => openProjectHomeCommand ?? (openProjectHomeCommand = new RelayCommand(OpenProjectHome));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenFeedbackCommand => openFeedbackCommand ?? (openFeedbackCommand = new RelayCommand(OpenFeedback));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<InfoReferenceProjectItem?> OpenReferenceProjectCommand => openReferenceProjectCommand ?? (openReferenceProjectCommand = new RelayCommand<InfoReferenceProjectItem>(OpenReferenceProject));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<LegalDocumentItem?> OpenLegalDocumentCommand => openLegalDocumentCommand ?? (openLegalDocumentCommand = new RelayCommand<LegalDocumentItem?>(OpenLegalDocument));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand CheckUpdatesCommand => checkUpdatesCommand ?? (checkUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync, CanCheckUpdates, AsyncRelayCommandOptions.AllowConcurrentExecutions));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenUpdateChangelogCommand => openUpdateChangelogCommand ?? (openUpdateChangelogCommand = new RelayCommand(OpenUpdateChangelog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelUpdateDialogCommand => cancelUpdateDialogCommand ?? (cancelUpdateDialogCommand = new RelayCommand(CancelUpdateDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmUpdateCommand => confirmUpdateCommand ?? (confirmUpdateCommand = new AsyncRelayCommand(ConfirmUpdateAsync, CanConfirmUpdate));

	[RelayCommand]
	private void OpenGithubRepository()
	{
		try
		{

			logger.LogInformation("About page external link. Target=source-repository Url={Url}", StartRide.Core.SiteLinks.GitHubRepo);
			if (!externalLinkService.TryOpen(StartRide.Core.SiteLinks.GitHubRepo))
			{
				statusService.Report(Strings.Status_OpenGithubRepositoryFailed);
			}
		}
		catch (Exception)
		{
			statusService.Report(Strings.Status_OpenGithubRepositoryFailed);
		}
	}

	[RelayCommand]
	private void OpenProjectHome()
	{
		try
		{
			logger.LogInformation("About page external link. Target=project-home Url={Url}", StartRide.Core.SiteLinks.ProjectHome);
			if (!externalLinkService.TryOpen(StartRide.Core.SiteLinks.ProjectHome))
			{
				statusService.Report(Strings.Status_OpenProjectHomeFailed);
			}
		}
		catch (Exception)
		{
			statusService.Report(Strings.Status_OpenProjectHomeFailed);
		}
	}

	[RelayCommand]
	private void OpenFeedback()
	{
		try
		{
			logger.LogInformation("About page external link. Target=feedback Url={Url}", StartRide.Core.SiteLinks.FeedbackUrl("feature"));
			if (!externalLinkService.TryOpen(StartRide.Core.SiteLinks.FeedbackUrl("feature")))
			{
				statusService.Report(Strings.Status_OpenFeedbackPageFailed);
			}
		}
		catch (Exception)
		{
			statusService.Report(Strings.Status_OpenFeedbackPageFailed);
		}
	}

	[RelayCommand]
	private void OpenReferenceProject(InfoReferenceProjectItem? project)
	{
		if ((object)project == null)
		{
			return;
		}
		try
		{
			if (!externalLinkService.TryOpen(project.ProjectUrl))
			{
				ReportVisibleStatus(Strings.Status_OpenReferenceProjectFailed);
			}
		}
		catch (Exception)
		{
			ReportVisibleStatus(Strings.Status_OpenReferenceProjectFailed);
		}
	}

	[RelayCommand]
	private void OpenLegalDocument(LegalDocumentItem? document)
	{
		if (document is null)
		{
			logger.LogWarning("About page: legal document entry is missing.");
			ReportVisibleStatus(Strings.Status_OpenLegalDocumentFailed);
			return;
		}

		logger.LogInformation("About page: opening built-in legal reader. Id={Id}", document.Id);
		legalReader.Open(document);
	}

	[RelayCommand(CanExecute = "CanCheckUpdates", AllowConcurrentExecutions = true)]
	private async Task CheckUpdatesAsync()
	{
		await CheckUpdatesCoreAsync(UpdateCheckPresentation.Manual);
	}

	public Task CheckUpdatesOnStartupAsync()
	{

		ReportUpdateJournalResult();
		return CheckUpdatesCoreAsync(UpdateCheckPresentation.StartupSilent);
	}

	[RelayCommand]
	private void OpenUpdateChangelog()
	{
		if (!TryOpenUpdateUrl(updateDialogReleasePageUrl))
		{
			ReportVisibleStatus(Strings.Status_OpenUpdatePageFailed);
		}
	}

	[RelayCommand]
	private void CancelUpdateDialog()
	{
		IsUpdateAvailableDialogOpen = false;
	}

	[RelayCommand(CanExecute = "CanConfirmUpdate")]
	private async Task ConfirmUpdateAsync()
	{
		if (IsStartingUpdate)
		{
			return;
		}
		if ((object)availableUpdate == null)
		{
			ReportVisibleStatus(Strings.Status_OpenUpdatePageFailed);
			return;
		}
		if (!availableUpdate.CanAutoInstall && !StartRide.Services.StartRideSelfUpdateService.CanAutoInstall(availableUpdate))
		{
			ReportVisibleStatus(Strings.Status_UpdateAutoInstallPackageNotFound);
			return;
		}
		IsStartingUpdate = true;
		UpdateDialogTitle = Strings.Dialog_UpdateProgressTitle;
		UpdateProgressPercent = 0;
		UpdateProgressText = Strings.Status_DownloadingLauncherUpdate;
		ReportStatus(UpdateProgressText);
		try
		{
			LauncherSelfUpdateStartResult startResult;
			var progress = new Progress<StartRide.Services.StartRideUpdateProgress>(OnUpdateProgress);
			if (launcherSelfUpdateService is StartRide.Services.StartRideSelfUpdateService selfUpdate)
			{
				startResult = await selfUpdate.StartUpdateWithProgressAsync(availableUpdate, progress, CancellationToken.None);
			}
			else
			{
				startResult = await launcherSelfUpdateService.StartUpdateAsync(availableUpdate);
			}
			if (!startResult.Succeeded)
			{
				ReportVisibleStatus(Strings.Status_LauncherUpdateStartFailed);
				return;
			}

			UpdateProgressPercent = 100;
			UpdateProgressText = Strings.Status_UpdateReadyRestarting;
			ReportStatus(UpdateProgressText);
			await Task.Delay(1600);
			IsUpdateAvailableDialogOpen = false;
			applicationExitService.Shutdown();
		}
		catch (Exception)
		{
			ReportVisibleStatus(Strings.Status_LauncherUpdateStartFailed);
		}
		finally
		{
			IsStartingUpdate = false;
		}
	}

	private void OnUpdateProgress(StartRide.Services.StartRideUpdateProgress progress)
	{
		if (!IsStartingUpdate)
		{
			return;
		}

		switch (progress.Stage)
		{
		case StartRide.Services.StartRideUpdateStage.Preparing:
			UpdateProgressText = Strings.Status_DownloadingLauncherUpdate;
			break;
		case StartRide.Services.StartRideUpdateStage.Downloading:
			UpdateProgressText = string.Format(
				Strings.Status_UpdateDownloadingFormat,
				FormatBytes(progress.Received),
				progress.Total > 0 ? FormatBytes(progress.Total) : "?");
			if (progress.Total > 0)
			{
				UpdateProgressPercent = Math.Clamp(progress.Received * 100.0 / progress.Total, 0, 100);
			}
			break;
		case StartRide.Services.StartRideUpdateStage.Verifying:
			UpdateProgressText = Strings.Status_UpdateVerifying;
			break;
		case StartRide.Services.StartRideUpdateStage.Extracting:
			UpdateProgressText = Strings.Status_UpdateExtracting;
			break;
		case StartRide.Services.StartRideUpdateStage.Ready:
			UpdateProgressPercent = 100;
			UpdateProgressText = Strings.Status_UpdateReadyRestarting;
			break;
		}
		ReportStatus(UpdateProgressText);
	}

	private static string FormatBytes(long bytes)
	{
		if (bytes < 1024)
		{
			return bytes.ToString(CultureInfo.InvariantCulture) + " B";
		}
		double kilobytes = bytes / 1024.0;
		if (kilobytes < 1024)
		{
			return kilobytes.ToString("0.#", CultureInfo.InvariantCulture) + " KB";
		}
		return (kilobytes / 1024.0).ToString("0.##", CultureInfo.InvariantCulture) + " MB";
	}

	private void CloseUpdateResult()
	{
		IsUpdateResultDialogOpen = false;
	}

	private async Task RetryUpdateAsync()
	{
		IsUpdateResultDialogOpen = false;
		await CheckUpdatesCoreAsync(UpdateCheckPresentation.Manual);
	}

	private void ReportUpdateJournalResult()
	{
		try
		{
			StartRide.Services.StartRideUpdateStatus result =
				StartRide.Services.StartRideUpdateJournal.Consume(LauncherVersionText);
			if (!result.HasResult)
			{
				return;
			}

			if (!result.IsFailure)
			{
				UpdateResultIsFailure = false;
				UpdateResultTitle = Strings.Dialog_UpdateResultSuccessTitle;
				UpdateResultMessage = string.Format(Strings.Dialog_UpdateResultSuccessFormat, result.TargetVersion);
				IsUpdateResultDialogOpen = true;
				floatingMessageService.Show(UpdateResultMessage);
				logger.LogInformation(
					"Previous launcher update was applied. CurrentVersion={CurrentVersion} TargetVersion={TargetVersion}",
					result.CurrentVersion, result.TargetVersion);
				return;
			}

			UpdateResultIsFailure = true;
			UpdateResultTitle = Strings.Dialog_UpdateResultFailureTitle;
			string reason = string.IsNullOrWhiteSpace(result.Reason) ? Strings.Status_UpdateReasonNoRecord : result.Reason;
			UpdateResultMessage = string.Format(Strings.Dialog_UpdateResultFailureFormat, result.CurrentVersion, reason);
			IsUpdateResultDialogOpen = true;
			floatingMessageService.Show(UpdateResultTitle);
			logger.LogWarning(
				"Previous launcher update was NOT applied. CurrentVersion={CurrentVersion} TargetVersion={TargetVersion} Reason={Reason}",
				result.CurrentVersion, result.TargetVersion, reason);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to settle the previous launcher update result.");
		}
	}

	private bool CanCheckUpdates()
	{
		if (!IsStartingUpdate)
		{
			return !isUpdateCheckRunning;
		}
		return false;
	}

	private bool CanConfirmUpdate()
	{
		return !IsStartingUpdate;
	}

	internal InfoSettingsViewModel(SettingsPersistenceCoordinator persistence, IStatusService statusService, IFloatingMessageService floatingMessageService, IExternalLinkService externalLinkService, ILauncherUpdateService launcherUpdateService, ILauncherSelfUpdateService launcherSelfUpdateService, IApplicationExitService applicationExitService, IInfoReferenceProjectCatalog referenceProjectCatalog, LegalReaderViewModel legalReader, ILogger<InfoSettingsViewModel>? logger = null)
		: base(persistence)
	{
		this.statusService = statusService;
		this.floatingMessageService = floatingMessageService;
		this.externalLinkService = externalLinkService;
		this.legalReader = legalReader;
		this.launcherUpdateService = launcherUpdateService;
		this.launcherSelfUpdateService = launcherSelfUpdateService;
		this.applicationExitService = applicationExitService;
		this.logger = logger ?? NullLogger<InfoSettingsViewModel>.Instance;
		LauncherVersionText = ResolveLauncherVersion();
		ReferenceProjects = referenceProjectCatalog.GetProjects();
		LegalDocuments = LegalDocumentItemFactory.CreateAll();

		UpdateChannelOptions = new ObservableCollection<SettingsUpdateChannelOption>
		{
			new SettingsUpdateChannelOption(LauncherUpdateChannel.Release, Strings.Settings_UpdateChannelReleaseTitle)
		};
		selectedUpdateChannelOption = UpdateChannelOptions[0];
	}

	public void Load(LauncherSettings settings)
	{
		LoadState(delegate
		{
			SelectedUpdateChannelOption = UpdateChannelOptions[0];
		});
	}

	private void ShowUpdateAvailableDialog(LauncherUpdateInfo update)
	{
		availableUpdate = update;
		UpdateDialogTitle = Strings.Dialog_UpdateAvailableTitle;
		UpdateDialogVersionText = update.DisplayVersion;
		UpdateDialogMessage = string.Format(Strings.Dialog_UpdateAvailableVersionFormat, update.DisplayVersion);
		UpdateDialogChangelog = (update.Changelog ?? string.Empty).Trim();
		updateDialogReleasePageUrl = update.ReleasePageUrl;
		updateDialogDownloadUrl = (string.IsNullOrWhiteSpace(update.DownloadUrl) ? update.ReleasePageUrl : update.DownloadUrl);
		IsUpdateAvailableDialogOpen = true;
	}

	private void ReportStatus(string message)
	{
		statusService.Report(message);
	}

	private void ReportVisibleStatus(string message)
	{
		statusService.Report(message);
		floatingMessageService.Show(message);
	}

	private bool TryOpenUpdateUrl(string? url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return false;
		}
		try
		{
			return externalLinkService.TryOpen(url);
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static string ResolveLauncherVersion()
	{
		Assembly assembly = typeof(InfoSettingsViewModel).Assembly;
		string text = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text.Trim();
		}
		string text2 = assembly.GetName().Version?.ToString();
		if (!string.IsNullOrWhiteSpace(text2))
		{
			return text2;
		}
		return Strings.Settings_LauncherVersionUnknown;
	}

	private async Task CheckUpdatesCoreAsync(UpdateCheckPresentation presentation)
	{
		if (isUpdateCheckRunning)
		{
			return;
		}
		LauncherUpdateChannel channel = SelectedUpdateChannelOption?.Channel ?? LauncherUpdateChannel.Release;
		isUpdateCheckRunning = true;
		CheckUpdatesCommand.NotifyCanExecuteChanged();
		if (presentation == UpdateCheckPresentation.Manual)
		{
			IsCheckingUpdates = true;
			statusService.Report(Strings.Status_CheckingUpdates);
		}
		else
		{
			logger.LogInformation("Startup launcher update check started. CurrentVersion={CurrentVersion} Channel={Channel}", LauncherVersionText, channel);
		}
		try
		{
			LauncherUpdateCheckResult launcherUpdateCheckResult;
			try
			{
				launcherUpdateCheckResult = await launcherUpdateService.CheckForUpdatesAsync(LauncherVersionText, channel);
			}
			catch (Exception exception)
			{
				if (presentation == UpdateCheckPresentation.Manual)
				{
					ReportVisibleStatus(Strings.Status_CheckUpdatesFailed);
					return;
				}
				logger.LogWarning(exception, "Startup launcher update check threw an exception. CurrentVersion={CurrentVersion} Channel={Channel}", LauncherVersionText, channel);
				return;
			}
			if (launcherUpdateCheckResult.IsFailed)
			{
				if (presentation == UpdateCheckPresentation.Manual)
				{
					ReportVisibleStatus(Strings.Status_CheckUpdatesFailed);
					return;
				}
				logger.LogWarning("Startup launcher update check failed. CurrentVersion={CurrentVersion} Channel={Channel} Error={Error}", LauncherVersionText, channel, string.IsNullOrWhiteSpace(launcherUpdateCheckResult.ErrorMessage) ? "<none>" : launcherUpdateCheckResult.ErrorMessage);
				return;
			}
			if (!launcherUpdateCheckResult.IsUpdateAvailable || (object)launcherUpdateCheckResult.Update == null)
			{
				if (presentation == UpdateCheckPresentation.Manual)
				{
					ReportVisibleStatus(Strings.Status_LauncherAlreadyLatest);
					return;
				}
				logger.LogInformation("Startup launcher update check completed. No update available. CurrentVersion={CurrentVersion} Channel={Channel}", LauncherVersionText, channel);
				return;
			}
			if (presentation == UpdateCheckPresentation.StartupSilent)
			{
				logger.LogInformation("Startup launcher update check found an update. CurrentVersion={CurrentVersion} Channel={Channel} UpdateVersion={UpdateVersion}", LauncherVersionText, channel, launcherUpdateCheckResult.Update.DisplayVersion);
			}
			ShowUpdateAvailableDialog(launcherUpdateCheckResult.Update);
		}
		finally
		{
			if (presentation == UpdateCheckPresentation.Manual)
			{
				IsCheckingUpdates = false;
			}
			isUpdateCheckRunning = false;
			CheckUpdatesCommand.NotifyCanExecuteChanged();
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedUpdateChannelOptionChanged(SettingsUpdateChannelOption? oldValue, SettingsUpdateChannelOption? newValue)
	{
		if (newValue == null)
		{
			LoadState(delegate
			{
				SelectedUpdateChannelOption = oldValue ?? UpdateChannelOptions[0];
			});
		}
		else
		{
			Persist(delegate(LauncherSettings settings)
			{
				settings.UpdateChannel = newValue.Channel;
			});
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsCheckingUpdatesChanged(bool value)
	{
		OnPropertyChanged("CheckUpdatesButtonText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsStartingUpdateChanged(bool value)
	{
		OnPropertyChanged("ConfirmUpdateButtonText");
		CheckUpdatesCommand.NotifyCanExecuteChanged();
		ConfirmUpdateCommand.NotifyCanExecuteChanged();
	}
}
