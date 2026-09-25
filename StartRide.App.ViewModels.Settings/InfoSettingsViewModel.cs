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

	/// <summary>更新弹窗里的更新说明文本（来自清单的 changelog）。</summary>
	private string updateDialogChangelog = string.Empty;

	/// <summary>更新过程中的阶段文案（下载 3.2 MB / 5.1 MB 之类）。</summary>
	private string updateProgressText = string.Empty;

	/// <summary>更新进度百分比（0-100）。</summary>
	private double updateProgressPercent;

	/// <summary>上一次更新结果的弹窗（下次启动时结算，见 StartRideUpdateJournal）。</summary>
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
	private RelayCommand? openCopyrightNoticeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openOpenSourceLicenseCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openUserAgreementCommand;

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

	/// <summary>更新弹窗标题：平时是「有可用更新」，点下去后换成「正在安装更新」。</summary>
	public string UpdateDialogTitle
	{
		get => updateDialogTitle;
		set => SetProperty(ref updateDialogTitle, value);
	}

	/// <summary>更新阶段文案（更新期间对话框里实时显示）。</summary>
	public string UpdateProgressText
	{
		get => updateProgressText;
		set
		{
			if (SetProperty(ref updateProgressText, value))
			{
				// 更新按钮上的字也跟着阶段走，避免整块界面在下载期间毫无变化
				OnPropertyChanged("ConfirmUpdateButtonText");
			}
		}
	}

	/// <summary>更新进度百分比（下载阶段按已收字节算；总大小未知时保持 0）。</summary>
	public double UpdateProgressPercent
	{
		get => updateProgressPercent;
		set => SetProperty(ref updateProgressPercent, value);
	}

	/// <summary>是否弹出「上次更新结果」提示。</summary>
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

	/// <summary>上次更新是否失败（失败时才给「重新更新」按钮）。</summary>
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

	/// <summary>
	/// 更新弹窗里的「本次更新内容」。
	///
	/// 之前这个弹窗只有一个「打开更新日志」按钮，点了跳 GitHub Releases ——
	/// 国内基本打不开，等于用户永远看不到改了什么。清单里本来就有 changelog 字段，
	/// 直接显示出来，按钮只作为「想看详情」的补充入口。
	/// </summary>
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

	/// <summary>清单没给更新说明时，那块区域整体收起。</summary>
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
	public IRelayCommand OpenCopyrightNoticeCommand => openCopyrightNoticeCommand ?? (openCopyrightNoticeCommand = new RelayCommand(OpenCopyrightNotice));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenOpenSourceLicenseCommand => openOpenSourceLicenseCommand ?? (openOpenSourceLicenseCommand = new RelayCommand(OpenOpenSourceLicense));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenUserAgreementCommand => openUserAgreementCommand ?? (openUserAgreementCommand = new RelayCommand(OpenUserAgreement));

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
			// 关于页四个按钮的目标地址都记一行日志 —— 「点了按钮跳到别人仓库」这种反馈，
			// 有这行日志就能一秒分辨是旧包（日志里的 URL 是上游地址）还是真的改错了。
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

	/// <summary>打开项目主页（startride.top）。地址来自 SiteLinks，换域名只改那一处。</summary>
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

	/// <summary>
	/// 打开反馈入口——自有反馈页（startride.top/feedback.html?type=feature&amp;v=版本号）。
	///
	/// ⚠️ 这里原先指 GitHub 的新建 issue 页（带 feature_request.md 模板）。改掉的原因有两个：
	///   1) GitHub 在国内直连打不开，用户点开等于没有反馈入口；
	///   2) 关于页上「查看源码仓库」本来就已经是 GitHub，反馈入口再指 GitHub 会让人以为
	///      反馈是提到别人的仓库里去 —— 关于页的四个按钮里，只有「查看源码仓库」该出去。
	/// 反馈页与设置里的「建议与反馈」对话框现在同源（都走 SiteLinks.FeedbackUrl）。
	/// </summary>
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
	private void OpenCopyrightNotice()
	{
		OpenLegalDocument(StartRide.Core.SiteLinks.GitHubRepo);
	}

	[RelayCommand]
	private void OpenOpenSourceLicense()
	{
		OpenLegalDocument(StartRide.Core.SiteLinks.LicenseUrl);
	}

	[RelayCommand]
	private void OpenUserAgreement()
	{
		OpenLegalDocument(StartRide.Core.SiteLinks.UserAgreementUrl);
	}

	private void OpenLegalDocument(string url)
	{
		try
		{
			if (externalLinkService.TryOpen(url))
			{
				return;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to open a legal document link.");
			ReportVisibleStatus(Strings.Status_OpenLegalDocumentFailed);
			return;
		}
		logger.LogWarning("Failed to open a legal document link.");
		ReportVisibleStatus(Strings.Status_OpenLegalDocumentFailed);
	}

	[RelayCommand(CanExecute = "CanCheckUpdates", AllowConcurrentExecutions = true)]
	private async Task CheckUpdatesAsync()
	{
		await CheckUpdatesCoreAsync(UpdateCheckPresentation.Manual);
	}

	public Task CheckUpdatesOnStartupAsync()
	{
		// 先结算上一次自更新的结果：成功要告诉用户「已更新到 vX」，
		// 失败更要说明白（否则用户只会看到"还是旧版本"，却不知道更新没装上）。
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
			// 走到这里 = 包已解包好、替换脚本正在后台等本进程退出。
			// 对话框保持打开、明说「已就绪、马上自动重启」，再留一小会儿让用户看清：
			// 以前是直接消失 + 退进程，用户会以为更新失败了（实测被投诉过）。
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

	/// <summary>把自更新进度翻成界面文案（Progress&lt;T&gt; 保证回到 UI 线程）。</summary>
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

	/// <summary>「重新更新」：关掉结果弹窗，立刻重查一次（有更新就直接弹更新框）。</summary>
	private async Task RetryUpdateAsync()
	{
		IsUpdateResultDialogOpen = false;
		await CheckUpdatesCoreAsync(UpdateCheckPresentation.Manual);
	}

	/// <summary>
	/// 结算上一次自更新的结果。
	/// 单靠版本号无法判断「刚才那次更新到底成没成」，所以安装前会留下一张交接条，
	/// 由这里对照实际版本给用户一个明确答复（成功 / 失败 + 卡在哪）。
	/// </summary>
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
			// 结算失败绝不能拖累启动流程
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

	internal InfoSettingsViewModel(SettingsPersistenceCoordinator persistence, IStatusService statusService, IFloatingMessageService floatingMessageService, IExternalLinkService externalLinkService, ILauncherUpdateService launcherUpdateService, ILauncherSelfUpdateService launcherSelfUpdateService, IApplicationExitService applicationExitService, IInfoReferenceProjectCatalog referenceProjectCatalog, ILogger<InfoSettingsViewModel>? logger = null)
		: base(persistence)
	{
		this.statusService = statusService;
		this.floatingMessageService = floatingMessageService;
		this.externalLinkService = externalLinkService;
		this.launcherUpdateService = launcherUpdateService;
		this.launcherSelfUpdateService = launcherSelfUpdateService;
		this.applicationExitService = applicationExitService;
		this.logger = logger ?? NullLogger<InfoSettingsViewModel>.Instance;
		LauncherVersionText = ResolveLauncherVersion();
		ReferenceProjects = referenceProjectCatalog.GetProjects();
		// StartRide 不是 Minecraft，不存在快照版/测试版之类的「特殊版本」，
		// 更新通道固定只留官方正式版，避免用户切到不存在的通道。
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
			// 历史配置里可能残留 Beta，统一回正到正式版。
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
