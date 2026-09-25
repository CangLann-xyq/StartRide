using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace StartRide.App.Services;

internal static class UiTransitionGate
{
	private readonly record struct DeferredAction(Action Action, DateTime Deadline);

	private static readonly List<DeferredAction> DeferredActions = new List<DeferredAction>();

	private static int activeTransitionCount;

	private static Dispatcher? uiDispatcher;

	private static DispatcherTimer? deadlineWatchdog;

	internal static readonly TimeSpan MaximumDeferral = TimeSpan.FromSeconds(2.0);

	private static readonly TimeSpan DeadlineCheckInterval = TimeSpan.FromMilliseconds(250.0);

	internal static bool IsTransitionActive => activeTransitionCount > 0;

	internal static int PendingCount => DeferredActions.Count;

	internal static void AttachDispatcher(Dispatcher dispatcher)
	{
		if (uiDispatcher == null)
		{
			uiDispatcher = dispatcher;
		}
	}

	private static Dispatcher? ResolveDispatcher()
	{
		Dispatcher dispatcher = uiDispatcher;
		if (dispatcher == null)
		{
			System.Windows.Application current = System.Windows.Application.Current;
			if (current == null)
			{
				return null;
			}
			dispatcher = ((DispatcherObject)current).Dispatcher;
		}
		return dispatcher;
	}

	internal static void Enter()
	{
		activeTransitionCount++;
	}

	internal static void Exit()
	{
		if (activeTransitionCount != 0)
		{
			activeTransitionCount--;
			if (activeTransitionCount <= 0)
			{
				DrainDeferredActions();
			}
		}
	}

	internal static void RunWhenIdle(Action action)
	{
		ArgumentNullException.ThrowIfNull(action, "action");
		Dispatcher val = ResolveDispatcher();
		if (val == null)
		{
			action();
			return;
		}
		if (!val.CheckAccess())
		{
			val.BeginInvoke((Delegate)(Action)delegate
			{
				RunWhenIdle(action);
			}, (DispatcherPriority)4, Array.Empty<object>());
			return;
		}
		DeferredAction deferred = new DeferredAction(action, DateTime.UtcNow + MaximumDeferral);
		if (!IsTransitionActive)
		{
			val.BeginInvoke((Delegate)(Action)delegate
			{
				if (IsTransitionActive && DateTime.UtcNow < deferred.Deadline)
				{
					Defer(deferred);
				}
				else
				{
					deferred.Action();
				}
			}, (DispatcherPriority)4, Array.Empty<object>());
		}
		else
		{
			Defer(deferred);
		}
	}

	internal static async Task WaitForIdleAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		DateTime deadline = DateTime.UtcNow + MaximumDeferral;
		while (IsTransitionActive)
		{
			TimeSpan timeSpan = deadline - DateTime.UtcNow;
			if (timeSpan <= TimeSpan.Zero)
			{
				break;
			}
			TaskCompletionSource released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			RunWhenIdle(delegate
			{
				released.TrySetResult();
			});
			if (await Task.WhenAny(released.Task, Task.Delay(timeSpan, cancellationToken)).ConfigureAwait(continueOnCapturedContext: true) != released.Task)
			{
				cancellationToken.ThrowIfCancellationRequested();
				break;
			}
		}
	}

	internal static void ResetForTesting(Dispatcher? dispatcher = null)
	{
		activeTransitionCount = 0;
		DeferredActions.Clear();
		StopDeadlineWatchdog();
		uiDispatcher = dispatcher;
	}

	private static void Defer(DeferredAction deferred)
	{
		DeferredActions.Add(deferred);
		EnsureDeadlineWatchdog();
	}

	private static void EnsureDeadlineWatchdog()
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		if (deadlineWatchdog != null || DeferredActions.Count == 0)
		{
			return;
		}
		Dispatcher val = ResolveDispatcher();
		if (val != null)
		{
			DispatcherTimer val2 = new DispatcherTimer((DispatcherPriority)4, val)
			{
				Interval = DeadlineCheckInterval
			};
			val2.Tick += delegate
			{
				ReleaseExpiredActions();
			};
			deadlineWatchdog = val2;
			val2.Start();
		}
	}

	private static void StopDeadlineWatchdog()
	{
		DispatcherTimer? obj = deadlineWatchdog;
		if (obj != null)
		{
			obj.Stop();
		}
		deadlineWatchdog = null;
	}

	private static void ReleaseExpiredActions()
	{
		DateTime now = DateTime.UtcNow;
		DeferredAction[] array = DeferredActions.Where((DeferredAction deferred) => now >= deferred.Deadline).ToArray();
		DeferredActions.RemoveAll((DeferredAction deferred) => now >= deferred.Deadline);
		if (DeferredActions.Count == 0)
		{
			StopDeadlineWatchdog();
		}
		Dispatcher val = ResolveDispatcher();
		DeferredAction[] array2 = array;
		for (int num = 0; num < array2.Length; num++)
		{
			DeferredAction deferredAction = array2[num];
			if (val == null)
			{
				deferredAction.Action();
			}
			else
			{
				val.BeginInvoke((Delegate)deferredAction.Action, (DispatcherPriority)4, Array.Empty<object>());
			}
		}
	}

	private static void DrainDeferredActions()
	{
		if (DeferredActions.Count == 0)
		{
			return;
		}
		DeferredAction[] array = DeferredActions.ToArray();
		DeferredActions.Clear();
		StopDeadlineWatchdog();
		Dispatcher val = ResolveDispatcher();
		DeferredAction[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			DeferredAction deferred = array2[i];
			if (val == null)
			{
				deferred.Action();
				continue;
			}
			val.BeginInvoke((Delegate)(Action)delegate
			{
				if (IsTransitionActive && DateTime.UtcNow < deferred.Deadline)
				{
					Defer(deferred);
				}
				else
				{
					deferred.Action();
				}
			}, (DispatcherPriority)4, Array.Empty<object>());
		}
	}
}
