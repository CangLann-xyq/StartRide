using System;
using System.Collections.Generic;
using System.Linq;
using Launcher.App.Resources;
using Launcher.App.ViewModels.Shared;

namespace Launcher.App.ViewModels.GameSettings;

internal static class GameSettingsInstanceFilter
{
	public static GameSettingsInstanceFilterResult Apply(IEnumerable<GameSettingsInstanceItem> allInstances, GameSettingsInstanceCategory? category, string searchQuery, GameSettingsInstanceItem? selectedInstance, bool hasLoadedInstances, bool isLoadingInstances, bool hasInstanceLoadError)
	{
		if (hasInstanceLoadError)
		{
			return Empty(shouldClearSelectedInstance: false);
		}
		string query = searchQuery.Trim();
		IEnumerable<GameSettingsInstanceItem> source = ((category?.Id == "mod_loader") ? allInstances.Where((GameSettingsInstanceItem instance) => instance.HasModLoader) : ListFilterUtilities.ApplyMinecraftCategory(allInstances, category?.Id, (GameSettingsInstanceItem instance) => instance.IsRelease, (GameSettingsInstanceItem instance) => instance.IsSnapshot, (GameSettingsInstanceItem instance) => instance.IsAprilFools, (GameSettingsInstanceItem instance) => instance.IsBeta, (GameSettingsInstanceItem instance) => instance.IsAlpha));
		string text = category?.Id;
		if ((text == null || text == "all") ? true : false)
		{
			source = allInstances;
		}
		bool flag;
		switch (category?.Id)
		{
		case null:
		case "all":
		case "mod_loader":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag && !ListFilterUtilities.IsKnownMinecraftCategory(category.Id))
		{
			source = allInstances;
		}
		if (!string.IsNullOrWhiteSpace(query))
		{
			source = source.Where((GameSettingsInstanceItem instance) => instance.MatchesSearch(query));
		}
		List<GameSettingsInstanceItem> list = (from instance in source
			orderby instance.Instance.CreatedAt descending, instance.Instance.UpdatedAt descending
			select instance).ToList();
		string emptyMessage = ListFilterUtilities.CreateEmptyMessage(list.Count, hasLoadedInstances, isLoadingInstances, () => CreateEmptyMessage(category, query));
		bool shouldClearSelectedInstance = ListFilterUtilities.ShouldClearSelection(selectedInstance, list);
		return new GameSettingsInstanceFilterResult(list, emptyMessage, shouldClearSelectedInstance);
	}

	private static GameSettingsInstanceFilterResult Empty(bool shouldClearSelectedInstance)
	{
		return new GameSettingsInstanceFilterResult(Array.Empty<GameSettingsInstanceItem>(), string.Empty, shouldClearSelectedInstance);
	}

	private static string CreateEmptyMessage(GameSettingsInstanceCategory? category, string query)
	{
		if (!string.IsNullOrWhiteSpace(query))
		{
			return Strings.GameSettings_NoMatchingInstances;
		}
		string text = category?.Id;
		if ((text == null || text == "all") ? true : false)
		{
			return Strings.GameSettings_NoInstances;
		}
		return string.Format(Strings.GameSettings_NoCategoryInstancesFormat, category.Title);
	}
}
