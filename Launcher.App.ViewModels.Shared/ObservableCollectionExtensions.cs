using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Launcher.App.ViewModels.Shared;

internal static class ObservableCollectionExtensions
{
	public static void ReplaceWith<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
	{
		collection.Clear();
		foreach (T item in items)
		{
			collection.Add(item);
		}
	}

	public static bool ReplaceWithIfChanged<T>(this ObservableCollection<T> collection, IReadOnlyList<T> items)
	{
		if (collection.Count == items.Count)
		{
			bool flag = true;
			for (int i = 0; i < items.Count; i++)
			{
				if (!EqualityComparer<T>.Default.Equals(collection[i], items[i]))
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				return false;
			}
		}
		collection.ReplaceWith(items);
		return true;
	}

	public static bool SynchronizeByKey<T, TKey>(this ObservableCollection<T> collection, IReadOnlyList<T> items, Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
	{
		if (comparer == null)
		{
			comparer = EqualityComparer<TKey>.Default;
		}
		bool result = false;
		for (int i = 0; i < items.Count; i++)
		{
			T val = items[i];
			TKey y = keySelector(val);
			if (i < collection.Count && comparer.Equals(keySelector(collection[i]), y))
			{
				if ((object)collection[i] != (object)val)
				{
					collection[i] = val;
					result = true;
				}
				continue;
			}
			int num = -1;
			for (int j = i + 1; j < collection.Count; j++)
			{
				if (comparer.Equals(keySelector(collection[j]), y))
				{
					num = j;
					break;
				}
			}
			if (num >= 0)
			{
				collection.Move(num, i);
				result = true;
				if ((object)collection[i] != (object)val)
				{
					collection[i] = val;
				}
			}
			else
			{
				collection.Insert(i, val);
				result = true;
			}
		}
		while (collection.Count > items.Count)
		{
			collection.RemoveAt(collection.Count - 1);
			result = true;
		}
		return result;
	}
}
