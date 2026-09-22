using System.Windows.Media;

namespace Launcher.App.Services;

public interface ILauncherBackgroundImageLoader
{
	ImageSource Load(string path);
}
