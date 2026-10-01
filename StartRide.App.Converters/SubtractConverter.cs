using System;
using System.Globalization;
using System.Windows.Data;

namespace StartRide.App.Converters;

public sealed class SubtractConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		double input = value is double number && !double.IsNaN(number) ? number : 0d;
		double amount = 0d;
		double minimum = 0d;
		double maximum = double.PositiveInfinity;

		if (parameter is string text)
		{

		string[] parts = text.Split(',', '|');
			if (parts.Length > 0)
			{
				double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out amount);
			}
			if (parts.Length > 1)
			{
				double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out minimum);
			}
			if (parts.Length > 2)
			{
				double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out maximum);
			}
		}

		double result = input - amount;
		if (result < minimum)
		{
			result = minimum;
		}
		if (result > maximum)
		{
			result = maximum;
		}
		return result;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotSupportedException("SubtractConverter 只用于单向绑定。");
}
