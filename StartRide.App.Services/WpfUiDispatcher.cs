using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace StartRide.App.Services;

public sealed class WpfUiDispatcher : IUiDispatcher
{
	public bool HasAccess
	{
		get
		{
			System.Windows.Application current = System.Windows.Application.Current;
			bool? obj;
			if (current == null)
			{
				obj = null;
			}
			else
			{
				Dispatcher dispatcher = ((DispatcherObject)current).Dispatcher;
				obj = ((dispatcher != null) ? new bool?(dispatcher.CheckAccess()) : ((bool?)null));
			}
			return obj ?? true;
		}
	}

	public void Post(Action action)
	{
		System.Windows.Application current = System.Windows.Application.Current;
		Dispatcher val = ((current != null) ? ((DispatcherObject)current).Dispatcher : null);
		if (val == null)
		{
			action();
		}
		else
		{
			val.BeginInvoke((Delegate)action, (DispatcherPriority)4, Array.Empty<object>());
		}
	}

	public void PostAfterTransition(Action action)
	{
		UiTransitionGate.RunWhenIdle(action);
	}

	public Task PostAfterTransitionAsync(Action action)
	{
		ArgumentNullException.ThrowIfNull(action, "action");
		TaskCompletionSource completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		UiTransitionGate.RunWhenIdle(delegate
		{
			try
			{
				action();
				completed.TrySetResult();
			}
			catch (Exception exception)
			{
				completed.TrySetException(exception);
			}
		});
		return completed.Task;
	}

	public void Invoke(Action action)
	{
		System.Windows.Application current = System.Windows.Application.Current;
		Dispatcher val = ((current != null) ? ((DispatcherObject)current).Dispatcher : null);
		if (val == null || val.CheckAccess())
		{
			action();
		}
		else
		{
			val.Invoke(action);
		}
	}

	public Task InvokeAsync(Func<Task> action)
	{
		System.Windows.Application current = System.Windows.Application.Current;
		Dispatcher val = ((current != null) ? ((DispatcherObject)current).Dispatcher : null);
		if (val == null || val.CheckAccess())
		{
			return action();
		}
		return val.InvokeAsync<Task>(action, (DispatcherPriority)4).Task.Unwrap();
	}
}
