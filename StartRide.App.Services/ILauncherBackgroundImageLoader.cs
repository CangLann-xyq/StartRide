using System.Windows.Media;

namespace StartRide.App.Services;

public interface ILauncherBackgroundImageLoader
{
	ImageSource Load(string path);
}
