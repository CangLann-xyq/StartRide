using System.Collections.Generic;

namespace Launcher.App.ViewModels.Resources;

internal sealed record AvailableVersionListBuildResult(IReadOnlyList<object> Items, int VisibleVersionCount);
