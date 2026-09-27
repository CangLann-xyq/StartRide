using System;

namespace StartRide.App.ViewModels.Settings;

public sealed class SettingsMaximumDownloadConcurrencyChangedEventArgs : EventArgs
{
	public int MaximumDownloadConcurrency { get; }

	public SettingsMaximumDownloadConcurrencyChangedEventArgs(int maximumDownloadConcurrency)
	{
		MaximumDownloadConcurrency = maximumDownloadConcurrency;
	}
}
