using System;
using System.Threading.Tasks;

namespace StartRide.App.Services;

public interface IUiDispatcher
{
	bool HasAccess { get; }

	void Post(Action action);

	void PostAfterTransition(Action action);

	Task PostAfterTransitionAsync(Action action);

	void Invoke(Action action);

	Task InvokeAsync(Func<Task> action);
}
