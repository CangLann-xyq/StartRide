using System.Collections.Generic;

namespace StartRide.App.ViewModels.GameSettings;

public sealed record ShaderPackDeleteRequest(IReadOnlyList<string> FullPaths, IReadOnlyList<string> Titles);
