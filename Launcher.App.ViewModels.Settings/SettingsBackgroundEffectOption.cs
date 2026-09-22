using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Settings;

public sealed class SettingsBackgroundEffectOption
{
	public string Id { get; }

	public bool IsAcrylicEnabled => LauncherBackgroundEffects.IsAcrylic(Id);

	public bool IsImageSelected => LauncherBackgroundEffects.IsImage(Id);

	public string Title { get; }

	public SettingsBackgroundEffectOption(string id, string title)
	{
		Id = id;
		Title = title;
	}
}
