using System;
using System.Globalization;
using System.Windows.Data;
using StartRide.App.Resources;

namespace StartRide.App.Converters;

public sealed class UuidTextConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (!(value is string text) || string.IsNullOrWhiteSpace(text))
		{
			return Strings.Account_NoneValue;
		}
		return text;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
