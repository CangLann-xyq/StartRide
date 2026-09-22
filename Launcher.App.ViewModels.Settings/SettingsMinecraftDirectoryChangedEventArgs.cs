using System;

namespace Launcher.App.ViewModels.Settings;

public sealed class SettingsMinecraftDirectoryChangedEventArgs : EventArgs
{
	public string MinecraftDirectory { get; }

	public SettingsMinecraftDirectoryChangedEventArgs(string minecraftDirectory)
	{
		MinecraftDirectory = minecraftDirectory;
	}
}
