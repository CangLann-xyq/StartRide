using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using StartRide.App.Models;

namespace StartRide.App.Converters;

public sealed class FallbackPageVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return NavigationCatalog.HasDedicatedView(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
