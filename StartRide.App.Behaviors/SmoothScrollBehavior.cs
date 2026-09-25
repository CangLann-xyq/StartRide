using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StartRide.App.Controls;
using StartRide.App.Diagnostics;

namespace StartRide.App.Behaviors;

public static class SmoothScrollBehavior
{
	private sealed class ScrollMomentum
	{
		internal List<ScrollImpulse> Impulses { get; } = new List<ScrollImpulse>();

		internal double Offset { get; set; }

		internal bool IsClockRunning { get; set; }
	}

	private sealed class ScrollImpulse
	{
		internal double TotalDelta { get; init; }

		internal long StartedAt { get; init; }

		internal double DurationMilliseconds { get; init; }

		internal double AppliedProgress { get; set; }
	}

	private const double EasingExponent = 3.0;

	private const double MinimumFrameDelta = 0.001;

	public static readonly DependencyProperty IsEnabledProperty;

	public static readonly DependencyProperty ScrollAmountProperty;

	public static readonly DependencyProperty AllowContentScrollProperty;

	public static readonly DependencyProperty WheelAnimationDurationMillisecondsProperty;

	private static readonly DependencyProperty MomentumClockProperty;

	private static readonly DependencyProperty MomentumProperty;

	private static readonly DependencyProperty IsInternalScrollUpdateProperty;

	private static readonly DependencyProperty IsAnimatingProperty;

	private static readonly DependencyProperty AnimationVersionProperty;

	private static readonly DependencyProperty InteractionScopeProperty;

	public static bool GetIsEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsEnabledProperty);
	}

	public static void SetIsEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsEnabledProperty, (object)value);
	}

	public static double GetScrollAmount(DependencyObject element)
	{
		return (double)element.GetValue(ScrollAmountProperty);
	}

	public static void SetScrollAmount(DependencyObject element, double value)
	{
		element.SetValue(ScrollAmountProperty, (object)value);
	}

	public static bool GetAllowContentScroll(DependencyObject element)
	{
		return (bool)element.GetValue(AllowContentScrollProperty);
	}

	public static void SetAllowContentScroll(DependencyObject element, bool value)
	{
		element.SetValue(AllowContentScrollProperty, (object)value);
	}

	public static double GetWheelAnimationDurationMilliseconds(DependencyObject element)
	{
		return (double)element.GetValue(WheelAnimationDurationMillisecondsProperty);
	}

	public static void SetWheelAnimationDurationMilliseconds(DependencyObject element, double value)
	{
		element.SetValue(WheelAnimationDurationMillisecondsProperty, (object)value);
	}

	public static void CancelAnimation(ScrollViewer scrollViewer)
	{
		ReleaseInteractionScope(scrollViewer);
		if (((DependencyObject)scrollViewer).GetValue(MomentumProperty) is ScrollMomentum scrollMomentum)
		{
			scrollMomentum.Impulses.Clear();
			scrollMomentum.Offset = scrollViewer.VerticalOffset;
			ReleaseRenderingHook(scrollViewer, scrollMomentum);
		}
		SetIsAnimating((DependencyObject)(object)scrollViewer, value: false);
	}

	public static bool CancelAnimationFromDescendant(DependencyObject root)
	{
		ScrollViewer scrollViewer = FindDescendant<ScrollViewer>(root);
		if (scrollViewer == null)
		{
			return false;
		}
		CancelAnimation(scrollViewer);
		return true;
	}

	private static bool GetIsInternalScrollUpdate(DependencyObject element)
	{
		return (bool)element.GetValue(IsInternalScrollUpdateProperty);
	}

	private static void SetIsInternalScrollUpdate(DependencyObject element, bool value)
	{
		element.SetValue(IsInternalScrollUpdateProperty, (object)value);
	}

	private static bool GetIsAnimating(DependencyObject element)
	{
		return (bool)element.GetValue(IsAnimatingProperty);
	}

	private static void SetIsAnimating(DependencyObject element, bool value)
	{
		element.SetValue(IsAnimatingProperty, (object)value);
	}

	private static void BeginScrollInteractionScope(ScrollViewer scrollViewer)
	{
		if (((DependencyObject)scrollViewer).GetValue(InteractionScopeProperty) is UiInteractionScope)
		{
			return;
		}
		string detail = ResolveScrollDetail(scrollViewer);
		UiPerformanceLog.LogScrollSurface(scrollViewer, detail);
		UiInteractionScope uiInteractionScope = UiPerformanceLog.BeginInteraction("Scroll", detail, scrollViewer);
		uiInteractionScope.RenderPath = "Live";
		uiInteractionScope.SurfaceWidth = scrollViewer.ViewportWidth;
		uiInteractionScope.SurfaceHeight = scrollViewer.ViewportHeight;
		BackdropBlurRefreshCoordinator coordinator = BackdropBlurRefreshCoordinator.TryGet(scrollViewer);
		if (coordinator != null)
		{
			uiInteractionScope.BackdropControlCount = coordinator.GetScrollViewerControlCount(scrollViewer);
			uiInteractionScope.BackdropCounterReader = () => (Batches: coordinator.TotalBatchCount, Refreshes: coordinator.TotalRefreshCount);
		}
		uiInteractionScope.HasAncestorBitmapCache = HasBitmapCache(scrollViewer);
		uiInteractionScope.HasOpacityMask = HasOpacityMask(scrollViewer);
		uiInteractionScope.ScrollOffsetReader = () => scrollViewer.VerticalOffset;
		uiInteractionScope.CaptureBackdropBaseline();
		((DependencyObject)scrollViewer).SetValue(InteractionScopeProperty, (object)uiInteractionScope);
	}

	private static bool HasBitmapCache(ScrollViewer scrollViewer)
	{
		if (scrollViewer.Content is UIElement uIElement && uIElement.CacheMode is BitmapCache)
		{
			return true;
		}
		return HasAncestor((DependencyObject)(object)scrollViewer, (UIElement element) => element.CacheMode is BitmapCache);
	}

	private static bool HasOpacityMask(ScrollViewer scrollViewer)
	{
		return HasAncestor((DependencyObject)(object)scrollViewer, (UIElement element) => element.OpacityMask != null);
	}

	private static bool HasAncestor(DependencyObject element, Func<UIElement, bool> predicate)
	{
		for (DependencyObject val = element; val != null; val = VisualTreeHelper.GetParent(val))
		{
			if (val is UIElement arg && predicate(arg))
			{
				return true;
			}
		}
		return false;
	}

	private static void ReleaseInteractionScope(ScrollViewer scrollViewer)
	{
		if (((DependencyObject)scrollViewer).GetValue(InteractionScopeProperty) is UiInteractionScope uiInteractionScope)
		{
			((DependencyObject)scrollViewer).ClearValue(InteractionScopeProperty);
			uiInteractionScope.Dispose();
		}
	}

	private static string ResolveScrollDetail(ScrollViewer scrollViewer)
	{
		for (DependencyObject val = (DependencyObject)(object)scrollViewer; val != null; val = VisualTreeHelper.GetParent(val))
		{
			if (val is FrameworkElement { Name: { Length: >0 } } frameworkElement)
			{
				return frameworkElement.Name;
			}
		}
		return ((object)scrollViewer).GetType().Name;
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
		if (!(d is ScrollViewer scrollViewer))
		{
			UpdateDescendantScrollHost(d, (bool)e.NewValue);
		}
		else if ((bool)e.NewValue)
		{
			scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
			scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
			scrollViewer.Unloaded += ScrollViewer_Unloaded;
			EnsureMomentum(scrollViewer).Offset = scrollViewer.VerticalOffset;
		}
		else
		{
			scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
			scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
			scrollViewer.Unloaded -= ScrollViewer_Unloaded;
			CancelAnimation(scrollViewer);
		}
	}

	private static void UpdateDescendantScrollHost(DependencyObject d, bool isEnabled)
	{
		if (d is FrameworkElement frameworkElement)
		{
			if (isEnabled)
			{
				frameworkElement.PreviewMouseWheel += DescendantScrollHost_PreviewMouseWheel;
				frameworkElement.Unloaded += DescendantScrollHost_Unloaded;
			}
			else
			{
				frameworkElement.PreviewMouseWheel -= DescendantScrollHost_PreviewMouseWheel;
				frameworkElement.Unloaded -= DescendantScrollHost_Unloaded;
				CancelAnimationFromDescendant((DependencyObject)(object)frameworkElement);
			}
		}
	}

	private static void DescendantScrollHost_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		DependencyObject val = (DependencyObject)((sender is DependencyObject) ? sender : null);
		if (val != null)
		{
			HandleMouseWheelFromDescendant(val, e);
		}
	}

	private static void DescendantScrollHost_Unloaded(object sender, RoutedEventArgs e)
	{
		DependencyObject val = (DependencyObject)((sender is DependencyObject) ? sender : null);
		if (val != null)
		{
			CancelAnimationFromDescendant(val);
		}
	}

	private static void ScrollViewer_Unloaded(object sender, RoutedEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			CancelAnimation(scrollViewer);
		}
	}

	private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer && !GetIsInternalScrollUpdate((DependencyObject)(object)scrollViewer) && !GetIsAnimating((DependencyObject)(object)scrollViewer) && ((DependencyObject)scrollViewer).GetValue(MomentumProperty) is ScrollMomentum scrollMomentum)
		{
			scrollMomentum.Impulses.Clear();
			scrollMomentum.Offset = scrollViewer.VerticalOffset;
			ReleaseRenderingHook(scrollViewer, scrollMomentum);
		}
	}

	private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			HandleMouseWheel(scrollViewer, e);
		}
	}

	public static bool HandleMouseWheel(ScrollViewer scrollViewer, MouseWheelEventArgs e)
	{
		return HandleMouseWheel(scrollViewer, e, (DependencyObject)(object)scrollViewer);
	}

	private static bool HandleMouseWheel(ScrollViewer scrollViewer, MouseWheelEventArgs e, DependencyObject optionsSource)
	{
		if (scrollViewer.ScrollableHeight <= 0.0 || (scrollViewer.CanContentScroll && !GetAllowContentScroll(optionsSource)))
		{
			return false;
		}
		double num = GetScrollAmount(optionsSource) * Math.Max(1.0, (double)Math.Abs(e.Delta) / 120.0);
		double num2 = ((e.Delta > 0) ? (0.0 - num) : num);
		ScrollMomentum scrollMomentum = EnsureMomentum(scrollViewer);
		double num3 = Math.Clamp(scrollMomentum.Offset + GetRemainingDelta(scrollMomentum), 0.0, scrollViewer.ScrollableHeight);
		if ((num2 < 0.0 && num3 <= 0.1) || (num2 > 0.0 && num3 >= scrollViewer.ScrollableHeight - 0.1))
		{
			e.Handled = true;
			return true;
		}
		double num4 = GetWheelAnimationDurationMilliseconds(optionsSource);
		if (GetAllowContentScroll(optionsSource))
		{
			num4 = Math.Clamp(num4, 100.0, 160.0);
		}
		scrollMomentum.Impulses.Add(new ScrollImpulse
		{
			TotalDelta = num2,
			StartedAt = Stopwatch.GetTimestamp(),
			DurationMilliseconds = Math.Max(num4, 1.0)
		});
		SetIsAnimating((DependencyObject)(object)scrollViewer, value: true);
		BeginScrollInteractionScope(scrollViewer);
		EnsureRenderingHook(scrollViewer, scrollMomentum);
		e.Handled = true;
		return true;
	}

	private static ScrollMomentum EnsureMomentum(ScrollViewer scrollViewer)
	{
		if (((DependencyObject)scrollViewer).GetValue(MomentumProperty) is ScrollMomentum result)
		{
			return result;
		}
		ScrollMomentum scrollMomentum = new ScrollMomentum
		{
			Offset = scrollViewer.VerticalOffset
		};
		((DependencyObject)scrollViewer).SetValue(MomentumProperty, (object)scrollMomentum);
		return scrollMomentum;
	}

	private static double GetRemainingDelta(ScrollMomentum momentum)
	{
		double num = 0.0;
		foreach (ScrollImpulse impulse in momentum.Impulses)
		{
			num += impulse.TotalDelta * (1.0 - impulse.AppliedProgress);
		}
		return num;
	}

	private static void EnsureRenderingHook(ScrollViewer scrollViewer, ScrollMomentum momentum)
	{
		if (!momentum.IsClockRunning)
		{
			momentum.IsClockRunning = true;
			scrollViewer.BeginAnimation(MomentumClockProperty, new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromSeconds(1.0)))
			{
				RepeatBehavior = RepeatBehavior.Forever
			});
		}
	}

	private static void ReleaseRenderingHook(ScrollViewer scrollViewer, ScrollMomentum momentum)
	{
		if (momentum.IsClockRunning)
		{
			momentum.IsClockRunning = false;
			scrollViewer.BeginAnimation(MomentumClockProperty, null);
		}
	}

	private static void OnMomentumClockChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ScrollViewer scrollViewer && ((DependencyObject)scrollViewer).GetValue(MomentumProperty) is ScrollMomentum momentum)
		{
			AdvanceMomentum(scrollViewer, momentum);
		}
	}

	private static void AdvanceMomentum(ScrollViewer scrollViewer, ScrollMomentum momentum)
	{
		double num = 0.0;
		for (int num2 = momentum.Impulses.Count - 1; num2 >= 0; num2--)
		{
			ScrollImpulse scrollImpulse = momentum.Impulses[num2];
			double num3 = Math.Clamp(Stopwatch.GetElapsedTime(scrollImpulse.StartedAt).TotalMilliseconds / scrollImpulse.DurationMilliseconds, 0.0, 1.0);
			double num4 = 1.0 - Math.Pow(1.0 - num3, 3.0);
			num += scrollImpulse.TotalDelta * (num4 - scrollImpulse.AppliedProgress);
			scrollImpulse.AppliedProgress = num4;
			if (num3 >= 1.0)
			{
				momentum.Impulses.RemoveAt(num2);
			}
		}
		if (Math.Abs(num) > 0.001)
		{
			momentum.Offset = Math.Clamp(momentum.Offset + num, 0.0, scrollViewer.ScrollableHeight);
			SetIsInternalScrollUpdate((DependencyObject)(object)scrollViewer, value: true);
			scrollViewer.ScrollToVerticalOffset(momentum.Offset);
			SetIsInternalScrollUpdate((DependencyObject)(object)scrollViewer, value: false);
		}
		if (momentum.Impulses.Count <= 0)
		{
			ReleaseRenderingHook(scrollViewer, momentum);
			momentum.Offset = scrollViewer.VerticalOffset;
			SetIsAnimating((DependencyObject)(object)scrollViewer, value: false);
			ReleaseInteractionScope(scrollViewer);
		}
	}

	public static bool HandleMouseWheelFromDescendant(DependencyObject root, MouseWheelEventArgs e, bool handleWhenUnavailable = false)
	{
		ScrollViewer scrollViewer = FindDescendant<ScrollViewer>(root);
		if (scrollViewer == null)
		{
			if (handleWhenUnavailable)
			{
				e.Handled = true;
			}
			return false;
		}
		DependencyObject optionsSource = (DependencyObject)(object)(GetIsEnabled(root) ? ((ScrollViewer)(object)root) : scrollViewer);
		bool num = HandleMouseWheel(scrollViewer, e, optionsSource);
		if (!num & handleWhenUnavailable)
		{
			e.Handled = true;
		}
		return num;
	}

	private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
	{
		int childrenCount = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			T val = (T)(object)((child is T) ? child : null);
			if (val != null)
			{
				return val;
			}
			T val2 = FindDescendant<T>(child);
			if (val2 != null)
			{
				return val2;
			}
		}
		return default(T);
	}

	static SmoothScrollBehavior()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Expected O, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Expected O, but got Unknown
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Expected O, but got Unknown
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Expected O, but got Unknown
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Expected O, but got Unknown
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Expected O, but got Unknown
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Expected O, but got Unknown
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Expected O, but got Unknown
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Expected O, but got Unknown
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Expected O, but got Unknown
		IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(SmoothScrollBehavior), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsEnabledChanged)));
		ScrollAmountProperty = DependencyProperty.RegisterAttached("ScrollAmount", typeof(double), typeof(SmoothScrollBehavior), new PropertyMetadata((object)84.0));
		AllowContentScrollProperty = DependencyProperty.RegisterAttached("AllowContentScroll", typeof(bool), typeof(SmoothScrollBehavior), new PropertyMetadata((object)false));
		WheelAnimationDurationMillisecondsProperty = DependencyProperty.RegisterAttached("WheelAnimationDurationMilliseconds", typeof(double), typeof(SmoothScrollBehavior), new PropertyMetadata((object)130.0));
		MomentumClockProperty = DependencyProperty.RegisterAttached("MomentumClock", typeof(double), typeof(SmoothScrollBehavior), new PropertyMetadata((object)0.0, new PropertyChangedCallback(OnMomentumClockChanged)));
		MomentumProperty = DependencyProperty.RegisterAttached("Momentum", typeof(ScrollMomentum), typeof(SmoothScrollBehavior), new PropertyMetadata((PropertyChangedCallback)null));
		IsInternalScrollUpdateProperty = DependencyProperty.RegisterAttached("IsInternalScrollUpdate", typeof(bool), typeof(SmoothScrollBehavior), new PropertyMetadata((object)false));
		IsAnimatingProperty = DependencyProperty.RegisterAttached("IsAnimating", typeof(bool), typeof(SmoothScrollBehavior), new PropertyMetadata((object)false));
		AnimationVersionProperty = DependencyProperty.RegisterAttached("AnimationVersion", typeof(int), typeof(SmoothScrollBehavior), new PropertyMetadata((object)0));
		InteractionScopeProperty = DependencyProperty.RegisterAttached("InteractionScope", typeof(UiInteractionScope), typeof(SmoothScrollBehavior), new PropertyMetadata((PropertyChangedCallback)null));
	}
}
