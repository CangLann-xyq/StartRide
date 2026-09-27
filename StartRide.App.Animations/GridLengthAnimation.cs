using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace StartRide.App.Animations;

public sealed class GridLengthAnimation : AnimationTimeline
{
	public static readonly DependencyProperty FromProperty = DependencyProperty.Register("From", typeof(GridLength), typeof(GridLengthAnimation));

	public static readonly DependencyProperty ToProperty = DependencyProperty.Register("To", typeof(GridLength), typeof(GridLengthAnimation));

	public static readonly DependencyProperty EasingFunctionProperty = DependencyProperty.Register("EasingFunction", typeof(IEasingFunction), typeof(GridLengthAnimation));

	public GridLength From
	{
		get
		{
			return (GridLength)((DependencyObject)this).GetValue(FromProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(FromProperty, (object)value);
		}
	}

	public GridLength To
	{
		get
		{
			return (GridLength)((DependencyObject)this).GetValue(ToProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ToProperty, (object)value);
		}
	}

	public IEasingFunction? EasingFunction
	{
		get
		{
			return (IEasingFunction)((DependencyObject)this).GetValue(EasingFunctionProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(EasingFunctionProperty, (object)value);
		}
	}

	public override Type TargetPropertyType => typeof(GridLength);

	public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
	{
		double num = animationClock.CurrentProgress.GetValueOrDefault();
		if (EasingFunction != null)
		{
			num = EasingFunction.Ease(num);
		}
		double value = From.Value;
		double value2 = To.Value;
		return new GridLength(value + (value2 - value) * num, GridUnitType.Pixel);
	}

	protected override Freezable CreateInstanceCore()
	{
		return (Freezable)(object)new GridLengthAnimation();
	}
}
