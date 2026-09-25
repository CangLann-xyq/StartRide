using System;
using System.Threading;
using System.Threading.Tasks;
using StartRide.App.ViewModels.Download;
using StartRide.App.ViewModels.Settings;
using Launcher.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.Services;

public sealed class LauncherShutdownService
{
	private readonly object shutdownLock = new object();

	private readonly DownloadTasksPageViewModel downloadTasksPage;

	private readonly SettingsPageViewModel? settingsPage;

	private readonly IInstanceInstallCleanupService installCleanupService;

	private readonly IModpackWorkspaceCleanupService workspaceCleanupService;

	private readonly IModpackSandboxCleanupService sandboxCleanupService;

	private readonly IMultiplayerLobbyService? multiplayerLobbyService;

	private readonly ILogger<LauncherShutdownService> logger;

	private Task? shutdownTask;

	public LauncherShutdownService(DownloadTasksPageViewModel downloadTasksPage, IInstanceInstallCleanupService installCleanupService, IModpackWorkspaceCleanupService workspaceCleanupService, IModpackSandboxCleanupService sandboxCleanupService, SettingsPageViewModel? settingsPage = null, ILogger<LauncherShutdownService>? logger = null, IMultiplayerLobbyService? multiplayerLobbyService = null)
	{
		this.downloadTasksPage = downloadTasksPage;
		this.settingsPage = settingsPage;
		this.installCleanupService = installCleanupService;
		this.workspaceCleanupService = workspaceCleanupService;
		this.sandboxCleanupService = sandboxCleanupService;
		this.multiplayerLobbyService = multiplayerLobbyService;
		this.logger = logger ?? NullLogger<LauncherShutdownService>.Instance;
	}

	public Task PrepareForExitAsync(TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (shutdownLock)
		{
			return shutdownTask ?? (shutdownTask = PrepareForExitCoreAsync(timeout, cancellationToken));
		}
	}

	private async Task PrepareForExitCoreAsync(TimeSpan timeout, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCancellation.CancelAfter(timeout);
		downloadTasksPage.CancelAllRunningTasks();
		try
		{
			if (multiplayerLobbyService != null)
			{
				await multiplayerLobbyService.StopAsync(timeoutCancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
		{
			logger.LogWarning("Timed out stopping the multiplayer lobby during launcher exit.");
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to stop the multiplayer lobby during launcher exit.");
		}
		try
		{
			if (settingsPage != null)
			{
				await settingsPage.FlushPendingSettingsAsync(timeoutCancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
		{
			logger.LogWarning("Timed out flushing pending launcher settings during exit.");
		}
		catch (Exception exception2)
		{
			logger.LogWarning(exception2, "Failed to flush pending launcher settings during exit.");
		}
		try
		{
			if (!(await downloadTasksPage.WaitForTrackedBackgroundTasksAsync(timeout, timeoutCancellation.Token).ConfigureAwait(continueOnCapturedContext: false)))
			{
				logger.LogWarning("Timed out waiting for background download tasks during launcher exit.");
			}
		}
		catch (Exception exception3)
		{
			logger.LogWarning(exception3, "Failed while waiting for background download tasks during launcher exit.");
		}
		try
		{
			await sandboxCleanupService.WaitForPendingCleanupAsync(timeoutCancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
		{
			logger.LogWarning("Timed out cleaning modpack loader sandboxes during launcher exit.");
		}
		catch (Exception exception4)
		{
			logger.LogWarning(exception4, "Failed while cleaning modpack loader sandboxes during launcher exit.");
		}
		try
		{
			await installCleanupService.CleanupPendingAsync(timeoutCancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
		{
			logger.LogWarning("Timed out cleaning pending instance installations during launcher exit.");
		}
		catch (Exception exception5)
		{
			logger.LogWarning(exception5, "Failed to clean pending instance installations during launcher exit.");
		}
		try
		{
			await workspaceCleanupService.CleanupAllAsync(timeoutCancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
		{
			logger.LogWarning("Timed out cleaning modpack workspaces during launcher exit.");
		}
		catch (Exception exception6)
		{
			logger.LogWarning(exception6, "Failed to clean modpack workspaces during launcher exit.");
		}
	}
}
