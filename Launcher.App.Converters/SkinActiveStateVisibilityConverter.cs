using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Launcher.App.Converters;

public sealed class SkinActiveStateVisibilityConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values.Length < 2)
		{
			return Visibility.Collapsed;
		}
		string text = values[0]?.ToString();
		string b = values[1]?.ToString();
		return (string.IsNullOrWhiteSpace(text) || !string.Equals(text, b, StringComparison.Ordinal)) ? Visibility.Collapsed : Visibility.Visible;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
