using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Launcher.App.Converters;

/// <summary>字符串为 null/空白时折叠元素（用于可选的版本、评分等标签）。</summary>
public sealed class StringBlankToCollapsedConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
