using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace StartRide.App.Behaviors;

public static class VerticalEdgeOpacityMask
{
	public static readonly DependencyProperty IsEnabledProperty;

	public static readonly DependencyProperty TopFadeLengthProperty;

	public static readonly DependencyProperty BottomFadeLengthProperty;

	public static readonly DependencyProperty TopIntermediateLengthProperty;

	public static readonly DependencyProperty TopIntermediateOpacityProperty;

	public static readonly DependencyProperty TopPlateauLengthProperty;

	public static readonly DependencyProperty MinimumOpacityProperty;

	public static readonly DependencyProperty TopMinimumOpacityProperty;

	public static bool GetIsEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsEnabledProperty);
	}

	public static void SetIsEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsEnabledProperty, (object)value);
	}

	public static double GetTopFadeLength(DependencyObject element)
	{
		return (double)element.GetValue(TopFadeLengthProperty);
	}

	public static void SetTopFadeLength(DependencyObject element, double value)
	{
		element.SetValue(TopFadeLengthProperty, (object)value);
	}

	public static double GetBottomFadeLength(DependencyObject element)
	{
		return (double)element.GetValue(BottomFadeLengthProperty);
	}

	public static void SetBottomFadeLength(DependencyObject element, double value)
	{
		element.SetValue(BottomFadeLengthProperty, (object)value);
	}

	public static double GetTopIntermediateLength(DependencyObject element)
	{
		return (double)element.GetValue(TopIntermediateLengthProperty);
	}

	public static void SetTopIntermediateLength(DependencyObject element, double value)
	{
		element.SetValue(TopIntermediateLengthProperty, (object)value);
	}

	public static double GetTopIntermediateOpacity(DependencyObject element)
	{
		return (double)element.GetValue(TopIntermediateOpacityProperty);
	}

	public static void SetTopIntermediateOpacity(DependencyObject element, double value)
	{
		element.SetValue(TopIntermediateOpacityProperty, (object)value);
	}

	public static double GetTopPlateauLength(DependencyObject element)
	{
		return (double)element.GetValue(TopPlateauLengthProperty);
	}

	public static void SetTopPlateauLength(DependencyObject element, double value)
	{
		element.SetValue(TopPlateauLengthProperty, (object)value);
	}

	public static double GetMinimumOpacity(DependencyObject element)
	{
		return (double)element.GetValue(MinimumOpacityProperty);
	}

	public static void SetMinimumOpacity(DependencyObject element, double value)
	{
		element.SetValue(MinimumOpacityProperty, (object)value);
	}

	public static double GetTopMinimumOpacity(DependencyObject element)
	{
		return (double)element.GetValue(TopMinimumOpacityProperty);
	}

	public static void SetTopMinimumOpacity(DependencyObject element, double value)
	{
		element.SetValue(TopMinimumOpacityProperty, (object)value);
	}

	private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is FrameworkElement frameworkElement)
		{
			frameworkElement.SizeChanged -= Element_SizeChanged;
			if ((bool)e.NewValue)
			{
				frameworkElement.SizeChanged += Element_SizeChanged;
				ApplyMask(frameworkElement);
			}
			else
			{
				frameworkElement.OpacityMask = null;
			}
		}
	}

	private static void OnFadePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is FrameworkElement element && GetIsEnabled((DependencyObject)(object)element))
		{
			ApplyMask(element);
		}
	}

	private static void Element_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (sender is FrameworkElement element)
		{
			ApplyMask(element);
		}
	}

	private static void ApplyMask(FrameworkElement element)
	{
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		double actualHeight = element.ActualHeight;
		if (actualHeight <= 0.0)
		{
			element.OpacityMask = null;
			return;
		}
		double num = Math.Clamp(GetTopFadeLength((DependencyObject)(object)element), 0.0, actualHeight);
		double num2 = Math.Clamp(GetBottomFadeLength((DependencyObject)(object)element), 0.0, actualHeight);
		double num3 = Math.Clamp(GetTopIntermediateLength((DependencyObject)(object)element), 0.0, num);
		double num4 = Math.Clamp(GetTopPlateauLength((DependencyObject)(object)element), 0.0, num);
		double num5 = num + num2;
		if (num5 > actualHeight && num5 > 0.0)
		{
			double num6 = actualHeight / num5;
			num *= num6;
			num2 *= num6;
			num3 = Math.Min(num3 * num6, num);
			num4 = Math.Min(num4 * num6, num);
		}
		if (num <= 0.0 && num2 <= 0.0)
		{
			element.OpacityMask = null;
			return;
		}
		double num7 = Math.Clamp(GetMinimumOpacity((DependencyObject)(object)element), 0.0, 1.0);
		double topMinimumOpacity = GetTopMinimumOpacity((DependencyObject)(object)element);
		double num8 = (double.IsFinite(topMinimumOpacity) ? Math.Clamp(topMinimumOpacity, 0.0, 1.0) : num7);
		double value = Math.Clamp(GetTopIntermediateOpacity((DependencyObject)(object)element), num8, 1.0);
		double offset = num3 / actualHeight;
		double offset2 = num4 / actualHeight;
		double offset3 = num / actualHeight;
		double offset4 = 1.0 - num2 / actualHeight;
		Color color = Color.FromArgb(ToByte(num8), 0, 0, 0);
		Color color2 = Color.FromArgb(ToByte(num7), 0, 0, 0);
		Color color3 = Color.FromArgb(ToByte(value), 0, 0, 0);
		Color color4 = Color.FromArgb(byte.MaxValue, 0, 0, 0);
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush
		{
			StartPoint = new Point(0.0, 0.0),
			EndPoint = new Point(0.0, 1.0)
		};
		List<GradientStop> list = new List<GradientStop>
		{
			new GradientStop((num > 0.0) ? color : color4, 0.0)
		};
		if (num4 > 0.0)
		{
			list.Add(new GradientStop(color, offset2));
		}
		if (num3 > 0.0 && num3 < num)
		{
			list.Add(new GradientStop(color3, offset));
		}
		list.Add(new GradientStop(color4, offset3));
		list.Add(new GradientStop(color4, offset4));
		list.Add(new GradientStop((num2 > 0.0) ? color2 : color4, 1.0));
		foreach (GradientStop item in list.OrderBy((GradientStop stop) => stop.Offset))
		{
			linearGradientBrush.GradientStops.Add(item);
		}
		((Freezable)linearGradientBrush).Freeze();
		element.OpacityMask = linearGradientBrush;
	}

	private static byte ToByte(double value)
	{
		return (byte)Math.Round(value * 255.0);
	}

	static VerticalEdgeOpacityMask()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected O, but got Unknown
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Expected O, but got Unknown
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Expected O, but got Unknown
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Expected O, but got Unknown
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Expected O, but got Unknown
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Expected O, but got Unknown
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Expected O, but got Unknown
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Expected O, but got Unknown
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Expected O, but got Unknown
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Expected O, but got Unknown
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Expected O, but got Unknown
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Expected O, but got Unknown
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Expected O, but got Unknown
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Expected O, but got Unknown
		IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(VerticalEdgeOpacityMask), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsEnabledChanged)));
		TopFadeLengthProperty = DependencyProperty.RegisterAttached("TopFadeLength", typeof(double), typeof(VerticalEdgeOpacityMask), new PropertyMetadata((object)0.0, new PropertyChangedCallback(OnFadePropertyChanged)));
		BottomFadeLengthProperty = DependencyProperty.RegisterAttached("BottomFadeLength", typeof(double), typeof(VerticalEdgeOpacityMask), new PropertyMetadata((object)0.0, new PropertyChangedCallback(OnFadePropertyChanged)));
		TopIntermediateLengthProperty = DependencyProperty.RegisterAttached("TopIntermediateLength", typeof(double), typeof(VerticalEdgeOpacityMask), new PropertyMetadata((object)0.0, new PropertyChangedCallback(OnFadePropertyChanged)));
		TopIntermediateOpacityProperty = DependencyProperty.RegisterAttached("TopIntermediateOpacity", typeof(double), typeof(VerticalEdgeOpacityMask), new PropertyMetadata((object)1.0, new PropertyChangedCallback(OnFadePropertyChanged)));
		TopPlateauLengthProperty = DependencyProperty.RegisterAttached("TopPlateauLength", typeof(double), typeof(VerticalEdgeOpacityMask), new PropertyMetadata((object)0.0, new PropertyChangedCallback(OnFadePropertyChanged)));
		MinimumOpacityProperty = DependencyProperty.RegisterAttached("MinimumOpacity", typeof(double), typeof(VerticalEdgeOpacityMask), new PropertyMetadata((object)0.0, new PropertyChangedCallback(OnFadePropertyChanged)));
		TopMinimumOpacityProperty = DependencyProperty.RegisterAttached("TopMinimumOpacity", typeof(double), typeof(VerticalEdgeOpacityMask), new PropertyMetadata((object)double.NaN, new PropertyChangedCallback(OnFadePropertyChanged)));
	}
}
