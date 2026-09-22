using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Launcher.App.ViewModels.Settings;

public sealed class SettingsPersistenceCoordinator : IDisposable
{
	private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(350.0);

	private readonly ISettingsService settingsService;

	private readonly IStatusService statusService;

	private readonly ILogger logger;

	private readonly SemaphoreSlim saveLock = new SemaphoreSlim(1, 1);

	private readonly object pendingUpdatesLock = new object();

	private readonly List<Action<LauncherSettings>> pendingUpdates = new List<Action<LauncherSettings>>();

	private CancellationTokenSource? pendingSave;

	public LauncherSettings Settings { get; private set; } = new LauncherSettings();

	public bool IsPrimed { get; private set; }

	public SettingsPersistenceCoordinator(ISettingsService settingsService, IStatusService statusService, ILogger logger)
	{
		this.settingsService = settingsService;
		this.statusService = statusService;
		this.logger = logger;
	}

	public void Prime(LauncherSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings, "settings");
		CancelPendingSave();
		lock (pendingUpdatesLock)
		{
			pendingUpdates.Clear();
		}
		Settings = settings;
		IsPrimed = true;
	}

	public void Update(Action<LauncherSettings> update)
	{
		ArgumentNullException.ThrowIfNull(update, "update");
		if (IsPrimed)
		{
			update(Settings);
			lock (pendingUpdatesLock)
			{
				pendingUpdates.Add(update);
			}
			ScheduleSave();
		}
	}

	public async Task SaveImmediatelyAsync(Action<LauncherSettings> update, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(update, "update");
		if (IsPrimed)
		{
			CancelPendingSave();
			update(Settings);
			lock (pendingUpdatesLock)
			{
				pendingUpdates.Add(update);
			}
			await SaveCoreAsync(cancellationToken, update).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public void Dispose()
	{
		CancelPendingSave();
		saveLock.Dispose();
	}

	public async Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		CancelPendingSave();
		await SaveCoreAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void ScheduleSave()
	{
		CancelPendingSave();
		SaveAfterDelayAsync(pendingSave = new CancellationTokenSource());
	}

	private async Task SaveAfterDelayAsync(CancellationTokenSource cancellation)
	{
		_ = 1;
		try
		{
			await Task.Delay(SaveDelay, cancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
			await SaveCoreAsync(cancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to save launcher settings.");
			statusService.Report(Strings.Status_SettingsSaveFailed);
		}
		finally
		{
			if (Interlocked.CompareExchange(ref pendingSave, null, cancellation) == cancellation)
			{
				cancellation.Dispose();
			}
		}
	}

	private async Task SaveCoreAsync(CancellationToken cancellationToken, Action<LauncherSettings>? updateToDiscardOnFailure = null)
	{
		await saveLock.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			Action<LauncherSettings>[] updates;
			lock (pendingUpdatesLock)
			{
				if (pendingUpdates.Count == 0)
				{
					return;
				}
				updates = pendingUpdates.ToArray();
				pendingUpdates.Clear();
			}
			try
			{
				await settingsService.UpdateAsync(delegate(LauncherSettings latest)
				{
					Action<LauncherSettings>[] array = updates;
					for (int i = 0; i < array.Length; i++)
					{
						array[i](latest);
					}
				}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch
			{
				List<Action<LauncherSettings>> list = updates.ToList();
				if (updateToDiscardOnFailure != null)
				{
					int num = list.LastIndexOf(updateToDiscardOnFailure);
					if (num >= 0)
					{
						list.RemoveAt(num);
					}
				}
				if (list.Count > 0)
				{
					lock (pendingUpdatesLock)
					{
						pendingUpdates.InsertRange(0, list);
					}
				}
				throw;
			}
		}
		finally
		{
			saveLock.Release();
		}
	}

	private void CancelPendingSave()
	{
		CancellationTokenSource cancellationTokenSource = Interlocked.Exchange(ref pendingSave, null);
		if (cancellationTokenSource != null)
		{
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
		}
	}
}
