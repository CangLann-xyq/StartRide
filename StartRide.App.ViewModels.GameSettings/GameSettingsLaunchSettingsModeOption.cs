using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class GameSettingsLaunchSettingsModeOption
{
	public LaunchSettingsMode Mode { get; }

	public string Title { get; }

	public GameSettingsLaunchSettingsModeOption(LaunchSettingsMode mode, string title)
	{
		Mode = mode;
		Title = title;
	}

	public override string ToString()
	{
		return Title;
	}
}
