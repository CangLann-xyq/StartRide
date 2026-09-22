using System;
using System.Globalization;
using System.Windows.Data;
using Launcher.App.Utilities;
using Launcher.Application.Accounts;

namespace Launcher.App.Converters;

public sealed class CapeDisplayNameConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (!(value is AccountCapeOption cape))
		{
			return string.Empty;
		}
		return AccountCapeTextProvider.GetDisplayName(cape);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
