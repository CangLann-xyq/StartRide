namespace Launcher.App.ViewModels.Settings;

public sealed class SettingsInteractiveControlItem
{
	public string Title { get; }

	public string Category { get; }

	public SettingsInteractiveControlItem(string title, string category)
	{
		Title = title;
		Category = category;
	}
}
