using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace StartRide.App.Behaviors.Animation;

public static class AnimatedCollapse
{
	public static readonly DependencyProperty IsExpandedProperty;

	public static readonly DependencyProperty DurationProperty;

	private static readonly DependencyProperty OriginalHeightProperty;

	private static readonly DependencyProperty OriginalMarginProperty;

	private static readonly DependencyProperty HasOriginalHeightProperty;

	public static bool GetIsExpanded(DependencyObject element)
	{
		return (bool)element.GetValue(IsExpandedProperty);
	}

	public static void SetIsExpanded(DependencyObject element, bool value)
	{
		element.SetValue(IsExpandedProperty, (object)value);
	}

	public static Duration GetDuration(DependencyObject element)
	{
		return (Duration)element.GetValue(DurationProperty);
	}

	public static void SetDuration(DependencyObject element, Duration value)
	{
		element.SetValue(DurationProperty, (object)value);
	}

	private static void OnIsExpandedChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is FrameworkElement frameworkElement)
		{
			CaptureOriginalHeight(frameworkElement);
			bool isExpanded = (bool)e.NewValue;
			if (!frameworkElement.IsLoaded)
			{
				frameworkElement.Loaded -= Element_Loaded;
				frameworkElement.Loaded += Element_Loaded;
				ApplyInstantState(frameworkElement, isExpanded);
			}
			else
			{
				Animate(frameworkElement, isExpanded);
			}
		}
	}

	private static void Element_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement frameworkElement)
		{
			frameworkElement.Loaded -= Element_Loaded;
			ApplyInstantState(frameworkElement, GetIsExpanded((DependencyObject)(object)frameworkElement));
		}
	}

	private static void CaptureOriginalHeight(FrameworkElement element)
	{
		if (!(bool)((DependencyObject)element).GetValue(HasOriginalHeightProperty))
		{
			((DependencyObject)element).SetValue(OriginalHeightProperty, (object)element.Height);
			((DependencyObject)element).SetValue(OriginalMarginProperty, (object)element.Margin);
			((DependencyObject)element).SetValue(HasOriginalHeightProperty, (object)true);
		}
	}

	private static void ApplyInstantState(FrameworkElement element, bool isExpanded)
	{
		element.BeginAnimation(FrameworkElement.HeightProperty, null);
		element.BeginAnimation(UIElement.OpacityProperty, null);
		element.BeginAnimation(FrameworkElement.MarginProperty, null);
		if (isExpanded)
		{
			RestoreOriginalHeight(element);
			RestoreOriginalMargin(element);
			element.Visibility = Visibility.Visible;
			element.Opacity = 1.0;
		}
		else
		{
			element.Height = 0.0;
			element.Margin = default(Thickness);
			element.Opacity = 0.0;
			element.Visibility = Visibility.Collapsed;
		}
	}

	private static void Animate(FrameworkElement element, bool isExpanded)
	{
		element.BeginAnimation(FrameworkElement.HeightProperty, null);
		element.BeginAnimation(UIElement.OpacityProperty, null);
		element.BeginAnimation(FrameworkElement.MarginProperty, null);
		if (isExpanded)
		{
			AnimateExpand(element);
		}
		else
		{
			AnimateCollapse(element);
		}
	}

	private static void AnimateExpand(FrameworkElement element)
	{
		element.Visibility = Visibility.Visible;
		element.Opacity = 0.0;
		element.Margin = default(Thickness);
		double to = MeasureExpandedHeight(element);
		element.Height = 0.0;
		Thickness to2 = (Thickness)((DependencyObject)element).GetValue(OriginalMarginProperty);
		Duration duration = GetDuration((DependencyObject)(object)element);
		DoubleAnimation doubleAnimation = CreateHeightAnimation(0.0, to, duration);
		doubleAnimation.Completed += delegate
		{
			if (GetIsExpanded((DependencyObject)(object)element))
			{
				element.BeginAnimation(FrameworkElement.HeightProperty, null);
				RestoreOriginalHeight(element);
				RestoreOriginalMargin(element);
				element.Opacity = 1.0;
			}
		};
		element.BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation);
		element.BeginAnimation(UIElement.OpacityProperty, CreateOpacityAnimation(0.0, 1.0, duration));
		element.BeginAnimation(FrameworkElement.MarginProperty, CreateMarginAnimation(default(Thickness), to2, duration));
	}

	private static void AnimateCollapse(FrameworkElement element)
	{
		double num = element.ActualHeight;
		if (num <= 0.0)
		{
			num = MeasureExpandedHeight(element);
		}
		element.Height = num;
		element.Visibility = Visibility.Visible;
		Thickness margin = element.Margin;
		Duration duration = GetDuration((DependencyObject)(object)element);
		DoubleAnimation doubleAnimation = CreateHeightAnimation(num, 0.0, duration);
		doubleAnimation.Completed += delegate
		{
			if (!GetIsExpanded((DependencyObject)(object)element))
			{
				element.Visibility = Visibility.Collapsed;
				element.Height = 0.0;
				element.Margin = default(Thickness);
				element.Opacity = 0.0;
			}
		};
		element.BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation);
		element.BeginAnimation(UIElement.OpacityProperty, CreateOpacityAnimation(element.Opacity, 0.0, duration));
		element.BeginAnimation(FrameworkElement.MarginProperty, CreateMarginAnimation(margin, default(Thickness), duration));
	}

	private static double MeasureExpandedHeight(FrameworkElement element)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		double num = (double)((DependencyObject)element).GetValue(OriginalHeightProperty);
		if (!double.IsNaN(num))
		{
			return num;
		}
		element.Height = double.NaN;
		element.Measure(new Size((element.ActualWidth > 0.0) ? element.ActualWidth : double.PositiveInfinity, double.PositiveInfinity));
		Size desiredSize = element.DesiredSize;
		return desiredSize.Height;
	}

	private static DoubleAnimation CreateHeightAnimation(double from, double to, Duration duration)
	{
		return new DoubleAnimation(from, to, duration)
		{
			EasingFunction = CreateEasingFunction()
		};
	}

	private static DoubleAnimation CreateOpacityAnimation(double from, double to, Duration duration)
	{
		return new DoubleAnimation(from, to, duration)
		{
			EasingFunction = CreateEasingFunction()
		};
	}

	private static ThicknessAnimation CreateMarginAnimation(Thickness from, Thickness to, Duration duration)
	{
		return new ThicknessAnimation(from, to, duration)
		{
			EasingFunction = CreateEasingFunction()
		};
	}

	private static IEasingFunction CreateEasingFunction()
	{
		return new ExponentialEase
		{
			EasingMode = EasingMode.EaseOut,
			Exponent = 6.0
		};
	}

	private static void RestoreOriginalHeight(FrameworkElement element)
	{
		double height = (double)((DependencyObject)element).GetValue(OriginalHeightProperty);
		element.Height = height;
	}

	private static void RestoreOriginalMargin(FrameworkElement element)
	{
		Thickness margin = (Thickness)((DependencyObject)element).GetValue(OriginalMarginProperty);
		element.Margin = margin;
	}

	static AnimatedCollapse()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Expected O, but got Unknown
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Expected O, but got Unknown
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Expected O, but got Unknown
		IsExpandedProperty = DependencyProperty.RegisterAttached("IsExpanded", typeof(bool), typeof(AnimatedCollapse), new PropertyMetadata((object)true, new PropertyChangedCallback(OnIsExpandedChanged)));
		DurationProperty = DependencyProperty.RegisterAttached("Duration", typeof(Duration), typeof(AnimatedCollapse), new PropertyMetadata((object)new Duration(TimeSpan.FromMilliseconds(180.0))));
		OriginalHeightProperty = DependencyProperty.RegisterAttached("OriginalHeight", typeof(double), typeof(AnimatedCollapse), new PropertyMetadata((object)double.NaN));
		OriginalMarginProperty = DependencyProperty.RegisterAttached("OriginalMargin", typeof(Thickness), typeof(AnimatedCollapse), new PropertyMetadata((object)default(Thickness)));
		HasOriginalHeightProperty = DependencyProperty.RegisterAttached("HasOriginalHeight", typeof(bool), typeof(AnimatedCollapse), new PropertyMetadata((object)false));
	}
}
