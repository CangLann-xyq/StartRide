using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Shell;

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
	private RelayCommand? openAgreementCommand;

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

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenAgreementCommand => openAgreementCommand ?? (openAgreementCommand = new RelayCommand(OpenAgreement));

	public UserAgreementDialogViewModel(ISettingsService settingsService, IExternalLinkService externalLinkService, IApplicationExitService applicationExitService, IStatusService statusService, IFloatingMessageService floatingMessageService, ILogger<UserAgreementDialogViewModel>? logger = null)
	{
		this.settingsService = settingsService;
		this.externalLinkService = externalLinkService;
		this.applicationExitService = applicationExitService;
		this.statusService = statusService;
		this.floatingMessageService = floatingMessageService;
		this.logger = logger ?? NullLogger<UserAgreementDialogViewModel>.Instance;
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
	private void OpenAgreement()
	{
		try
		{
			if (externalLinkService.TryOpen("https://docs.qq.com/markdown/DSmhwTHJ3WXVobHVY"))
			{
				return;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to open the user agreement link.");
			ReportFailure(Strings.Status_OpenUserAgreementFailed);
			return;
		}
		logger.LogWarning("Failed to open the user agreement link.");
		ReportFailure(Strings.Status_OpenUserAgreementFailed);
	}

	private void ReportFailure(string message)
	{
		statusService.Report(message);
		floatingMessageService.Show(message);
	}
}
