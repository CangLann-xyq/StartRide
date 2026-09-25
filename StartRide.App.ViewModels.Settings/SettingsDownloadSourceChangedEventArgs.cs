using System;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Settings;

public sealed class SettingsDownloadSourceChangedEventArgs : EventArgs
{
	public DownloadSourcePreference Preference { get; }

	public SettingsDownloadSourceChangedEventArgs(DownloadSourcePreference preference)
	{
		Preference = preference;
	}
}
