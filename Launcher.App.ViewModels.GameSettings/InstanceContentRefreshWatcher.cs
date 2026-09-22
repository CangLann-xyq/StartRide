using System;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Launcher.App.ViewModels.GameSettings;

internal sealed class InstanceContentRefreshWatcher : IDisposable
{
	private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(200.0);

	private readonly object gate = new object();

	private readonly IInstanceDirectoryMonitor monitor;

	private readonly InstanceDirectoryKind directoryKind;

	private readonly Func<Task> refreshAsync;

	private readonly Action<Exception> reportFailure;

	private readonly Func<InstanceDirectoryChangedEventArgs, bool>? shouldRefresh;

	private readonly Action<InstanceDirectoryChangedEventArgs>? invalidated;

	private readonly Func<bool>? canRefresh;

	private readonly ILogger logger;

	private IInstanceDirectoryWatch? watch;

	private CancellationTokenSource? pendingDelay;

	private GameInstance? instance;

	private InstanceDirectoryChangedEventArgs? latestChange;

	private bool enabled;

	private bool refreshRunning;

	private bool trailingRefresh;

	private bool rebuildAfterRefresh;

	private int suspensionCount;

	private long generation;

	public InstanceContentRefreshWatcher(IInstanceDirectoryMonitor monitor, InstanceDirectoryKind directoryKind, Func<Task> refreshAsync, Action<Exception> reportFailure, ILogger logger, Func<InstanceDirectoryChangedEventArgs, bool>? shouldRefresh = null, Action<InstanceDirectoryChangedEventArgs>? invalidated = null, Func<bool>? canRefresh = null)
	{
		this.monitor = monitor;
		this.directoryKind = directoryKind;
		this.refreshAsync = refreshAsync;
		this.reportFailure = reportFailure;
		this.logger = logger;
		this.shouldRefresh = shouldRefresh;
		this.invalidated = invalidated;
		this.canRefresh = canRefresh;
	}

	public void SetInstance(GameInstance? value)
	{
		lock (gate)
		{
			if (IsSameWatchTarget(instance, value))
			{
				instance = value;
				return;
			}
			instance = value;
			ResetWatchLocked();
		}
	}

	public void SetEnabled(bool value)
	{
		lock (gate)
		{
			if (enabled != value)
			{
				enabled = value;
				ResetWatchLocked();
			}
		}
	}

	public void Suspend()
	{
		lock (gate)
		{
			suspensionCount++;
			if (suspensionCount == 1)
			{
				ResetWatchLocked();
			}
		}
	}

	public void Resume(bool restart = true)
	{
		lock (gate)
		{
			if (suspensionCount == 0)
			{
				return;
			}
			suspensionCount--;
			if (suspensionCount == 0)
			{
				if (restart)
				{
					ResetWatchLocked();
				}
				else
				{
					StopWatchAndInvalidateLocked();
				}
			}
		}
	}

	public void Dispose()
	{
		lock (gate)
		{
			enabled = false;
			suspensionCount++;
			StopWatchAndInvalidateLocked();
		}
	}

	private void ResetWatchLocked()
	{
		StopWatchAndInvalidateLocked();
		if (!enabled || suspensionCount > 0 || instance == null || string.IsNullOrWhiteSpace(instance.InstanceDirectory))
		{
			return;
		}
		try
		{
			watch = monitor.Watch(instance, directoryKind);
			watch.Changed += Watch_Changed;
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to start instance content watcher. InstanceId={InstanceId} DirectoryKind={DirectoryKind}", instance.Id, directoryKind);
		}
	}

	private void StopWatchAndInvalidateLocked()
	{
		generation++;
		trailingRefresh = false;
		rebuildAfterRefresh = false;
		latestChange = null;
		CancelPendingDelayLocked();
		if (watch != null)
		{
			watch.Changed -= Watch_Changed;
			watch.Dispose();
			watch = null;
		}
	}

	private void Watch_Changed(object? sender, InstanceDirectoryChangedEventArgs e)
	{
		lock (gate)
		{
			bool flag = e.ChangeType.Equals("Error", StringComparison.OrdinalIgnoreCase);
			if (sender != watch || !IsActiveLocked())
			{
				return;
			}
			if (!flag)
			{
				Func<InstanceDirectoryChangedEventArgs, bool>? func = shouldRefresh;
				if (func != null && !func(e))
				{
					return;
				}
			}
			latestChange = e;
			rebuildAfterRefresh |= flag;
			invalidated?.Invoke(e);
			Func<bool>? func2 = canRefresh;
			if (func2 != null && !func2())
			{
				if (rebuildAfterRefresh)
				{
					rebuildAfterRefresh = false;
					ResetWatchLocked();
				}
			}
			else if (refreshRunning)
			{
				trailingRefresh = true;
			}
			else
			{
				ScheduleRefreshLocked();
			}
		}
	}

	private void ScheduleRefreshLocked()
	{
		CancelPendingDelayLocked();
		RunAfterDelayAsync(pendingDelay = new CancellationTokenSource(), generation, instance);
	}

	private async Task RunAfterDelayAsync(CancellationTokenSource cancellation, long expectedGeneration, GameInstance watchedInstance)
	{
		_ = 1;
		try
		{
			await Task.Delay(RefreshDelay, cancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
			InstanceDirectoryChangedEventArgs e;
			lock (gate)
			{
				if (!IsCurrentLocked(expectedGeneration, watchedInstance) || pendingDelay != cancellation)
				{
					return;
				}
				Func<bool>? func = canRefresh;
				if (func != null && !func())
				{
					return;
				}
				pendingDelay = null;
				refreshRunning = true;
				trailingRefresh = false;
				e = latestChange;
			}
			logger.LogDebug("Detected instance content change. InstanceId={InstanceId} DirectoryKind={DirectoryKind} ChangeType={ChangeType} Path={Path}", watchedInstance.Id, directoryKind, e?.ChangeType ?? "<unknown>", e?.FullPath ?? "<unknown>");
			await refreshAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
		}
		catch (Exception ex2)
		{
			lock (gate)
			{
				if (!IsCurrentLocked(expectedGeneration, watchedInstance))
				{
					return;
				}
			}
			logger.LogError(ex2, "Failed to refresh instance content after directory change. InstanceId={InstanceId} DirectoryKind={DirectoryKind}", watchedInstance.Id, directoryKind);
			reportFailure(ex2);
		}
		finally
		{
			lock (gate)
			{
				if (pendingDelay == cancellation)
				{
					pendingDelay = null;
				}
				cancellation.Dispose();
				if (refreshRunning)
				{
					refreshRunning = false;
					if (IsActiveLocked() && rebuildAfterRefresh)
					{
						bool num = trailingRefresh;
						rebuildAfterRefresh = false;
						ResetWatchLocked();
						if (num && IsActiveLocked())
						{
							ScheduleRefreshLocked();
						}
					}
					else if (IsActiveLocked() && trailingRefresh)
					{
						trailingRefresh = false;
						ScheduleRefreshLocked();
					}
					if (!IsActiveLocked())
					{
						trailingRefresh = false;
						rebuildAfterRefresh = false;
					}
				}
			}
		}
	}

	private void CancelPendingDelayLocked()
	{
		CancellationTokenSource cancellationTokenSource = pendingDelay;
		pendingDelay = null;
		cancellationTokenSource?.Cancel();
	}

	private bool IsActiveLocked()
	{
		if (enabled && suspensionCount == 0)
		{
			return instance != null;
		}
		return false;
	}

	private bool IsCurrentLocked(long expectedGeneration, GameInstance watchedInstance)
	{
		if (expectedGeneration == generation && IsActiveLocked())
		{
			return IsSameWatchTarget(instance, watchedInstance);
		}
		return false;
	}

	private static bool IsSameWatchTarget(GameInstance? left, GameInstance? right)
	{
		if (left == null || right == null)
		{
			if (left == null)
			{
				return right == null;
			}
			return false;
		}
		if (string.Equals(left.Id, right.Id, StringComparison.Ordinal))
		{
			return string.Equals(left.InstanceDirectory, right.InstanceDirectory, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}
}
