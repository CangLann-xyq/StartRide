using System.Collections.Generic;

namespace Launcher.App.ViewModels.Download;

internal sealed record DownloadVersionFilterResult(IReadOnlyList<DownloadMinecraftVersionItem> Versions, string EmptyMessage, bool ShouldClearSelectedVersion);
