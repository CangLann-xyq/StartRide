using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace StartRide.App.ViewModels.GameSettings;

internal sealed class InstanceSettingsPersistenceCoordinator : IDisposable
{
	private readonly IGameInstanceService instanceService;

	private readonly IStatusService statusService;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger logger;

	private readonly SemaphoreSlim saveGate = new SemaphoreSlim(1, 1);

	private readonly Dictionary<string, CancellationTokenSource> pendingSaves = new Dictionary<string, CancellationTokenSource>(StringComparer.Ordinal);

	private readonly object pendingSavesLock = new object();

	private CancellationTokenSource instanceLifetime = new CancellationTokenSource();

	private GameInstance? selectedInstance;

	private string? selectedInstanceId;

	private int generation;

	private bool disposed;

	public event Action<GameInstance>? InstanceSaved;

	public InstanceSettingsPersistenceCoordinator(IGameInstanceService instanceService, IStatusService statusService, IUiDispatcher uiDispatcher, ILogger logger)
	{
		this.instanceService = instanceService;
		this.statusService = statusService;
		this.uiDispatcher = uiDispatcher;
		this.logger = logger;
	}

	public void SetInstance(GameInstance? instance)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		if (selectedInstance != instance)
		{
			selectedInstance = instance;
			selectedInstanceId = instance?.Id;
			Interlocked.Increment(ref generation);
			CancelPendingSaves();
			CancellationTokenSource value = new CancellationTokenSource();
			CancellationTokenSource cancellationTokenSource = Interlocked.Exchange(ref instanceLifetime, value);
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
		}
	}

	public void Schedule(string area, GameInstance instance, Func<GameInstance, Action?> applyMutation, Action restoreEditor, TimeSpan? delay = null)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		if (string.Equals(selectedInstanceId, instance.Id, StringComparison.OrdinalIgnoreCase))
		{
			int requestGeneration = generation;
			CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(instanceLifetime.Token);
			CancellationTokenSource value;
			lock (pendingSavesLock)
			{
				pendingSaves.Remove(area, out value);
				pendingSaves[area] = cancellationTokenSource;
			}
			value?.Cancel();
			PersistAsync(area, instance, requestGeneration, applyMutation, restoreEditor, delay, cancellationTokenSource);
		}
	}

	public void Dispose()
	{
		if (!disposed)
		{
			disposed = true;
			CancelPendingSaves();
			instanceLifetime.Cancel();
			instanceLifetime.Dispose();
		}
	}

	private async Task PersistAsync(string area, GameInstance instance, int requestGeneration, Func<GameInstance, Action?> applyMutation, Action restoreEditor, TimeSpan? delay, CancellationTokenSource cancellation)
	{
		Action rollback = null;
		bool lockTaken = false;
		try
		{
			if (delay.HasValue)
			{
				TimeSpan valueOrDefault = delay.GetValueOrDefault();
				if (valueOrDefault > TimeSpan.Zero)
				{
					await Task.Delay(valueOrDefault, cancellation.Token);
				}
			}
			await saveGate.WaitAsync(cancellation.Token);
			lockTaken = true;
			if (!IsCurrent(instance, requestGeneration))
			{
				return;
			}
			rollback = applyMutation(instance);
			if (rollback == null)
			{
				return;
			}
			await instanceService.SaveInstanceAsync(instance, cancellation.Token);
			if (IsCurrent(instance, requestGeneration))
			{
				uiDispatcher.Post(delegate
				{
					InstanceSaved?.Invoke(instance);
				});
			}
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
			rollback?.Invoke();
		}
		catch (Exception exception)
		{
			rollback?.Invoke();
			logger.LogError(exception, "Failed to save instance settings. InstanceId={InstanceId} Area={Area}", instance.Id, area);
			if (IsCurrent(instance, requestGeneration))
			{
				uiDispatcher.Post(delegate
				{
					restoreEditor();
					statusService.Report(Strings.Status_InstanceSettingsSaveFailed);
				});
			}
		}
		finally
		{
			if (lockTaken)
			{
				saveGate.Release();
			}
			lock (pendingSavesLock)
			{
				if (pendingSaves.TryGetValue(area, out CancellationTokenSource value) && value == cancellation)
				{
					pendingSaves.Remove(area);
				}
			}
			cancellation.Dispose();
		}
	}

	private bool IsCurrent(GameInstance instance, int requestGeneration)
	{
		if (requestGeneration == generation)
		{
			return string.Equals(selectedInstanceId, instance.Id, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private void CancelPendingSaves()
	{
		CancellationTokenSource[] array;
		lock (pendingSavesLock)
		{
			array = pendingSaves.Values.ToArray();
			pendingSaves.Clear();
		}
		CancellationTokenSource[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i].Cancel();
		}
	}
}
