using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Settings;

public sealed class SettingsDownloadSourceOption
{
	public DownloadSourcePreference Preference { get; }

	public string Title { get; }

	public SettingsDownloadSourceOption(DownloadSourcePreference preference, string title)
	{
		Preference = preference;
		Title = title;
	}
}
