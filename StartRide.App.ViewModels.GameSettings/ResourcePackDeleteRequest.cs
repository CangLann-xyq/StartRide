using System.Collections.Generic;

namespace StartRide.App.ViewModels.GameSettings;

public sealed record ResourcePackDeleteRequest(IReadOnlyList<string> FullPaths, IReadOnlyList<string> Titles);
