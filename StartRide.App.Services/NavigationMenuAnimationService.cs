using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using StartRide.App.Animations;

namespace StartRide.App.Services;

public sealed class NavigationMenuAnimationService
{
	private const double CollapsedWidth = 0.0;

	private const double ExpandedWidth = 0.0;

	private static readonly TimeSpan WidthAnimationDuration = TimeSpan.FromMilliseconds(360.0);

	private readonly ColumnDefinition menuColumn;

	public NavigationMenuAnimationService(ColumnDefinition menuColumn)
	{
		this.menuColumn = menuColumn;
	}

	public void SetExpanded(bool isExpanded)
	{
		menuColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
		menuColumn.Width = GetWidth(isExpanded);
	}

	public void AnimateExpanded(bool isExpanded)
	{
		GridLength targetWidth = GetWidth(isExpanded);
		GridLengthAnimation gridLengthAnimation = new GridLengthAnimation
		{
			From = new GridLength(menuColumn.ActualWidth),
			To = targetWidth,
			Duration = WidthAnimationDuration,
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseInOut
			}
		};
		gridLengthAnimation.Completed += delegate
		{
			menuColumn.Width = targetWidth;
		};
		menuColumn.BeginAnimation(ColumnDefinition.WidthProperty, gridLengthAnimation);
	}

	private static GridLength GetWidth(bool isExpanded)
	{
		return new GridLength(isExpanded ? 176.0 : 62.0);
	}
}
