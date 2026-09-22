using System;
using System.CodeDom.Compiler;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Account;

public sealed class AccountPurchaseViewModel : ObservableObject
{
	private readonly IExternalLinkService externalLinkService;

	private readonly IStatusService statusService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly ILogger<AccountPurchaseViewModel> logger;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openMinecraftPurchasePageCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenMinecraftPurchasePageCommand => openMinecraftPurchasePageCommand ?? (openMinecraftPurchasePageCommand = new RelayCommand(OpenMinecraftPurchasePage));

	public AccountPurchaseViewModel(IExternalLinkService externalLinkService, IStatusService statusService, IFloatingMessageService floatingMessageService, ILogger<AccountPurchaseViewModel>? logger = null)
	{
		this.externalLinkService = externalLinkService;
		this.statusService = statusService;
		this.floatingMessageService = floatingMessageService;
		this.logger = logger ?? NullLogger<AccountPurchaseViewModel>.Instance;
	}

	[RelayCommand]
	private void OpenMinecraftPurchasePage()
	{
		try
		{
			logger.LogDebug("Opening the BeamNG.drive official website.");
			if (externalLinkService.TryOpen("https://www.beamng.com/"))
			{
				logger.LogInformation("BeamNG.drive official website opened.");
				return;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to open the BeamNG.drive official website.");
			ReportFailure();
			return;
		}
		logger.LogWarning("Failed to open the BeamNG.drive official website.");
		ReportFailure();
	}

	private void ReportFailure()
	{
		statusService.Report(Strings.Status_OpenMinecraftPurchasePageFailed);
		floatingMessageService.Show(Strings.Status_OpenMinecraftPurchasePageFailed);
	}
}
