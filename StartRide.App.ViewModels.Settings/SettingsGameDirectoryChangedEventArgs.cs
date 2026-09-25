using System;

namespace StartRide.App.ViewModels.Settings;

public sealed class SettingsGameDirectoryChangedEventArgs : EventArgs
{
	public string MinecraftDirectory { get; }

	public SettingsGameDirectoryChangedEventArgs(string minecraftDirectory)
	{
		MinecraftDirectory = minecraftDirectory;
	}
}
