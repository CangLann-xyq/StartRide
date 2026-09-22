using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Launcher.App.Behaviors;

public static class ProgressBarAnimation
{
	public static readonly DependencyProperty IsEnabledProperty;

	public static readonly DependencyProperty DurationMillisecondsProperty;

	public static readonly DependencyProperty AnimatedWidthProperty;

	private static readonly DependencyProperty AnimationVersionProperty;

	public static bool GetIsEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsEnabledProperty);
	}

	public static void SetIsEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsEnabledProperty, (object)value);
	}

	public static double GetDurationMilliseconds(DependencyObject element)
	{
		return (double)element.GetValue(DurationMillisecondsProperty);
	}

	public static void SetDurationMilliseconds(DependencyObject element, double value)
	{
		element.SetValue(DurationMillisecondsProperty, (object)value);
	}

	public static double GetAnimatedWidth(DependencyObject element)
	{
		return (double)element.GetValue(AnimatedWidthProperty);
	}

	public static void SetAnimatedWidth(DependencyObject element, double value)
	{
		element.SetValue(AnimatedWidthProperty, (object)value);
	}

	private static int GetAnimationVersion(DependencyObject element)
	{
		return (int)element.GetValue(AnimationVersionProperty);
	}

	private static void SetAnimationVersion(DependencyObject element, int value)
	{
		element.SetValue(AnimationVersionProperty, (object)value);
	}

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ProgressBar progressBar)
		{
			if ((bool)e.NewValue)
			{
				progressBar.Loaded += ProgressBar_Loaded;
				progressBar.Unloaded += ProgressBar_Unloaded;
				progressBar.SizeChanged += ProgressBar_SizeChanged;
				progressBar.ValueChanged += ProgressBar_ValueChanged;
				UpdateAnimatedWidth(progressBar, animate: false);
			}
			else
			{
				progressBar.Loaded -= ProgressBar_Loaded;
				progressBar.Unloaded -= ProgressBar_Unloaded;
				progressBar.SizeChanged -= ProgressBar_SizeChanged;
				progressBar.ValueChanged -= ProgressBar_ValueChanged;
				progressBar.BeginAnimation(AnimatedWidthProperty, null);
			}
		}
	}

	private static void ProgressBar_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is ProgressBar progressBar)
		{
			UpdateAnimatedWidth(progressBar, animate: false);
		}
	}

	private static void ProgressBar_Unloaded(object sender, RoutedEventArgs e)
	{
		if (sender is ProgressBar progressBar)
		{
			progressBar.BeginAnimation(AnimatedWidthProperty, null);
		}
	}

	private static void ProgressBar_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (sender is ProgressBar progressBar)
		{
			UpdateAnimatedWidth(progressBar, animate: false);
		}
	}

	private static void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (sender is ProgressBar progressBar)
		{
			UpdateAnimatedWidth(progressBar, animate: true);
		}
	}

	private static void UpdateAnimatedWidth(ProgressBar progressBar, bool animate)
	{
		double targetWidth = CalculateTargetWidth(progressBar);
		double animatedWidth = GetAnimatedWidth((DependencyObject)(object)progressBar);
		if (!animate || !progressBar.IsLoaded || targetWidth <= animatedWidth || Math.Abs(targetWidth - animatedWidth) < 0.5)
		{
			progressBar.BeginAnimation(AnimatedWidthProperty, null);
			SetAnimatedWidth((DependencyObject)(object)progressBar, targetWidth);
			return;
		}
		int animationVersion = GetAnimationVersion((DependencyObject)(object)progressBar) + 1;
		SetAnimationVersion((DependencyObject)(object)progressBar, animationVersion);
		double value = Math.Clamp(GetDurationMilliseconds((DependencyObject)(object)progressBar), 80.0, 900.0);
		DoubleAnimation doubleAnimation = new DoubleAnimation
		{
			From = animatedWidth,
			To = targetWidth,
			Duration = TimeSpan.FromMilliseconds(value),
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			},
			FillBehavior = FillBehavior.Stop
		};
		doubleAnimation.Completed += delegate
		{
			if (GetAnimationVersion((DependencyObject)(object)progressBar) == animationVersion)
			{
				progressBar.BeginAnimation(AnimatedWidthProperty, null);
				SetAnimatedWidth((DependencyObject)(object)progressBar, targetWidth);
			}
		};
		progressBar.BeginAnimation(AnimatedWidthProperty, doubleAnimation, HandoffBehavior.SnapshotAndReplace);
	}

	private static double CalculateTargetWidth(ProgressBar progressBar)
	{
		if (progressBar.ActualWidth <= 0.0 || double.IsNaN(progressBar.ActualWidth) || double.IsInfinity(progressBar.ActualWidth))
		{
			return 0.0;
		}
		double num = progressBar.Maximum - progressBar.Minimum;
		if (num <= 0.0 || double.IsNaN(num) || double.IsInfinity(num))
		{
			return 0.0;
		}
		double value = (progressBar.Value - progressBar.Minimum) / num;
		value = Math.Clamp(value, 0.0, 1.0);
		return progressBar.ActualWidth * value;
	}

	static ProgressBarAnimation()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Expected O, but got Unknown
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Expected O, but got Unknown
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Expected O, but got Unknown
		IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(ProgressBarAnimation), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsEnabledChanged)));
		DurationMillisecondsProperty = DependencyProperty.RegisterAttached("DurationMilliseconds", typeof(double), typeof(ProgressBarAnimation), new PropertyMetadata((object)360.0));
		AnimatedWidthProperty = DependencyProperty.RegisterAttached("AnimatedWidth", typeof(double), typeof(ProgressBarAnimation), new PropertyMetadata((object)0.0));
		AnimationVersionProperty = DependencyProperty.RegisterAttached("AnimationVersion", typeof(int), typeof(ProgressBarAnimation), new PropertyMetadata((object)0));
	}
}
