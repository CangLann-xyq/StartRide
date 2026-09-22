using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Launcher.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Launcher.App.ViewModels.GameSettings;

internal sealed class LocalContentRefreshCoordinator<TContent> : IDisposable
{
	private readonly Func<GameInstance, CancellationToken, Task<IReadOnlyList<TContent>>> loadAsync;

	private readonly InstanceDirectoryKind directoryKind;

	private readonly Action<IReadOnlyList<TContent>> apply;

	private readonly Action clear;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger logger;

	private readonly InstanceContentRefreshWatcher watcher;

	private CancellationTokenSource? refreshCancellation;

	private int refreshGeneration;

	private long invalidationRevision;

	private long appliedInvalidationRevision;

	private bool observationArmed;

	private volatile bool isSectionActive;

	public GameInstance? SelectedInstance { get; private set; }

	public IReadOnlyList<TContent> CurrentItems { get; private set; } = Array.Empty<TContent>();

	public long Revision { get; private set; }

	public LocalContentRefreshCoordinator(IInstanceDirectoryMonitor monitor, InstanceDirectoryKind directoryKind, Func<GameInstance, CancellationToken, Task<IReadOnlyList<TContent>>> loadAsync, Action<IReadOnlyList<TContent>> apply, Action clear, Action<Exception> reportWatcherFailure, IUiDispatcher uiDispatcher, ILogger logger, Func<InstanceDirectoryChangedEventArgs, bool>? shouldRefresh = null)
	{
		this.loadAsync = loadAsync;
		this.directoryKind = directoryKind;
		this.apply = apply;
		this.clear = clear;
		this.uiDispatcher = uiDispatcher;
		this.logger = logger;
		watcher = new InstanceContentRefreshWatcher(monitor, directoryKind, RefreshAsync, reportWatcherFailure, logger, shouldRefresh, delegate
		{
			Interlocked.Increment(ref invalidationRevision);
		}, () => isSectionActive);
	}

	public void SetInstance(GameInstance? instance)
	{
		if ((SelectedInstance != null && IsSameInstancePath(SelectedInstance, instance)) || (SelectedInstance == null && instance == null))
		{
			SelectedInstance = instance;
			watcher.SetInstance(instance);
			return;
		}
		watcher.SetEnabled(value: false);
		observationArmed = false;
		SelectedInstance = instance;
		Interlocked.Exchange(ref invalidationRevision, 0L);
		Interlocked.Exchange(ref appliedInvalidationRevision, 0L);
		Interlocked.Increment(ref refreshGeneration);
		CancelRefresh();
		watcher.SetInstance(instance);
		CurrentItems = Array.Empty<TContent>();
		uiDispatcher.Invoke(clear);
	}

	public void SetSectionActive(bool active)
	{
		isSectionActive = active;
		if (active && SelectedInstance != null && !observationArmed)
		{
			observationArmed = true;
			watcher.SetEnabled(value: true);
		}
		if (!active)
		{
			if (Volatile.Read(in refreshCancellation) != null)
			{
				Interlocked.Increment(ref invalidationRevision);
			}
			Interlocked.Increment(ref refreshGeneration);
			CancelRefresh();
		}
	}

	public Task<bool> RefreshIfInvalidatedAsync()
	{
		if (!isSectionActive || Interlocked.Read(in invalidationRevision) <= Interlocked.Read(in appliedInvalidationRevision))
		{
			return Task.FromResult(result: true);
		}
		return RefreshAsync();
	}

	public void InvalidateSnapshot()
	{
		Interlocked.Increment(ref invalidationRevision);
	}

	public void ReleaseObservation()
	{
		isSectionActive = false;
		if (observationArmed)
		{
			Interlocked.Increment(ref invalidationRevision);
		}
		observationArmed = false;
		watcher.SetEnabled(value: false);
		Interlocked.Increment(ref refreshGeneration);
		CancelRefresh();
	}

	public void SuspendForRename()
	{
		watcher.Suspend();
		CancelRefresh();
	}

	public void ResumeAfterRename(bool restart = true)
	{
		watcher.Resume(restart);
	}

	public void SuspendForInternalOperation()
	{
		watcher.Suspend();
	}

	public void ResumeAfterInternalOperation()
	{
		watcher.Resume();
	}

	public async Task<TResult> ExecuteInternalOperationAsync<TResult>(Func<Task<TResult>> operation, Func<TResult, bool>? shouldRefresh = null)
	{
		watcher.Suspend();
		try
		{
			TResult result = await operation().ConfigureAwait(continueOnCapturedContext: false);
			if (shouldRefresh?.Invoke(result) ?? true)
			{
				await RefreshAsync().ConfigureAwait(continueOnCapturedContext: false);
			}
			return result;
		}
		finally
		{
			watcher.Resume();
		}
	}

	public async Task ExecuteInternalOperationAsync(Func<Task> operation)
	{
		watcher.Suspend();
		try
		{
			await operation().ConfigureAwait(continueOnCapturedContext: false);
			await RefreshAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			watcher.Resume();
		}
	}

	public async Task<bool> RefreshAsync()
	{
		long invalidationAtStart = Interlocked.Read(in invalidationRevision);
		int generation = Interlocked.Increment(ref refreshGeneration);
		CancellationTokenSource replacement = new CancellationTokenSource();
		CancellationTokenSource? cancellationTokenSource = Interlocked.Exchange(ref refreshCancellation, replacement);
		cancellationTokenSource?.Cancel();
		cancellationTokenSource?.Dispose();
		GameInstance instance = SelectedInstance;
		if (instance == null)
		{
			bool result = CurrentItems.Count > 0;
			if (CurrentItems.Count > 0)
			{
				CurrentItems = Array.Empty<TContent>();
				Revision++;
				uiDispatcher.Invoke(clear);
			}
			Release(replacement);
			return result;
		}
		try
		{
			IReadOnlyList<TContent> items = await loadAsync(instance, replacement.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (generation != refreshGeneration || replacement.IsCancellationRequested || !IsSameInstancePath(instance, SelectedInstance))
			{
				return false;
			}
			bool published = false;
			uiDispatcher.Invoke(delegate
			{
				if (generation == refreshGeneration && !replacement.IsCancellationRequested && IsSameInstancePath(instance, SelectedInstance))
				{
					if (!HasReferenceChanges(CurrentItems, items))
					{
						AdvanceAppliedInvalidationRevision(invalidationAtStart);
						published = true;
					}
					else
					{
						CurrentItems = items;
						long revision = Revision;
						Revision = revision + 1;
						apply(items);
						AdvanceAppliedInvalidationRevision(invalidationAtStart);
						published = true;
					}
				}
			});
			return published;
		}
		catch (OperationCanceledException) when (replacement.IsCancellationRequested)
		{
			return false;
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to refresh local instance content. InstanceId={InstanceId} DirectoryKind={DirectoryKind}", instance.Id, directoryKind);
			throw;
		}
		finally
		{
			Release(replacement);
		}
	}

	public void Dispose()
	{
		watcher.Dispose();
		CancelRefresh();
	}

	private void CancelRefresh()
	{
		CancellationTokenSource? cancellationTokenSource = Interlocked.Exchange(ref refreshCancellation, null);
		cancellationTokenSource?.Cancel();
		cancellationTokenSource?.Dispose();
	}

	private void Release(CancellationTokenSource cancellation)
	{
		if (Interlocked.CompareExchange(ref refreshCancellation, null, cancellation) == cancellation)
		{
			cancellation.Dispose();
		}
	}

	private void AdvanceAppliedInvalidationRevision(long revision)
	{
		long num;
		do
		{
			num = Interlocked.Read(in appliedInvalidationRevision);
		}
		while (num < revision && Interlocked.CompareExchange(ref appliedInvalidationRevision, revision, num) != num);
	}

	private static bool IsSameInstancePath(GameInstance left, GameInstance? right)
	{
		if (right != null && string.Equals(left.Id, right.Id, StringComparison.Ordinal))
		{
			return string.Equals(left.InstanceDirectory, right.InstanceDirectory, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static bool HasReferenceChanges(IReadOnlyList<TContent> current, IReadOnlyList<TContent> next)
	{
		if (current.Count != next.Count)
		{
			return true;
		}
		for (int i = 0; i < current.Count; i++)
		{
			if ((object)current[i] != (object)next[i])
			{
				return true;
			}
		}
		return false;
	}
}
