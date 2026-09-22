using System.Collections.Generic;

namespace Launcher.App.ViewModels.GameSettings;

internal sealed record GameSettingsInstanceFilterResult(IReadOnlyList<GameSettingsInstanceItem> Instances, string EmptyMessage, bool ShouldClearSelectedInstance);
