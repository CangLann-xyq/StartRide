using System;

namespace StartRide.App.Services;

internal sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
{
	private readonly Action<T> callback = callback ?? throw new ArgumentNullException("callback");

	public void Report(T value)
	{
		callback(value);
	}
}
