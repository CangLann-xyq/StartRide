using System;
using System.Threading.Tasks;

namespace Launcher.App.Services;

public sealed class ImmediateUiDispatcher : IUiDispatcher
{
	public static ImmediateUiDispatcher Instance { get; } = new ImmediateUiDispatcher();

	public bool HasAccess => true;

	private ImmediateUiDispatcher()
	{
	}

	public void Post(Action action)
	{
		action();
	}

	public void PostAfterTransition(Action action)
	{
		action();
	}

	public Task PostAfterTransitionAsync(Action action)
	{
		action();
		return Task.CompletedTask;
	}

	public void Invoke(Action action)
	{
		action();
	}

	public Task InvokeAsync(Func<Task> action)
	{
		return action();
	}
}
