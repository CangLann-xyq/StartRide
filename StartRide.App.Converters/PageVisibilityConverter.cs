using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StartRide.App.Converters;

public sealed class PageVisibilityConverter : IValueConverter
{

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string current = value?.ToString() ?? string.Empty;
		string wanted = parameter?.ToString() ?? string.Empty;
		if (wanted.Length == 0)
		{
			return Visibility.Collapsed;
		}
		foreach (string candidate in wanted.Split('|'))
		{
			if (string.Equals(current, candidate.Trim(), StringComparison.OrdinalIgnoreCase))
			{
				return Visibility.Visible;
			}
		}
		return Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
