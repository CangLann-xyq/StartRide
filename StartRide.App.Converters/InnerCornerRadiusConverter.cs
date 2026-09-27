using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StartRide.App.Converters;

public sealed class InnerCornerRadiusConverter : IMultiValueConverter
{
	public static InnerCornerRadiusConverter Instance { get; } = new InnerCornerRadiusConverter();

	public static CornerRadius Deflate(CornerRadius radius, Thickness border)
	{
		return new CornerRadius(Inset(radius.TopLeft, border.Left, border.Top), Inset(radius.TopRight, border.Top, border.Right), Inset(radius.BottomRight, border.Right, border.Bottom), Inset(radius.BottomLeft, border.Bottom, border.Left));
	}

	public static CornerRadius Inflate(CornerRadius radius, Thickness border)
	{
		return new CornerRadius(Outset(radius.TopLeft, border.Left, border.Top), Outset(radius.TopRight, border.Top, border.Right), Outset(radius.BottomRight, border.Right, border.Bottom), Outset(radius.BottomLeft, border.Bottom, border.Left));
	}

	private static double Inset(double radius, double first, double second)
	{
		return Math.Max(0.0, radius - HalfAverage(first, second));
	}

	private static double Outset(double radius, double first, double second)
	{
		double num = HalfAverage(first, second);
		if (!(num <= 0.0))
		{
			return radius + num;
		}
		return radius;
	}

	private static double HalfAverage(double first, double second)
	{
		double num = (double.IsFinite(first) ? Math.Max(0.0, first) : 0.0);
		double num2 = (double.IsFinite(second) ? Math.Max(0.0, second) : 0.0);
		return (num + num2) / 4.0;
	}

	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values == null || values.Length != 2 || !(values[0] is CornerRadius radius) || !(values[1] is Thickness border))
		{
			return DependencyProperty.UnsetValue;
		}
		CornerRadius cornerRadius = ((parameter is string a && string.Equals(a, "Outer", StringComparison.OrdinalIgnoreCase)) ? Inflate(radius, border) : Deflate(radius, border));
		if (!(targetType == typeof(double)))
		{
			return cornerRadius;
		}
		return cornerRadius.TopLeft;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
