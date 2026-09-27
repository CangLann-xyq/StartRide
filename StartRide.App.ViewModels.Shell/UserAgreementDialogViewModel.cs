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

	private readonly LegalReaderViewModel legalReader;

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
	/// 点任意一条都会在软件内置的阅读器里打开（见 <see cref="LegalReaderViewModel"/>）。
	/// </summary>
	public IReadOnlyList<LegalDocumentItem> Documents { get; }

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<LegalDocumentItem?> OpenLegalDocumentCommand => openLegalDocumentCommand ?? (openLegalDocumentCommand = new RelayCommand<LegalDocumentItem?>(OpenLegalDocument));

	/// <summary>「查看全部条款与说明」——直接把内置阅读器打开（目录里八份齐全）。</summary>
	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenAllDocumentsCommand => openAllDocumentsCommand ?? (openAllDocumentsCommand = new RelayCommand(OpenAllDocuments));

	public UserAgreementDialogViewModel(ISettingsService settingsService, LegalReaderViewModel legalReader, IApplicationExitService applicationExitService, IStatusService statusService, IFloatingMessageService floatingMessageService, ILogger<UserAgreementDialogViewModel>? logger = null)
	{
		this.settingsService = settingsService;
		this.legalReader = legalReader;
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
		if (document is null)
		{
			logger.LogWarning("User agreement dialog: legal document entry is missing.");
			ReportFailure(Strings.Status_OpenUserAgreementFailed);
			return;
		}

		// 正文随包内嵌，这里只是把阅读器打开并定位到这一份：不跳浏览器，
		// 离线可读，国内也一定打得开。「点了没反应」这类反馈看这行日志即可。
		logger.LogInformation("User agreement dialog: opening built-in legal reader. Id={Id}", document.Id);
		legalReader.Open(document);
	}

	/// <summary>
	/// 「查看全部条款与说明」——直接把内置阅读器打开。
	///
	/// 弹窗里只列了需要用户明确同意的六份；版权声明与开源协议不在同意之列，
	/// 但阅读器左侧的目录把八份全列了出来，所以点这一下就够得着。
	/// </summary>
	[RelayCommand]
	private void OpenAllDocuments()
	{
		logger.LogInformation("User agreement dialog: opening built-in legal reader index.");
		legalReader.Open(null);
	}

	private void ReportFailure(string message)
	{
		statusService.Report(message);
		floatingMessageService.Show(message);
	}
}
