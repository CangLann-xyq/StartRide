using System.Collections.Generic;
using System.Windows;

namespace StartRide.App.Services;

internal delegate TransitionRenderCacheScope TransitionRenderCacheFactory(string transitionKind, IReadOnlyList<FrameworkElement> elements);
