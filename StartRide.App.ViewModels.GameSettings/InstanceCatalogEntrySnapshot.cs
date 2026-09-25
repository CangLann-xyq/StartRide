using System;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.GameSettings;

internal readonly record struct InstanceCatalogEntrySnapshot(string Id, string Name, string MinecraftVersion, string VersionName, string VersionType, LoaderKind Loader, string? LoaderVersion, string? IconSource, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string InstanceDirectory)
{
	public static InstanceCatalogEntrySnapshot Create(GameInstance instance)
	{
		return new InstanceCatalogEntrySnapshot(instance.Id, instance.Name, instance.MinecraftVersion, instance.VersionName, instance.VersionType, instance.Loader, instance.LoaderVersion, instance.IconSource, instance.CreatedAt, instance.UpdatedAt, instance.InstanceDirectory);
	}
}
