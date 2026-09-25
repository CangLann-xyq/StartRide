using System;
using System.Collections.Generic;
using System.Linq;

namespace StartRide.App.ViewModels.Shared;

internal static class StableFilteredItemProjection
{
	public static IReadOnlyList<TItem> Synchronize<TSource, TKey, TItem>(IReadOnlyList<TSource> sourceItems, IDictionary<TKey, TItem> itemCache, Func<TSource, TKey> keySelector, Func<TSource, TItem> createItem, Action<TItem, TSource> updateItem, Func<TSource, bool> visibilityPredicate) where TKey : notnull
	{
		ArgumentNullException.ThrowIfNull(sourceItems, "sourceItems");
		ArgumentNullException.ThrowIfNull(itemCache, "itemCache");
		ArgumentNullException.ThrowIfNull(keySelector, "keySelector");
		ArgumentNullException.ThrowIfNull(createItem, "createItem");
		ArgumentNullException.ThrowIfNull(updateItem, "updateItem");
		ArgumentNullException.ThrowIfNull(visibilityPredicate, "visibilityPredicate");
		HashSet<TKey> nextKeys = new HashSet<TKey>();
		List<TItem> list = new List<TItem>(sourceItems.Count);
		foreach (TSource sourceItem in sourceItems)
		{
			TKey val = keySelector(sourceItem);
			nextKeys.Add(val);
			if (!itemCache.TryGetValue(val, out TItem value))
			{
				value = (itemCache[val] = createItem(sourceItem));
			}
			else
			{
				updateItem(value, sourceItem);
			}
			if (visibilityPredicate(sourceItem))
			{
				list.Add(value);
			}
		}
		TKey[] array = itemCache.Keys.Where((TKey item) => !nextKeys.Contains(item)).ToArray();
		foreach (TKey key in array)
		{
			itemCache.Remove(key);
		}
		return list;
	}
}
