using System;
using System.Windows;
using System.Windows.Media;

namespace Launcher.App.Utilities;

public static class VisualTreeSearch
{
	public static T? FindDescendant<T>(DependencyObject root, Func<T, bool> predicate) where T : DependencyObject
	{
		int childrenCount = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			T val = (T)(object)((child is T) ? child : null);
			if (val != null && predicate(val))
			{
				return val;
			}
			T val2 = FindDescendant(child, predicate);
			if (val2 != null)
			{
				return val2;
			}
		}
		return default(T);
	}
}
