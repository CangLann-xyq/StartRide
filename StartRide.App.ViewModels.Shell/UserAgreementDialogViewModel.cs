using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

namespace StartRide.App.ViewModels.Shell;

public sealed class UserAgreementDialogViewModel : ObservableObject
{
	private readonly ISettingsService settingsService;

	private readonly IExternalLinkService externalLinkService;

	private readonly IApplicationExitService applicationExitService;

	private readonly IStatusService statusService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly ILogger<UserAgreementDialogViewModel> logger;

	private readonly TaskCompletionSource<bool> decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

	private LauncherSettings? settings;

	[ObservableProperty]
	private bool isOpen;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? agreeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? disagreeAndExitCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<LegalDocumentItem?>? openLegalDocumentCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openAllDocumentsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsOpen
	{
		get
		{
			return isOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsOpen);
				isOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand AgreeCommand => agreeCommand ?? (agreeCommand = new AsyncRelayCommand(AgreeAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand DisagreeAndExitCommand => disagreeAndExitCommand ?? (disagreeAndExitCommand = new RelayCommand(DisagreeAndExit));

	/// <summary>
	/// 首次运行弹窗里逐条列出的文件（用户协议 / 隐私政策 / 未成年人保护规则 /
	/// 免责声明 / 联机规范 / 第三方许可声明）。
	///
	/// 以前这里只有一个「用户协议」超链接，而且指向 GitHub —— 国内直连打不开，
	/// 用户点下去是白屏，等于"同意了一份自己看不到的文件"。现在六份一起列出来，
	/// 地址统一走腾讯文档（见 <see cref="StartRide.Core.LegalDocuments"/>）。
	/// </summary>
	public IReadOnlyList<LegalDocumentItem> Documents { get; }

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<LegalDocumentItem?> OpenLegalDocumentCommand => openLegalDocumentCommand ?? (openLegalDocumentCommand = new RelayCommand<LegalDocumentItem?>(OpenLegalDocument));

	/// <summary>
	/// 「查看全部条款与说明」——打开腾讯文档上的总目录页。
	///
	/// 弹窗里只列了需要用户明确同意的六份；版权声明与开源协议不在同意之列，
	/// 但也得让用户够得着，否则那两份等于没有入口。
	/// 总目录地址留空时（开发期/未回填）退化成仓库里的 md，不会出现点了没反应的死链。
	/// </summary>
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenAllDocumentsCommand => openAllDocumentsCommand ?? (openAllDocumentsCommand = new RelayCommand(OpenAllDocuments));

	public UserAgreementDialogViewModel(ISettingsService settingsService, IExternalLinkService externalLinkService, IApplicationExitService applicationExitService, IStatusService statusService, IFloatingMessageService floatingMessageService, ILogger<UserAgreementDialogViewModel>? logger = null)
	{
		this.settingsService = settingsService;
		this.externalLinkService = externalLinkService;
		this.applicationExitService = applicationExitService;
		this.statusService = statusService;
		this.floatingMessageService = floatingMessageService;
		this.logger = logger ?? NullLogger<UserAgreementDialogViewModel>.Instance;
		Documents = LegalDocumentItemFactory.CreateFirstRunAgreements();
	}

	public void Prime(LauncherSettings launcherSettings)
	{
		ArgumentNullException.ThrowIfNull(launcherSettings, "launcherSettings");
		settings = launcherSettings;
		IsOpen = !launcherSettings.HasAcceptedUserAgreement;
		if (!IsOpen)
		{
			decision.TrySetResult(result: true);
		}
	}

	public Task<bool> WaitForDecisionAsync()
	{
		return decision.Task;
	}

	[RelayCommand]
	private async Task AgreeAsync()
	{
		if (settings == null || settings.HasAcceptedUserAgreement)
		{
			return;
		}
		settings.HasAcceptedUserAgreement = true;
		try
		{
			await settingsService.UpdateAsync(delegate(LauncherSettings latest)
			{
				latest.HasAcceptedUserAgreement = true;
			});
			IsOpen = false;
			decision.TrySetResult(result: true);
			logger.LogInformation("User agreement accepted and persisted.");
		}
		catch (Exception exception)
		{
			settings.HasAcceptedUserAgreement = false;
			logger.LogWarning(exception, "Failed to persist user agreement acceptance.");
			ReportFailure(Strings.Status_UserAgreementSaveFailed);
		}
	}

	[RelayCommand]
	private void DisagreeAndExit()
	{
		logger.LogInformation("User agreement declined; launcher exit requested.");
		decision.TrySetResult(result: false);
		applicationExitService.Shutdown();
	}

	[RelayCommand]
	private void OpenLegalDocument(LegalDocumentItem? document)
	{
		if (document is null || string.IsNullOrWhiteSpace(document.Url))
		{
			logger.LogWarning("User agreement dialog: legal document link is empty. Id={Id}",
				document?.Id ?? "<null>");
			ReportFailure(Strings.Status_OpenUserAgreementFailed);
			return;
		}

		try
		{
			// 记一行日志：腾讯文档地址变更/回填出错时，"点了没反应"这类反馈
			// 有这行就能立刻分辨是地址问题还是浏览器问题。
			logger.LogInformation("User agreement dialog external link. Id={Id} Url={Url}", document.Id, document.Url);
			if (externalLinkService.TryOpen(document.Url))
			{
				return;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to open a legal document link from the user agreement dialog.");
			ReportFailure(Strings.Status_OpenUserAgreementFailed);
			return;
		}
		logger.LogWarning("Failed to open a legal document link from the user agreement dialog. Id={Id}", document.Id);
		ReportFailure(Strings.Status_OpenUserAgreementFailed);
	}

	/// <summary>
	/// 「查看全部条款与说明」——打开腾讯文档上的总目录页。
	///
	/// 弹窗里只列了需要用户明确同意的六份；版权声明与开源协议不在同意之列，
	/// 但也得让用户够得着，否则那两份等于没有入口。
	/// 总目录地址留空时（开发期/未回填）退化成仓库里的 md，不会出现点了没反应的死链。
	/// </summary>
	[RelayCommand]
	private void OpenAllDocuments()
	{
		string url = StartRide.Core.SiteLinks.FirstNonEmpty(
			StartRide.Core.SiteLinks.Legal.IndexDoc,
			StartRide.Core.SiteLinks.UserAgreementFallbackUrl);

		try
		{
			logger.LogInformation("User agreement dialog external link. Id=index Url={Url}", url);
			if (externalLinkService.TryOpen(url))
			{
				return;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to open the legal document index from the user agreement dialog.");
			ReportFailure(Strings.Status_OpenUserAgreementFailed);
			return;
		}
		logger.LogWarning("Failed to open the legal document index from the user agreement dialog.");
		ReportFailure(Strings.Status_OpenUserAgreementFailed);
	}

	private void ReportFailure(string message)
	{
		statusService.Report(message);
		floatingMessageService.Show(message);
	}
}
