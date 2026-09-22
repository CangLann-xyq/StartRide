using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Settings;

public sealed class SettingsMemoryModeOption
{
	public MemorySettingsMode Mode { get; }

	public string Title { get; }

	public SettingsMemoryModeOption(MemorySettingsMode mode, string title)
	{
		Mode = mode;
		Title = title;
	}
}
