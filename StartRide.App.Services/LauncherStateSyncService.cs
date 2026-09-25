using System;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.Services;

public sealed class LauncherStateSyncService : IDisposable
{
	private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(300.0);

	private readonly object syncLock = new object();

	private readonly ILauncherStateMonitor stateMonitor;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger<LauncherStateSyncService> logger;

	private readonly TimeSpan debounceDelay;

	private Func<LauncherSettings>? settingsProvider;

	private Func<Task>? synchronize;

	private CancellationTokenSource? workerCancellation;

	private Task pendingTask = Task.CompletedTask;

	private DateTimeOffset ignoreMonitorChangesUntil;

	private long requestGeneration;

	private bool syncRequested;

	private bool workerRunning;

	private bool isStarted;

	private bool isDisposed;

	public LauncherStateSyncService(ILauncherStateMonitor stateMonitor, IUiDispatcher uiDispatcher, ILogger<LauncherStateSyncService>? logger = null, TimeSpan? debounceDelay = null)
	{
		this.stateMonitor = stateMonitor;
		this.uiDispatcher = uiDispatcher;
		this.logger = logger ?? NullLogger<LauncherStateSyncService>.Instance;
		this.debounceDelay = debounceDelay ?? DefaultDebounceDelay;
	}

	public void Start(Func<LauncherSettings> settingsProvider, Func<Task> synchronize)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
		ArgumentNullException.ThrowIfNull(settingsProvider, "settingsProvider");
		ArgumentNullException.ThrowIfNull(synchronize, "synchronize");
		if (isStarted)
		{
			Stop();
		}
		this.settingsProvider = settingsProvider;
		this.synchronize = synchronize;
		stateMonitor.StateChanged += StateMonitor_StateChanged;
		stateMonitor.Watch(settingsProvider());
		isStarted = true;
	}

	public void RequestSync()
	{
		lock (syncLock)
		{
			if (isStarted)
			{
				syncRequested = true;
				requestGeneration++;
				if (!workerRunning)
				{
					workerRunning = true;
					workerCancellation = new CancellationTokenSource();
					pendingTask = ProcessRequestsAsync(workerCancellation);
				}
			}
		}
	}

	public Task WaitForPendingSyncAsync()
	{
		lock (syncLock)
		{
			return pendingTask;
		}
	}

	public void AcknowledgeLocalStateChange()
	{
		Func<LauncherSettings> func;
		lock (syncLock)
		{
			if (!isStarted)
			{
				return;
			}
			ignoreMonitorChangesUntil = DateTimeOffset.UtcNow + debounceDelay;
			syncRequested = false;
			func = settingsProvider;
		}
		if (func != null)
		{
			stateMonitor.Watch(func());
		}
	}

	public void Stop()
	{
		if (isStarted)
		{
			stateMonitor.StateChanged -= StateMonitor_StateChanged;
			isStarted = false;
			settingsProvider = null;
			synchronize = null;
			lock (syncLock)
			{
				syncRequested = false;
				workerCancellation?.Cancel();
			}
			stateMonitor.Stop();
		}
	}

	public void Dispose()
	{
		if (!isDisposed)
		{
			Stop();
			isDisposed = true;
		}
	}

	private void StateMonitor_StateChanged(object? sender, EventArgs e)
	{
		lock (syncLock)
		{
			if (DateTimeOffset.UtcNow < ignoreMonitorChangesUntil)
			{
				return;
			}
		}
		RequestSync();
	}

	private async Task ProcessRequestsAsync(CancellationTokenSource cancellation)
	{
		_ = 1;
		try
		{
			while (true)
			{
				long observedGeneration;
				lock (syncLock)
				{
					if (!isStarted || !syncRequested)
					{
						CompleteWorkerLocked(cancellation);
						break;
					}
					observedGeneration = requestGeneration;
				}
				await Task.Delay(debounceDelay, cancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
				Func<Task> func;
				Func<LauncherSettings> getSettings;
				lock (syncLock)
				{
					if (!isStarted || !syncRequested)
					{
						CompleteWorkerLocked(cancellation);
						break;
					}
					if (observedGeneration != requestGeneration)
					{
						continue;
					}
					syncRequested = false;
					func = synchronize;
					getSettings = settingsProvider;
					goto IL_0159;
				}
				IL_0159:
				if (func == null || getSettings == null)
				{
					continue;
				}
				try
				{
					await uiDispatcher.InvokeAsync(func).ConfigureAwait(continueOnCapturedContext: false);
					if (!cancellation.IsCancellationRequested && isStarted)
					{
						stateMonitor.Watch(getSettings());
					}
				}
				catch (Exception exception)
				{
					logger.LogWarning(exception, "Failed to synchronize launcher state after a monitored change.");
				}
				getSettings = null;
			}
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
		}
		finally
		{
			lock (syncLock)
			{
				CompleteWorkerLocked(cancellation);
			}
			cancellation.Dispose();
		}
	}

	private void CompleteWorkerLocked(CancellationTokenSource cancellation)
	{
		if (workerCancellation == cancellation)
		{
			workerCancellation = null;
			workerRunning = false;
		}
	}
}
