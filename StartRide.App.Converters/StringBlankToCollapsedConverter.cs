using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StartRide.App.Converters;

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
