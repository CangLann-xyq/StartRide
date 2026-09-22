namespace Launcher.App.ViewModels.GameSettings;

public sealed class GameSettingsIconOption
{
	public string Title { get; }

	public string IconSource { get; }

	public GameSettingsIconOption(string title, string iconSource)
	{
		Title = title;
		IconSource = iconSource;
	}

	public override string ToString()
	{
		return Title;
	}
}
