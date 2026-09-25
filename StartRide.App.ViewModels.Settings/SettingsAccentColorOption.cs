namespace StartRide.App.ViewModels.Settings;

public sealed class SettingsAccentColorOption
{
	public string Id { get; }

	public string Title { get; }

	public SettingsAccentColorOption(string id, string title)
	{
		Id = id;
		Title = title;
	}
}
