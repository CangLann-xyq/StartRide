namespace Launcher.App.ViewModels.Settings;

public sealed class SettingsJavaSelectionOption
{
	public string Id { get; }

	public string Title { get; }

	public SettingsJavaSelectionOption(string id, string title)
	{
		Id = id;
		Title = title;
	}
}
