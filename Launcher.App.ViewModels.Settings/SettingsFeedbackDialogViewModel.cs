using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Settings;

public sealed class SettingsFeedbackDialogViewModel : ObservableObject
{
	private readonly IStatusService statusService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly IExternalLinkService externalLinkService;

	private readonly ILogger<SettingsFeedbackDialogViewModel> logger;

	[ObservableProperty]
	private bool isOpen;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openFeatureSuggestionsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openBugReportsCommand;

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
	public IRelayCommand CancelCommand => cancelCommand ?? (cancelCommand = new RelayCommand(Cancel));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenFeatureSuggestionsCommand => openFeatureSuggestionsCommand ?? (openFeatureSuggestionsCommand = new RelayCommand(OpenFeatureSuggestions));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenBugReportsCommand => openBugReportsCommand ?? (openBugReportsCommand = new RelayCommand(OpenBugReports));

	public SettingsFeedbackDialogViewModel(IStatusService statusService, IFloatingMessageService floatingMessageService, IExternalLinkService externalLinkService, ILogger<SettingsFeedbackDialogViewModel>? logger = null)
	{
		this.statusService = statusService;
		this.floatingMessageService = floatingMessageService;
		this.externalLinkService = externalLinkService;
		this.logger = logger ?? NullLogger<SettingsFeedbackDialogViewModel>.Instance;
	}

	public void Open()
	{
		IsOpen = true;
	}

	[RelayCommand]
	private void Cancel()
	{
		IsOpen = false;
	}

	[RelayCommand]
	private void OpenFeatureSuggestions()
	{
		OpenExternalLink(StartRide.Core.SiteLinks.GitHubNewIssue + "?labels=enhancement&template=feature_request.md", "feature-suggestions");
	}

	[RelayCommand]
	private void OpenBugReports()
	{
		OpenExternalLink(StartRide.Core.SiteLinks.GitHubIssues, "bug-reports");
	}

	private void OpenExternalLink(string url, string target)
	{
		try
		{
			logger.LogDebug("Opening feedback link. Target={Target}", target);
			if (externalLinkService.TryOpen(url))
			{
				logger.LogInformation("Feedback link opened.");
				logger.LogDebug("Opened feedback link target. Target={Target}", target);
				return;
			}
			logger.LogWarning("Unable to open feedback link. Target={Target}", target);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Unable to open feedback link. Target={Target}", target);
		}
		statusService.Report(Strings.Status_OpenFeedbackPageFailed);
		floatingMessageService.Show(Strings.Status_OpenFeedbackPageFailed);
	}
}
