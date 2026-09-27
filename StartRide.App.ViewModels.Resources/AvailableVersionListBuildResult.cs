using System.Collections.Generic;

namespace StartRide.App.ViewModels.Resources;

internal sealed record AvailableVersionListBuildResult(IReadOnlyList<object> Items, int VisibleVersionCount);
