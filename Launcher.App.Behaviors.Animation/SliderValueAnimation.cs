using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace Launcher.App.Behaviors.Animation;

public static class SliderValueAnimation
{
	public static readonly DependencyProperty IsEnabledProperty;

	public static readonly DependencyProperty TargetValueProperty;

	public static readonly DependencyProperty DurationProperty;

	private static readonly DependencyProperty IsAnimatingProperty;

	private static readonly DependencyProperty IsDraggingProperty;

	private static readonly DependencyProperty AnimationVersionProperty;

	public static bool GetIsEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsEnabledProperty);
	}

	public static void SetIsEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsEnabledProperty, (object)value);
	}

	public static double GetTargetValue(DependencyObject element)
	{
		return (double)element.GetValue(TargetValueProperty);
	}

	public static void SetTargetValue(DependencyObject element, double value)
	{
		element.SetValue(TargetValueProperty, (object)value);
	}

	public static Duration GetDuration(DependencyObject element)
	{
		return (Duration)element.GetValue(DurationProperty);
	}

	public static void SetDuration(DependencyObject element, Duration value)
	{
		element.SetValue(DurationProperty, (object)value);
	}

	private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is Slider slider)
		{
			if ((bool)e.NewValue)
			{
				slider.Loaded += Slider_Loaded;
				slider.Unloaded += Slider_Unloaded;
				slider.ValueChanged += Slider_ValueChanged;
				slider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(Slider_DragStarted));
				slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(Slider_DragCompleted));
			}
			else
			{
				slider.Loaded -= Slider_Loaded;
				slider.Unloaded -= Slider_Unloaded;
				slider.ValueChanged -= Slider_ValueChanged;
				slider.RemoveHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(Slider_DragStarted));
				slider.RemoveHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(Slider_DragCompleted));
				slider.BeginAnimation(RangeBase.ValueProperty, null);
				SetIsAnimating((DependencyObject)(object)slider, value: false);
			}
		}
	}

	private static void Slider_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is Slider slider)
		{
			SetSliderValue(slider, GetTargetValue((DependencyObject)(object)slider));
		}
	}

	private static void Slider_Unloaded(object sender, RoutedEventArgs e)
	{
		if (sender is Slider slider)
		{
			slider.BeginAnimation(RangeBase.ValueProperty, null);
			SetIsAnimating((DependencyObject)(object)slider, value: false);
		}
	}

	private static void Slider_DragStarted(object sender, DragStartedEventArgs e)
	{
		if (sender is Slider slider)
		{
			SetIsDragging((DependencyObject)(object)slider, value: true);
			slider.BeginAnimation(RangeBase.ValueProperty, null);
			SetIsAnimating((DependencyObject)(object)slider, value: false);
		}
	}

	private static void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
	{
		if (sender is Slider slider)
		{
			SetIsDragging((DependencyObject)(object)slider, value: false);
			SetTargetValue((DependencyObject)(object)slider, SnapToStep(slider, slider.Value));
		}
	}

	private static void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (sender is Slider slider && GetIsEnabled((DependencyObject)(object)slider) && !GetIsAnimating((DependencyObject)(object)slider))
		{
			double value = SnapToStep(slider, e.NewValue);
			if (GetIsDragging((DependencyObject)(object)slider))
			{
				SetTargetValue((DependencyObject)(object)slider, value);
				return;
			}
			SetSliderValue(slider, e.OldValue);
			SetTargetValue((DependencyObject)(object)slider, value);
		}
	}

	private static void OnTargetValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is Slider slider && GetIsEnabled((DependencyObject)(object)slider))
		{
			double num = SnapToStep(slider, (double)e.NewValue);
			if (Math.Abs(num - (double)e.NewValue) > 0.01)
			{
				SetTargetValue((DependencyObject)(object)slider, num);
			}
			else if (!slider.IsLoaded)
			{
				SetSliderValue(slider, num);
			}
			else
			{
				AnimateToTarget(slider, num);
			}
		}
	}

	private static void AnimateToTarget(Slider slider, double targetValue)
	{
		double num = CoerceToSliderRange(slider, slider.Value);
		if (Math.Abs(num - targetValue) < 0.01)
		{
			SetSliderValue(slider, targetValue);
			return;
		}
		int animationVersion = GetAnimationVersion((DependencyObject)(object)slider) + 1;
		SetAnimationVersion((DependencyObject)(object)slider, animationVersion);
		SetIsAnimating((DependencyObject)(object)slider, value: true);
		DoubleAnimation doubleAnimation = new DoubleAnimation
		{
			From = num,
			To = targetValue,
			Duration = GetAnimationDuration(slider),
			EasingFunction = new ExponentialEase
			{
				EasingMode = EasingMode.EaseOut,
				Exponent = 6.0
			},
			FillBehavior = FillBehavior.Stop
		};
		doubleAnimation.Completed += delegate
		{
			if (GetAnimationVersion((DependencyObject)(object)slider) == animationVersion)
			{
				slider.BeginAnimation(RangeBase.ValueProperty, null);
				SetIsAnimating((DependencyObject)(object)slider, value: false);
				SetSliderValue(slider, targetValue);
			}
		};
		slider.BeginAnimation(RangeBase.ValueProperty, doubleAnimation, HandoffBehavior.SnapshotAndReplace);
	}

	private static Duration GetAnimationDuration(Slider slider)
	{
		if (!GetIsDragging((DependencyObject)(object)slider))
		{
			return GetDuration((DependencyObject)(object)slider);
		}
		Duration duration = GetDuration((DependencyObject)(object)slider);
		if (!duration.HasTimeSpan)
		{
			return duration;
		}
		return new Duration(TimeSpan.FromMilliseconds(Math.Max(80.0, duration.TimeSpan.TotalMilliseconds * 0.6)));
	}

	private static double CoerceToSliderRange(Slider slider, double value)
	{
		return Math.Clamp(value, slider.Minimum, slider.Maximum);
	}

	private static double SnapToStep(Slider slider, double value)
	{
		double num = CoerceToSliderRange(slider, value);
		double num2 = ((slider.TickFrequency > 0.0) ? slider.TickFrequency : slider.SmallChange);
		if (num2 <= 0.0 || double.IsNaN(num2) || double.IsInfinity(num2))
		{
			return num;
		}
		double value2 = slider.Minimum + Math.Round((num - slider.Minimum) / num2) * num2;
		return CoerceToSliderRange(slider, value2);
	}

	private static void SetSliderValue(Slider slider, double value)
	{
		SetIsAnimating((DependencyObject)(object)slider, value: true);
		try
		{
			slider.Value = CoerceToSliderRange(slider, value);
		}
		finally
		{
			SetIsAnimating((DependencyObject)(object)slider, value: false);
		}
	}

	private static bool GetIsAnimating(DependencyObject element)
	{
		return (bool)element.GetValue(IsAnimatingProperty);
	}

	private static void SetIsAnimating(DependencyObject element, bool value)
	{
		element.SetValue(IsAnimatingProperty, (object)value);
	}

	private static bool GetIsDragging(DependencyObject element)
	{
		return (bool)element.GetValue(IsDraggingProperty);
	}

	private static void SetIsDragging(DependencyObject element, bool value)
	{
		element.SetValue(IsDraggingProperty, (object)value);
	}

	private static int GetAnimationVersion(DependencyObject element)
	{
		return (int)element.GetValue(AnimationVersionProperty);
	}

	private static void SetAnimationVersion(DependencyObject element, int value)
	{
		element.SetValue(AnimationVersionProperty, (object)value);
	}

	static SliderValueAnimation()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Expected O, but got Unknown
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Expected O, but got Unknown
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Expected O, but got Unknown
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Expected O, but got Unknown
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Expected O, but got Unknown
		IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(SliderValueAnimation), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsEnabledChanged)));
		TargetValueProperty = DependencyProperty.RegisterAttached("TargetValue", typeof(double), typeof(SliderValueAnimation), (PropertyMetadata)(object)new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(OnTargetValueChanged)));
		DurationProperty = DependencyProperty.RegisterAttached("Duration", typeof(Duration), typeof(SliderValueAnimation), new PropertyMetadata((object)new Duration(TimeSpan.FromMilliseconds(220.0))));
		IsAnimatingProperty = DependencyProperty.RegisterAttached("IsAnimating", typeof(bool), typeof(SliderValueAnimation), new PropertyMetadata((object)false));
		IsDraggingProperty = DependencyProperty.RegisterAttached("IsDragging", typeof(bool), typeof(SliderValueAnimation), new PropertyMetadata((object)false));
		AnimationVersionProperty = DependencyProperty.RegisterAttached("AnimationVersion", typeof(int), typeof(SliderValueAnimation), new PropertyMetadata((object)0));
	}
}
