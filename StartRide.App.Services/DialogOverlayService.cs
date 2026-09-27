using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using StartRide.App.Controls;

namespace StartRide.App.Services;

public sealed class DialogOverlayService
{
	private const double SizeAnimationThreshold = 1.0;

	private static readonly Duration FadeInDuration = TimeSpan.FromMilliseconds(160.0);

	private static readonly Duration FadeOutDuration = TimeSpan.FromMilliseconds(180.0);

	private static readonly Duration SizeTransitionDuration = TimeSpan.FromMilliseconds(240.0);

	private readonly Window owner;

	private bool isSizeAnimating;

	public bool IsSizeAnimating => isSizeAnimating;

	public DialogOverlayService(Window owner)
	{
		this.owner = owner;
	}

	public void AnimateSizeChange(DialogHost host, double previousHeight)
	{
		AnimateSizeChange(host.SurfaceBorder, previousHeight);
	}

	public void AnimateSizeChange(Border dialog, double previousHeight)
	{
		dialog.BeginAnimation(FrameworkElement.HeightProperty, null);
		dialog.Height = double.NaN;
		isSizeAnimating = true;
		owner.UpdateLayout();
		double actualHeight = dialog.ActualHeight;
		if (previousHeight <= 0.0 || actualHeight <= 0.0 || Math.Abs(previousHeight - actualHeight) <= 1.0)
		{
			dialog.Height = double.NaN;
			isSizeAnimating = false;
			return;
		}
		dialog.Height = previousHeight;
		dialog.UpdateLayout();
		DoubleAnimation doubleAnimation = new DoubleAnimation
		{
			From = previousHeight,
			To = actualHeight,
			Duration = SizeTransitionDuration,
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseInOut
			},
			FillBehavior = FillBehavior.Stop
		};
		doubleAnimation.Completed += delegate
		{
			dialog.Height = double.NaN;
			isSizeAnimating = false;
		};
		dialog.BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation);
	}

	public void Show(DialogHost host)
	{
		Show(host.OverlayRoot);
	}

	public void Show(Grid overlay)
	{
		overlay.BeginAnimation(UIElement.OpacityProperty, null);
		overlay.Visibility = Visibility.Visible;
		overlay.Opacity = 0.0;
		DoubleAnimation animation = new DoubleAnimation
		{
			From = 0.0,
			To = 1.0,
			Duration = FadeInDuration,
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		overlay.BeginAnimation(UIElement.OpacityProperty, animation);
	}

	public void Hide(DialogHost host, Action? completed = null)
	{
		Hide(host.OverlayRoot, completed);
	}

	public void Hide(Grid overlay, Action? completed = null)
	{
		double opacity = overlay.Opacity;
		overlay.BeginAnimation(UIElement.OpacityProperty, null);
		overlay.Opacity = opacity;
		if (opacity <= 0.0)
		{
			overlay.Visibility = Visibility.Collapsed;
			completed?.Invoke();
			return;
		}
		DoubleAnimation doubleAnimation = new DoubleAnimation
		{
			From = opacity,
			To = 0.0,
			Duration = FadeOutDuration,
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			},
			FillBehavior = FillBehavior.Stop
		};
		doubleAnimation.Completed += delegate
		{
			overlay.Opacity = 0.0;
			overlay.Visibility = Visibility.Collapsed;
			completed?.Invoke();
		};
		overlay.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	public void Prewarm(DialogHost host)
	{
		Prewarm(host.OverlayRoot, host.SurfaceBorder);
	}

	public void Prewarm(Grid overlay, Border dialog)
	{
		Visibility visibility = overlay.Visibility;
		double opacity = overlay.Opacity;
		overlay.BeginAnimation(UIElement.OpacityProperty, null);
		overlay.Visibility = Visibility.Hidden;
		overlay.Opacity = 0.0;
		dialog.ApplyTemplate();
		dialog.UpdateLayout();
		overlay.Visibility = visibility;
		overlay.Opacity = opacity;
	}
}
