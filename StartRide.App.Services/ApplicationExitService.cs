using System.Windows;

namespace StartRide.App.Services;

public sealed class ApplicationExitService : IApplicationExitService
{
	public void Shutdown()
	{
		System.Windows.Application.Current?.Shutdown();
	}
}
