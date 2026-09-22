using System;
using System.Collections.Generic;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Resources;

public sealed class ResourcesModpackManualDownloadsRequestedEventArgs : EventArgs
{
	public GameInstance Instance { get; }

	public IReadOnlyList<ManualModpackDownload> ManualDownloads { get; }

	public ResourcesModpackManualDownloadsRequestedEventArgs(GameInstance instance, IReadOnlyList<ManualModpackDownload> manualDownloads)
	{
		Instance = instance;
		ManualDownloads = manualDownloads;
	}
}
