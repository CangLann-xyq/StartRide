using System.Collections.Generic;

namespace StartRide.App.ViewModels.Download;

internal sealed record DownloadVersionFilterResult(IReadOnlyList<DownloadVersionItem> Versions, string EmptyMessage, bool ShouldClearSelectedVersion);
