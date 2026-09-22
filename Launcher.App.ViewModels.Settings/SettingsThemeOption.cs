namespace Launcher.App.ViewModels.Settings;

public sealed class SettingsThemeOption
{
	public string Id { get; }

	public string Title { get; }

	public SettingsThemeOption(string id, string title)
	{
		Id = id;
		Title = title;
	}
}
