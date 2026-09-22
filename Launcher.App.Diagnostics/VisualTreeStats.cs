using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Launcher.App.Controls;

namespace Launcher.App.Diagnostics;

internal static class VisualTreeStats
{
	internal const int MaximumElements = 20000;

	internal static VisualTreeSize Measure(DependencyObject? root)
	{
		if (root == null)
		{
			return new VisualTreeSize(0, 0, IsTruncated: false, 0, 0, 0, 0, string.Empty, string.Empty);
		}
		int num = 0;
		int num2 = 0;
		bool isTruncated = false;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
		List<string> list = new List<string>();
		Stack<(DependencyObject, int)> stack = new Stack<(DependencyObject, int)>();
		stack.Push((root, 1));
		while (stack.Count > 0)
		{
			(DependencyObject, int) tuple = stack.Pop();
			DependencyObject item = tuple.Item1;
			int item2 = tuple.Item2;
			num++;
			if (item is UIElement { Effect: { } effect } uIElement)
			{
				num3++;
				if (effect is DropShadowEffect)
				{
					num5++;
				}
				bool isVisible = uIElement.IsVisible;
				if (isVisible)
				{
					num4++;
					string text = ((uIElement is FrameworkElement { Name: { Length: >0 } } frameworkElement) ? (((object)uIElement).GetType().Name + "#" + frameworkElement.Name) : ((object)uIElement).GetType().Name);
					list.Add(text + "(" + ((object)effect).GetType().Name + ")");
				}
				string key = ((object)effect).GetType().Name + (isVisible ? "" : "(hidden)");
				dictionary[key] = dictionary.GetValueOrDefault(key) + 1;
			}
			if (item is CardShadowChrome)
			{
				num6++;
			}
			if (item2 > num2)
			{
				num2 = item2;
			}
			if (num >= 20000)
			{
				isTruncated = true;
				break;
			}
			int childrenCount = VisualTreeHelper.GetChildrenCount(item);
			for (int i = 0; i < childrenCount; i++)
			{
				stack.Push((VisualTreeHelper.GetChild(item, i), item2 + 1));
			}
		}
		string effectBreakdown = string.Join(",", from pair in dictionary
			orderby pair.Value descending
			select $"{pair.Key}:{pair.Value}");
		return new VisualTreeSize(num, num2, isTruncated, num3, num4, num5, num6, effectBreakdown, string.Join(",", list.Take(12)));
	}
}
