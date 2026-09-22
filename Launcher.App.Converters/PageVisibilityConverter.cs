using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Launcher.App.Converters;

public sealed class PageVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (!string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase)) ? Visibility.Collapsed : Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
