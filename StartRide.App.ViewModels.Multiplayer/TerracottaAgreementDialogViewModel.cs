using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.Multiplayer;

public sealed class TerracottaAgreementDialogViewModel : ObservableObject
{
	/// <summary>
	/// 「联机功能使用须知」里那个链接的目标地址。
	///
	/// 原先硬编码成 https://github.com/burningtnt/Terracotta —— 那是上游的 Minecraft
	/// 局域网穿透方案；联机层换成自建中继后文案已改成「StartRide 中继项目」，链接却没跟着改，
	/// 点开就跳到别人的 Minecraft 项目。统一收到 SiteLinks（唯一事实来源）。
	/// </summary>
	internal static string TerracottaProjectUrl => StartRide.Core.SiteLinks.RelayProjectUrl;

	private readonly ITerracottaProvisioningService provisioningService;

	private readonly IExternalLinkService externalLinkService;

	private readonly IStatusService statusService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly ILogger<TerracottaAgreementDialogViewModel> logger;

	private TaskCompletionSource<bool>? pendingDecision;

	[ObservableProperty]
	private bool isOpen;

	[ObservableProperty]
	private bool isDownloading;

	[ObservableProperty]
	private double downloadProgressPercent;

	[ObservableProperty]
	private string downloadStatus = Strings.Dialog_TerracottaDownloadPreparing;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? disagreeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? agreeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openProjectCommand;

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

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsDownloading
	{
		get
		{
			return isDownloading;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isDownloading, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDownloading);
				isDownloading = value;
				OnIsDownloadingChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDownloading);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double DownloadProgressPercent
	{
		get
		{
			return downloadProgressPercent;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(downloadProgressPercent, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DownloadProgressPercent);
				downloadProgressPercent = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DownloadProgressPercent);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string DownloadStatus
	{
		get
		{
			return downloadStatus;
		}
		[MemberNotNull("downloadStatus")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(downloadStatus, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DownloadStatus);
				downloadStatus = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DownloadStatus);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand DisagreeCommand => disagreeCommand ?? (disagreeCommand = new RelayCommand(Disagree, CanRespond));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand AgreeCommand => agreeCommand ?? (agreeCommand = new AsyncRelayCommand(AgreeAsync, CanRespond));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenProjectCommand => openProjectCommand ?? (openProjectCommand = new RelayCommand(OpenProject));

	public TerracottaAgreementDialogViewModel(ITerracottaProvisioningService provisioningService, IExternalLinkService externalLinkService, IStatusService statusService, IFloatingMessageService floatingMessageService, ILogger<TerracottaAgreementDialogViewModel>? logger = null)
	{
		this.provisioningService = provisioningService;
		this.externalLinkService = externalLinkService;
		this.statusService = statusService;
		this.floatingMessageService = floatingMessageService;
		this.logger = logger ?? NullLogger<TerracottaAgreementDialogViewModel>.Instance;
	}

	public Task<bool> EnsureReadyAsync()
	{
		if ((object)provisioningService.TryGetAvailable() != null)
		{
			return Task.FromResult(result: true);
		}
		if (pendingDecision != null)
		{
			return pendingDecision.Task;
		}
		pendingDecision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		DownloadProgressPercent = 0.0;
		DownloadStatus = Strings.Dialog_TerracottaDownloadPreparing;
		IsOpen = true;
		logger.LogInformation("Terracotta usage notice requested because the local module is unavailable.");
		return pendingDecision.Task;
	}

	[RelayCommand(CanExecute = "CanRespond")]
	private void Disagree()
	{
		IsOpen = false;
		CompleteDecision(result: false);
		logger.LogInformation("Terracotta usage notice declined; multiplayer navigation canceled.");
	}

	[RelayCommand(CanExecute = "CanRespond")]
	private async Task AgreeAsync()
	{
		IsDownloading = true;
		DownloadProgressPercent = 0.0;
		DownloadStatus = Strings.Dialog_TerracottaDownloading;
		try
		{
			Progress<LauncherProgress> progress = new Progress<LauncherProgress>(delegate(LauncherProgress value)
			{
				double? percent = value.Percent;
				if (percent.HasValue)
				{
					double valueOrDefault = percent.GetValueOrDefault();
					DownloadProgressPercent = Math.Clamp(valueOrDefault, 0.0, 100.0);
				}
				DownloadStatus = ((value.Stage == "terracotta-extract") ? Strings.Dialog_TerracottaExtracting : Strings.Dialog_TerracottaDownloading);
			});
			await provisioningService.EnsureAvailableAsync(progress);
			DownloadProgressPercent = 100.0;
			DownloadStatus = Strings.Dialog_TerracottaDownloadComplete;
			IsOpen = false;
			CompleteDecision(result: true);
			statusService.Report(Strings.Status_TerracottaReady);
			logger.LogInformation("Terracotta usage notice accepted and module provisioning completed.");
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to provision the Terracotta module after notice acceptance.");
			DownloadProgressPercent = 0.0;
			DownloadStatus = Strings.Dialog_TerracottaDownloadFailed;
			ReportFailure(Strings.Status_TerracottaDownloadFailed);
		}
		finally
		{
			IsDownloading = false;
		}
	}

	[RelayCommand]
	private void OpenProject()
	{
		try
		{
			if (externalLinkService.TryOpen(TerracottaProjectUrl))
			{
				return;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to open the relay project page.");
			ReportFailure(Strings.Status_OpenTerracottaProjectFailed);
			return;
		}
		logger.LogWarning("Failed to open the relay project page.");
		ReportFailure(Strings.Status_OpenTerracottaProjectFailed);
	}

	private bool CanRespond()
	{
		return !IsDownloading;
	}

	private void CompleteDecision(bool result)
	{
		TaskCompletionSource<bool>? taskCompletionSource = pendingDecision;
		pendingDecision = null;
		taskCompletionSource?.TrySetResult(result);
	}

	private void ReportFailure(string message)
	{
		statusService.Report(message);
		floatingMessageService.Show(message);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsDownloadingChanged(bool value)
	{
		AgreeCommand.NotifyCanExecuteChanged();
		DisagreeCommand.NotifyCanExecuteChanged();
	}
}
