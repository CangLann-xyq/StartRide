using System;
using System.Globalization;
using System.Windows.Data;
using Launcher.App.Controls;

namespace Launcher.App.Converters;

public sealed class IconSourceImageConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return IconSourceImageLoader.TryLoad(value);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
