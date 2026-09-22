using System;

namespace Launcher.App.ViewModels.GameSettings;

public sealed class LocalContentIconChangedEventArgs(string fullPath, string iconSource) : EventArgs
{
	public string FullPath { get; } = fullPath;

	public string IconSource { get; } = iconSource;
}
