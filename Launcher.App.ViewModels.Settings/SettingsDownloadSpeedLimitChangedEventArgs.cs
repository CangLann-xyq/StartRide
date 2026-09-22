using System;

namespace Launcher.App.ViewModels.Settings;

public sealed class SettingsDownloadSpeedLimitChangedEventArgs : EventArgs
{
	public int DownloadSpeedLimitMbPerSecond { get; }

	public SettingsDownloadSpeedLimitChangedEventArgs(int downloadSpeedLimitMbPerSecond)
	{
		DownloadSpeedLimitMbPerSecond = downloadSpeedLimitMbPerSecond;
	}
}
