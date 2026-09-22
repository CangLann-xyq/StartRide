using System;
using System.Collections.Generic;

namespace Launcher.App.ViewModels.GameSettings;

internal static class LocalContentListPresentation
{
	public static bool HasSameReferences<T>(IReadOnlyList<T> current, IReadOnlyList<T> next) where T : class
	{
		if (current.Count != next.Count)
		{
			return false;
		}
		for (int i = 0; i < current.Count; i++)
		{
			if (current[i] != next[i])
			{
				return false;
			}
		}
		return true;
	}

	public static IReadOnlyList<object> CreateSectionedItems<TItem>(IReadOnlyList<TItem> visibleItems, object infoSection, object listSection, bool includeInfoSection) where TItem : class
	{
		if (!includeInfoSection)
		{
			return Array.Empty<object>();
		}
		bool flag = visibleItems.Count > 0;
		object[] array = new object[visibleItems.Count + ((!flag) ? 1 : 2)];
		array[0] = infoSection;
		if (flag)
		{
			array[1] = listSection;
		}
		for (int i = 0; i < visibleItems.Count; i++)
		{
			array[i + ((!flag) ? 1 : 2)] = visibleItems[i];
		}
		return array;
	}
}
