using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.Settings;

public sealed class FeedbackSettingsViewModel : SettingsSectionViewModelBase
{
	private readonly IStatusService statusService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly IExternalLinkService externalLinkService;

	private readonly ILogger<FeedbackSettingsViewModel> logger;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<FeedbackChannelItem?>? openChannelCommand;

	public ObservableCollection<FeedbackChannelItem> Channels { get; }

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<FeedbackChannelItem?> OpenChannelCommand => openChannelCommand ?? (openChannelCommand = new RelayCommand<FeedbackChannelItem?>(OpenChannel));

	public FeedbackSettingsViewModel(SettingsPersistenceCoordinator persistence, IStatusService statusService, IFloatingMessageService floatingMessageService, IExternalLinkService externalLinkService, ILogger<FeedbackSettingsViewModel>? logger = null)
		: base(persistence)
	{
		this.statusService = statusService;
		this.floatingMessageService = floatingMessageService;
		this.externalLinkService = externalLinkService;
		this.logger = logger ?? NullLogger<FeedbackSettingsViewModel>.Instance;
		Channels = new ObservableCollection<FeedbackChannelItem>
		{
			new FeedbackChannelItem("setting_page/new_feature", Strings.Dialog_FeedbackFeatureSuggestionsButton, Strings.Settings_FeedbackFeatureDescription, "feature"),
			new FeedbackChannelItem("setting_page/bug", Strings.Dialog_FeedbackBugReportsButton, Strings.Settings_FeedbackBugDescription, "bug")
		};
	}

	[RelayCommand]
	private void OpenChannel(FeedbackChannelItem? channel)
	{
		if ((object)channel == null)
		{
			return;
		}
		try
		{
			logger.LogInformation("Opening feedback channel. Target={Target}", channel.Target);
			if (externalLinkService.TryOpen(StartRide.Core.SiteLinks.FeedbackUrl(channel.Target)))
			{
				logger.LogDebug("Opened feedback channel. Target={Target}", channel.Target);
				return;
			}
			logger.LogWarning("Unable to open feedback link. Target={Target}", channel.Target);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Unable to open feedback link. Target={Target}", channel.Target);
		}
		statusService.Report(Strings.Status_OpenFeedbackPageFailed);
		floatingMessageService.Show(Strings.Status_OpenFeedbackPageFailed);
	}
}
