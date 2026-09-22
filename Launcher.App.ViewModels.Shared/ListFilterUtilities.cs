using System;
using System.Collections.Generic;
using System.Linq;

namespace Launcher.App.ViewModels.Shared;

internal static class ListFilterUtilities
{
	public static IEnumerable<T> ApplyMinecraftCategory<T>(IEnumerable<T> items, string? categoryId, Func<T, bool> isRelease, Func<T, bool> isSnapshot, Func<T, bool> isAprilFools, Func<T, bool> isBeta, Func<T, bool> isAlpha)
	{
		return categoryId switch
		{
			"snapshot" => items.Where((T item) => isSnapshot(item) && !isAprilFools(item)), 
			"april_fools" => items.Where(isAprilFools), 
			"ancient" => items.Where((T item) => isBeta(item) || isAlpha(item)), 
			"old_beta" => items.Where(isBeta), 
			"old_alpha" => items.Where(isAlpha), 
			_ => items.Where(isRelease), 
		};
	}

	public static bool IsKnownMinecraftCategory(string? categoryId)
	{
		switch (categoryId)
		{
		case "release":
		case "snapshot":
		case "april_fools":
		case "ancient":
		case "old_beta":
		case "old_alpha":
			return true;
		default:
			return false;
		}
	}

	public static string CreateEmptyMessage(int itemCount, bool hasLoadedItems, bool isLoadingItems, Func<string> createMessage)
	{
		if (!((itemCount == 0) & hasLoadedItems) || isLoadingItems)
		{
			return string.Empty;
		}
		return createMessage();
	}

	public static bool ShouldClearSelection<T>(T? selectedItem, IReadOnlyCollection<T> visibleItems) where T : class
	{
		if (selectedItem != null)
		{
			return !visibleItems.Contains<T>(selectedItem);
		}
		return false;
	}
}
