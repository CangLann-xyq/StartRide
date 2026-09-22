using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.Utilities;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Download;

public sealed class DownloadInstallViewModel : ObservableObject
{
	private readonly IGameInstanceService instanceService;

	private readonly DownloadTasksPageViewModel downloadTasksPage;

	private readonly DownloadInstanceNameTracker instanceNameTracker;

	private readonly IUiDispatcher uiDispatcher;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly ILogger<DownloadInstallViewModel> logger;

	private int activeInstallCount;

	private long latestInstallSequence;

	[ObservableProperty]
	private bool isInstalling;

	[ObservableProperty]
	private string installStatusMessage = string.Empty;

	[ObservableProperty]
	private string installError = string.Empty;

	[ObservableProperty]
	private double installProgressPercent;

	public bool HasInstallStatus => !string.IsNullOrWhiteSpace(InstallStatusMessage);

	public bool HasInstallError => !string.IsNullOrWhiteSpace(InstallError);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsInstalling
	{
		get
		{
			return isInstalling;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isInstalling, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsInstalling);
				isInstalling = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsInstalling);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string InstallStatusMessage
	{
		get
		{
			return installStatusMessage;
		}
		[MemberNotNull("installStatusMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(installStatusMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InstallStatusMessage);
				installStatusMessage = value;
				OnInstallStatusMessageChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InstallStatusMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string InstallError
	{
		get
		{
			return installError;
		}
		[MemberNotNull("installError")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(installError, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InstallError);
				installError = value;
				OnInstallErrorChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InstallError);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double InstallProgressPercent
	{
		get
		{
			return installProgressPercent;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(installProgressPercent, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InstallProgressPercent);
				installProgressPercent = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InstallProgressPercent);
			}
		}
	}

	public event EventHandler<GameInstance>? InstanceInstalled;

	public event Action? NameAvailabilityChanged;

	internal DownloadInstallViewModel(IGameInstanceService instanceService, DownloadTasksPageViewModel downloadTasksPage, DownloadInstanceNameTracker instanceNameTracker, IUiDispatcher uiDispatcher, IFloatingMessageService floatingMessageService, ILogger<DownloadInstallViewModel>? logger = null)
	{
		this.instanceService = instanceService;
		this.downloadTasksPage = downloadTasksPage;
		this.instanceNameTracker = instanceNameTracker;
		this.uiDispatcher = uiDispatcher;
		this.floatingMessageService = floatingMessageService;
		this.logger = logger ?? NullLogger<DownloadInstallViewModel>.Instance;
	}

	public async Task InstallAsync(DownloadInstallRequest request)
	{
		long startedAt = Stopwatch.GetTimestamp();
		long installSequence = Interlocked.Increment(ref latestInstallSequence);
		DownloadTaskItem installTask = downloadTasksPage.BeginTask(request.LoaderDisplayName + " " + request.MinecraftVersion, request.InstanceName);
		logger.LogInformation("Starting instance installation. MinecraftVersion={MinecraftVersion} Loader={Loader} InstanceName={InstanceName}", request.MinecraftVersion, request.Loader, request.InstanceName);
		floatingMessageService.Show(Strings.Status_InstallStartingDownload);
		IsInstalling = Interlocked.Increment(ref activeInstallCount) > 0;
		InstallError = string.Empty;
		InstallProgressPercent = 0.0;
		InstallStatusMessage = Strings.Status_InstallPreparing;
		installTask.Report(new LauncherProgress("Install.Preparing", string.Empty, 0.0));
		instanceNameTracker.AddPending(request.InstanceName);
		NameAvailabilityChanged?.Invoke();
		try
		{
			using DownloadInstallProgress installProgress = new DownloadInstallProgress(installTask, installSequence, ReportInstallProgress, uiDispatcher);
			GameInstance gameInstance = await instanceService.CreateInstanceAsync(request.MinecraftVersion, request.Loader, request.LoaderVersion, request.InstanceName, installTask.CreateProgress(installProgress.Report), installTask.CancellationToken, request.DownloadSourcePreference, request.DownloadSpeedLimitMbPerSecond, request.FabricApiVersionId != null, request.FabricApiVersionId, request.QuiltStandardLibraryVersionId);
			gameInstance.VersionType = request.MinecraftVersionType;
			instanceNameTracker.RemovePending(request.InstanceName);
			instanceNameTracker.AddExisting(gameInstance.Name);
			instanceNameTracker.AddExisting(gameInstance.VersionName);
			string message = string.Format(Strings.Status_InstanceInstalledFormat, gameInstance.Name);
			SetLatestInstallCompletion(installSequence, message);
			installTask.Complete(message);
			logger.LogInformation("Instance installation completed. InstanceId={InstanceId} MinecraftVersion={MinecraftVersion} Loader={Loader} DurationMs={DurationMs}", gameInstance.Id, request.MinecraftVersion, request.Loader, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
			InstanceInstalled?.Invoke(this, gameInstance);
		}
		catch (OperationCanceledException) when (installTask.IsCancellationRequested)
		{
			instanceNameTracker.RemovePending(request.InstanceName);
			if (installSequence == latestInstallSequence)
			{
				InstallError = string.Empty;
				InstallStatusMessage = string.Empty;
				InstallProgressPercent = 0.0;
			}
			logger.LogInformation("Instance installation canceled. MinecraftVersion={MinecraftVersion} Loader={Loader} InstanceName={InstanceName} DurationMs={DurationMs}", request.MinecraftVersion, request.Loader, request.InstanceName, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
			downloadTasksPage.CancelTask(installTask);
		}
		catch (DuplicateGameInstanceNameException exception)
		{
			instanceNameTracker.RemovePending(request.InstanceName);
			SetLatestInstallFailure(installSequence, Strings.Status_DuplicateInstanceName);
			installTask.Fail(Strings.Status_DuplicateInstanceName);
			logger.LogWarning("Instance installation rejected because the name is unavailable. InstanceName={InstanceName} DurationMs={DurationMs}", request.InstanceName, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
			logger.LogDebug(exception, "Instance installation name conflict details. InstanceName={InstanceName}", request.InstanceName);
		}
		catch (JavaRuntimeSelectionException ex2)
		{
			instanceNameTracker.RemovePending(request.InstanceName);
			SetLatestInstallFailure(installSequence, Strings.Status_JavaSelectionFailed);
			installTask.Fail(Strings.Status_JavaSelectionFailed);
			logger.LogError(ex2, "Instance installation could not select a compatible Java runtime. MinecraftVersion={MinecraftVersion} Loader={Loader} InstanceName={InstanceName} FailureReason={FailureReason} RequiredMajorVersion={RequiredMajorVersion} CurrentMajorVersion={CurrentMajorVersion} DurationMs={DurationMs}", request.MinecraftVersion, request.Loader, request.InstanceName, ex2.Reason, ex2.RequiredMajorVersion, ex2.CurrentMajorVersion, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
		}
		catch (Exception exception2)
		{
			instanceNameTracker.RemovePending(request.InstanceName);
			SetLatestInstallFailure(installSequence, Strings.Status_InstallFailed);
			installTask.Fail(Strings.Status_InstallFailed);
			logger.LogError(exception2, "Instance installation failed. MinecraftVersion={MinecraftVersion} Loader={Loader} InstanceName={InstanceName} DurationMs={DurationMs}", request.MinecraftVersion, request.Loader, request.InstanceName, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
		}
		finally
		{
			int num = Interlocked.Decrement(ref activeInstallCount);
			if (num < 0)
			{
				Interlocked.Exchange(ref activeInstallCount, 0);
				num = 0;
			}
			IsInstalling = num > 0;
			NameAvailabilityChanged?.Invoke();
		}
	}

	private void ReportInstallProgress(DownloadTaskItem installTask, LauncherProgress progress, long installSequence)
	{
		LauncherProgress launcherProgress = progress with
		{
			Message = LauncherProgressTextFormatter.Format(progress)
		};
		if (installSequence == latestInstallSequence)
		{
			InstallError = string.Empty;
			InstallStatusMessage = launcherProgress.Message;
			double? percent = launcherProgress.Percent;
			if (percent.HasValue)
			{
				double valueOrDefault = percent.GetValueOrDefault();
				InstallProgressPercent = Math.Clamp(Math.Max(InstallProgressPercent, valueOrDefault), 0.0, 99.0);
			}
		}
		installTask.Report(launcherProgress);
	}

	private void SetLatestInstallCompletion(long installSequence, string message)
	{
		if (installSequence == latestInstallSequence)
		{
			InstallError = string.Empty;
			InstallProgressPercent = 100.0;
			InstallStatusMessage = message;
		}
	}

	private void SetLatestInstallFailure(long installSequence, string message)
	{
		if (installSequence == latestInstallSequence)
		{
			InstallError = message;
			InstallStatusMessage = string.Empty;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnInstallStatusMessageChanged(string value)
	{
		OnPropertyChanged("HasInstallStatus");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnInstallErrorChanged(string value)
	{
		OnPropertyChanged("HasInstallError");
	}
}
