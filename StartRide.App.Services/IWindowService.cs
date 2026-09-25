using System.Windows;

namespace StartRide.App.Services;

public interface IWindowService
{
	void Attach(Window window);

	void Minimize();

	void RestoreAndActivate();

	void Close();
}
