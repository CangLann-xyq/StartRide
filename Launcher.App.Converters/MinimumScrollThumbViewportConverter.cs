using System;
using System.Globalization;
using System.Windows.Data;

namespace Launcher.App.Converters;

public sealed class MinimumScrollThumbViewportConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (!TryGetDouble(values, 0, out var value) || !TryGetDouble(values, 1, out var value2) || !TryGetDouble(values, 2, out var value3))
		{
			return Binding.DoNothing;
		}
		if (value <= 0.0 || value2 <= 0.0 || value3 <= 0.0)
		{
			return value;
		}
		double num = ParseMinimumThumbLength(parameter, culture);
		if (num <= 0.0 || value3 <= num)
		{
			return value;
		}
		if (value / (value2 + value) * value3 >= num)
		{
			return value;
		}
		return num * value2 / (value3 - num);
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}

	private static bool TryGetDouble(object[] values, int index, out double value)
	{
		value = 0.0;
		if (values.Length <= index || !(values[index] is double num))
		{
			return false;
		}
		if (double.IsNaN(num) || double.IsInfinity(num))
		{
			return false;
		}
		value = num;
		return true;
	}

	private static double ParseMinimumThumbLength(object parameter, CultureInfo culture)
	{
		if (!(parameter is double result))
		{
			if (!(parameter is int num))
			{
				if (parameter is string s)
				{
					if (double.TryParse(s, NumberStyles.Float, culture, out var result2))
					{
						return result2;
					}
					if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result3))
					{
						return result3;
					}
				}
				return 58.0;
			}
			return num;
		}
		return result;
	}
}
