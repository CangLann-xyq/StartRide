using System;
using System.Collections.Generic;

namespace Launcher.App.ViewModels.Resources;

public sealed class ResourcesFilterOptionItem
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	public IReadOnlyList<string> MinecraftVersions { get; init; } = Array.Empty<string>();
}
