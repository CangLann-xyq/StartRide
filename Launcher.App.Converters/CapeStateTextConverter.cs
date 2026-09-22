using System;
using System.Globalization;
using System.Windows.Data;
using Launcher.App.Resources;
using Launcher.Application.Accounts;

namespace Launcher.App.Converters;

public sealed class CapeStateTextConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (!(value is AccountCapeOption accountCapeOption))
		{
			return string.Empty;
		}
		if (accountCapeOption.IsNone)
		{
			return Strings.Cape_NoneState;
		}
		if (!accountCapeOption.IsActive)
		{
			return Strings.Cape_AvailableState;
		}
		return Strings.Cape_ActiveState;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
