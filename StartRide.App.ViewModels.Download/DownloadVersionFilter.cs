using System;
using System.Collections.Generic;
using System.Linq;
using StartRide.App.Resources;
using StartRide.App.ViewModels.Shared;

namespace StartRide.App.ViewModels.Download;

internal static class DownloadVersionFilter
{
	public static DownloadVersionFilterResult Apply(IEnumerable<DownloadVersionItem> allVersions, DownloadVersionCategory? category, string searchQuery, DownloadVersionItem? selectedVersion, bool hasLoadedVersions, bool isLoadingVersions, bool hasVersionLoadError)
	{
		if (hasVersionLoadError)
		{
			return Empty(shouldClearSelectedVersion: false);
		}
		if (category == null)
		{
			return new DownloadVersionFilterResult(Array.Empty<DownloadVersionItem>(), Strings.Status_UnimplementedCategory, ShouldClearSelectedVersion: true);
		}
		string categoryId = VersionIconResolver.NormalizeVersionType(category.Id);
		if (!ListFilterUtilities.IsKnownMinecraftCategory(categoryId))
		{
			return new DownloadVersionFilterResult(Array.Empty<DownloadVersionItem>(), Strings.Status_UnimplementedCategory, ShouldClearSelectedVersion: true);
		}
		string query = searchQuery.Trim();
		IEnumerable<DownloadVersionItem> enumerable = ListFilterUtilities.ApplyMinecraftCategory(allVersions, categoryId, (DownloadVersionItem version) => version.IsRelease, (DownloadVersionItem version) => version.IsSnapshot, (DownloadVersionItem version) => version.IsAprilFools, (DownloadVersionItem version) => version.IsBeta, (DownloadVersionItem version) => version.IsAlpha);
		if (!string.IsNullOrWhiteSpace(query))
		{
			enumerable = enumerable.Where((DownloadVersionItem version) => version.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
		}
		List<DownloadVersionItem> list = Sort(enumerable, categoryId).ToList();
		string emptyMessage = ListFilterUtilities.CreateEmptyMessage(list.Count, hasLoadedVersions, isLoadingVersions, () => CreateEmptyMessage(category.Title, query));
		bool shouldClearSelectedVersion = ListFilterUtilities.ShouldClearSelection(selectedVersion, list);
		return new DownloadVersionFilterResult(list, emptyMessage, shouldClearSelectedVersion);
	}

	private static DownloadVersionFilterResult Empty(bool shouldClearSelectedVersion)
	{
		return new DownloadVersionFilterResult(Array.Empty<DownloadVersionItem>(), string.Empty, shouldClearSelectedVersion);
	}

	private static string CreateEmptyMessage(string categoryTitle, string query)
	{
		if (!string.IsNullOrWhiteSpace(query))
		{
			return Strings.Status_NoMatchingVersions;
		}
		return string.Format(Strings.Status_NoCategoryVersionsFormat, categoryTitle);
	}

	private static IEnumerable<DownloadVersionItem> Sort(IEnumerable<DownloadVersionItem> versions, string? categoryId)
	{
		bool flag;
		switch (categoryId)
		{
		case "snapshot":
		case "april_fools":
		case "ancient":
		case "old_beta":
		case "old_alpha":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return versions.OrderByDescending((DownloadVersionItem version) => version.Version.ReleaseTime ?? DateTimeOffset.MinValue).ThenByDescending<DownloadVersionItem, string>((DownloadVersionItem version) => version.Name, StringComparer.OrdinalIgnoreCase);
		}
		return versions;
	}
}
