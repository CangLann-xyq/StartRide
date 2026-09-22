using System.Collections.Generic;
using System.Windows;

namespace Launcher.App.Services;

internal delegate TransitionRenderCacheScope TransitionRenderCacheFactory(string transitionKind, IReadOnlyList<FrameworkElement> elements);
