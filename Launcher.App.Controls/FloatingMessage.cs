using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Launcher.App.Controls;

public partial class FloatingMessage : UserControl, IComponentConnector
{
	private static readonly Duration FadeInDuration;

	private static readonly Duration FadeOutDuration;

	public static readonly DependencyProperty MessageProperty;

	public static readonly DependencyProperty IsOpenProperty;

	public string Message
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(MessageProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(MessageProperty, (object)value);
		}
	}

	public bool IsOpen
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsOpenProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsOpenProperty, (object)value);
		}
	}

	public FloatingMessage()
	{
		InitializeComponent();
	}

	private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is FloatingMessage floatingMessage)
		{
			floatingMessage.SetOpenState((bool)e.NewValue);
		}
	}

	private void SetOpenState(bool isOpen)
	{
		BeginAnimation(UIElement.OpacityProperty, null);
		MessageOffset.BeginAnimation(TranslateTransform.YProperty, null);
		if (isOpen)
		{
			base.Visibility = Visibility.Visible;
			AnimateOpacity(base.Opacity, 1.0, FadeInDuration);
			AnimateOffset(MessageOffset.Y, 0.0, FadeInDuration);
			base.Opacity = 1.0;
			MessageOffset.Y = 0.0;
		}
		else if (base.Visibility == Visibility.Visible)
		{
			DoubleAnimation doubleAnimation = CreateAnimation(base.Opacity, 0.0, FadeOutDuration);
			doubleAnimation.Completed += delegate
			{
				base.Visibility = Visibility.Collapsed;
				base.Opacity = 0.0;
				MessageOffset.Y = -10.0;
			};
			BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
			AnimateOffset(MessageOffset.Y, -10.0, FadeOutDuration);
		}
	}

	private void AnimateOpacity(double from, double to, Duration duration)
	{
		BeginAnimation(UIElement.OpacityProperty, CreateAnimation(from, to, duration));
	}

	private void AnimateOffset(double from, double to, Duration duration)
	{
		MessageOffset.BeginAnimation(TranslateTransform.YProperty, CreateAnimation(from, to, duration));
	}

	private static DoubleAnimation CreateAnimation(double from, double to, Duration duration)
	{
		return new DoubleAnimation(from, to, duration)
		{
			FillBehavior = FillBehavior.Stop,
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
	}

	static FloatingMessage()
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Expected O, but got Unknown
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Expected O, but got Unknown
		FadeInDuration = TimeSpan.FromMilliseconds(140.0);
		FadeOutDuration = TimeSpan.FromMilliseconds(190.0);
		MessageProperty = DependencyProperty.Register("Message", typeof(string), typeof(FloatingMessage), new PropertyMetadata((object)string.Empty));
		IsOpenProperty = DependencyProperty.Register("IsOpen", typeof(bool), typeof(FloatingMessage), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsOpenChanged)));
	}
}
