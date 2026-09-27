using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Settings;

public sealed class SettingsUpdateChannelOption
{
	public LauncherUpdateChannel Channel { get; }

	public string Title { get; }

	public SettingsUpdateChannelOption(LauncherUpdateChannel channel, string title)
	{
		Channel = channel;
		Title = title;
	}
}
