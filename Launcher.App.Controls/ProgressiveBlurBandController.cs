using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Launcher.App.Behaviors;
using Launcher.App.Effects;
using Serilog;

namespace Launcher.App.Controls;

internal sealed class ProgressiveBlurBandController
{
	private static readonly DependencyPropertyDescriptor? TopFadeLengthDescriptor = DependencyPropertyDescriptor.FromProperty(VerticalEdgeOpacityMask.TopFadeLengthProperty, typeof(Grid));

	private static int progressiveBlurFailureLogged;

	private readonly ProgressiveBlurVisualParts parts;

	private readonly Func<bool> isActive;

	private readonly ProgressiveBlurEffectFactory effectFactory;

	private readonly ProgressiveBlurEffectAttacher effectAttacher;

	private readonly RectangleGeometry directListClipGeometry = new RectangleGeometry();

	private ProgressiveGaussianBlurEffect? horizontalEffect;

	private ProgressiveGaussianBlurEffect? verticalEffect;

	private Window? dpiWindow;

	private bool subscriptionsAttached;

	private bool activationFailureLatched;

	internal ProgressiveBlurBandController(ProgressiveBlurVisualParts parts, Func<bool> isActive, ProgressiveBlurEffectFactory? effectFactory = null, ProgressiveBlurEffectAttacher? effectAttacher = null)
	{
		this.parts = parts;
		this.isActive = isActive;
		this.effectFactory = effectFactory ?? new ProgressiveBlurEffectFactory(CreateEffects);
		this.effectAttacher = effectAttacher ?? new ProgressiveBlurEffectAttacher(AttachEffects);
		parts.BlurBandBrush.Visual = parts.ListVisualSource;
	}

	internal void OnLoaded()
	{
		AttachSubscriptions();
		Update();
	}

	internal void OnUnloaded()
	{
		DetachSubscriptions();
		Deactivate();
	}

	internal void OnEnabledChanged(bool becameEnabled)
	{
		if (becameEnabled)
		{
			activationFailureLatched = false;
		}
		Update();
	}

	internal void Update()
	{
		if (!parts.Owner.IsLoaded || !isActive())
		{
			Deactivate();
			return;
		}
		double actualWidth = parts.ListLayer.ActualWidth;
		double actualHeight = parts.ListLayer.ActualHeight;
		double num = ResolveEffectiveTopBlurLength(actualHeight);
		double num2 = Math.Min(actualHeight, num + 24.0);
		if (actualWidth <= 0.0 || actualHeight <= 0.0 || num <= 0.0 || num2 <= 0.0)
		{
			Deactivate();
			return;
		}
		try
		{
			if (TryEnsureEffects())
			{
				double maximumRadius = ResolveDoubleResource("ListPage.ProgressiveBlur.MaxRadius", 24.0, 0.0, double.MaxValue);
				double renderScale = ResolveDoubleResource("ListPage.ProgressiveBlur.RenderScale", 0.2, 0.1, 1.0);
				ProgressiveBlurRenderLayout renderLayout = ProgressiveBlurLayoutCalculator.Calculate(actualWidth, actualHeight, num, num2, maximumRadius, renderScale, VisualTreeHelper.GetDpi(parts.ListLayer));
				UpdateBandLayout(actualWidth, actualHeight, renderLayout);
				ApplyEffectParameters(horizontalEffect, renderLayout.LowResolutionWidth, renderLayout.LowResolutionHeight, renderLayout.ScaledBlurLength, renderLayout.HorizontalMaximumRadius);
				ApplyEffectParameters(verticalEffect, renderLayout.LowResolutionWidth, renderLayout.LowResolutionHeight, renderLayout.ScaledBlurLength, renderLayout.VerticalMaximumRadius);
				effectAttacher(horizontalEffect, verticalEffect);
				parts.BlurBandViewport.Visibility = Visibility.Visible;
				VerticalEdgeOpacityMask.SetTopMinimumOpacity((DependencyObject)(object)parts.ListLayer, ResolveDoubleResource("ListPage.ProgressiveBlur.ActiveMinimumOpacity", 0.0, 0.0, 1.0));
				VerticalEdgeOpacityMask.SetTopIntermediateOpacity((DependencyObject)(object)parts.ListLayer, ResolveDoubleResource("ListPage.ProgressiveBlur.ActiveIntermediateOpacity", 0.4, 0.0, 1.0));
			}
		}
		catch (Exception exception)
		{
			HandleActivationFailure(exception);
		}
	}

	private void AttachSubscriptions()
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Expected O, but got Unknown
		if (!subscriptionsAttached)
		{
			parts.ListLayer.SizeChanged += ListLayer_SizeChanged;
			parts.Owner.IsVisibleChanged += new DependencyPropertyChangedEventHandler(Owner_IsVisibleChanged);
			((PropertyDescriptor)(object)TopFadeLengthDescriptor)?.AddValueChanged((object)parts.ListLayer, (EventHandler)ListLayer_TopFadeLengthChanged);
			AttachDpiSubscription();
			subscriptionsAttached = true;
		}
	}

	private void DetachSubscriptions()
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Expected O, but got Unknown
		if (subscriptionsAttached)
		{
			parts.ListLayer.SizeChanged -= ListLayer_SizeChanged;
			parts.Owner.IsVisibleChanged -= new DependencyPropertyChangedEventHandler(Owner_IsVisibleChanged);
			((PropertyDescriptor)(object)TopFadeLengthDescriptor)?.RemoveValueChanged((object)parts.ListLayer, (EventHandler)ListLayer_TopFadeLengthChanged);
			DetachDpiSubscription();
			subscriptionsAttached = false;
		}
	}

	private void AttachDpiSubscription()
	{
		Window window = Window.GetWindow((DependencyObject)(object)parts.Owner);
		if (dpiWindow != window)
		{
			DetachDpiSubscription();
			dpiWindow = window;
			if (dpiWindow != null)
			{
				dpiWindow.DpiChanged += Window_DpiChanged;
			}
		}
	}

	private void DetachDpiSubscription()
	{
		if (dpiWindow != null)
		{
			dpiWindow.DpiChanged -= Window_DpiChanged;
			dpiWindow = null;
		}
	}

	private void Window_DpiChanged(object sender, DpiChangedEventArgs e)
	{
		Update();
	}

	private void ListLayer_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		Update();
	}

	private void Owner_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		Update();
	}

	private void ListLayer_TopFadeLengthChanged(object? sender, EventArgs e)
	{
		Update();
	}

	private bool TryEnsureEffects()
	{
		if (activationFailureLatched)
		{
			return false;
		}
		if (horizontalEffect != null && verticalEffect != null)
		{
			return true;
		}
		horizontalEffect = null;
		verticalEffect = null;
		ProgressiveBlurEffectCreationResult progressiveBlurEffectCreationResult = effectFactory();
		if (!progressiveBlurEffectCreationResult.IsSuccess)
		{
			HandleActivationFailure(progressiveBlurEffectCreationResult.Exception ?? new InvalidOperationException("Progressive blur shader did not create both effect instances."));
			return false;
		}
		horizontalEffect = progressiveBlurEffectCreationResult.HorizontalEffect;
		verticalEffect = progressiveBlurEffectCreationResult.VerticalEffect;
		return true;
	}

	private static ProgressiveBlurEffectCreationResult CreateEffects()
	{
		if (!ProgressiveGaussianBlurEffect.TryCreate(1.0, 0.0, out ProgressiveGaussianBlurEffect effect, out Exception exception) || effect == null)
		{
			return ProgressiveBlurEffectCreationResult.Failed(exception ?? new InvalidOperationException("Progressive blur horizontal effect could not be created."));
		}
		if (!ProgressiveGaussianBlurEffect.TryCreate(0.0, 1.0, out ProgressiveGaussianBlurEffect effect2, out Exception exception2) || effect2 == null)
		{
			return ProgressiveBlurEffectCreationResult.Failed(exception2 ?? new InvalidOperationException("Progressive blur vertical effect could not be created."));
		}
		return new ProgressiveBlurEffectCreationResult(effect, effect2, null);
	}

	private void AttachEffects(ProgressiveGaussianBlurEffect horizontalBlurEffect, ProgressiveGaussianBlurEffect verticalBlurEffect)
	{
		parts.BlurBandHorizontalHost.Effect = horizontalBlurEffect;
		parts.BlurBandVerticalHost.Effect = verticalBlurEffect;
	}

	private static void ApplyEffectParameters(ProgressiveGaussianBlurEffect effect, double width, double height, double blurLength, double maximumRadius)
	{
		effect.InputWidth = Math.Max(1.0, width);
		effect.InputHeight = Math.Max(1.0, height);
		effect.BlurLength = Math.Clamp(blurLength, 0.0, height);
		effect.MaximumRadius = Math.Max(0.0, maximumRadius);
	}

	private void UpdateBandLayout(double width, double height, ProgressiveBlurRenderLayout renderLayout)
	{
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		parts.BlurBandViewport.Height = renderLayout.PresentationHeight;
		parts.BlurBandUpscaleHost.Width = renderLayout.LowResolutionWidth;
		parts.BlurBandUpscaleHost.Height = renderLayout.LowResolutionHeight;
		parts.BlurBandHorizontalHost.Width = renderLayout.LowResolutionWidth;
		parts.BlurBandHorizontalHost.Height = renderLayout.LowResolutionHeight;
		parts.BlurBandVerticalHost.Width = renderLayout.LowResolutionWidth;
		parts.BlurBandVerticalHost.Height = renderLayout.LowResolutionHeight;
		parts.BlurBandUpscaleTransform.ScaleX = renderLayout.UpscaleX;
		parts.BlurBandUpscaleTransform.ScaleY = renderLayout.UpscaleY;
		parts.BlurBandBrush.Viewbox = new Rect(0.0, 0.0, width, renderLayout.TextureHeight);
		directListClipGeometry.Rect = new Rect(0.0, renderLayout.DirectListStart, width, Math.Max(0.0, height - renderLayout.DirectListStart));
		parts.DirectListHost.Clip = directListClipGeometry;
	}

	private double ResolveEffectiveTopBlurLength(double height)
	{
		if (height <= 0.0)
		{
			return 0.0;
		}
		double num = Math.Clamp(VerticalEdgeOpacityMask.GetTopFadeLength((DependencyObject)(object)parts.ListLayer), 0.0, height);
		double num2 = Math.Clamp(VerticalEdgeOpacityMask.GetBottomFadeLength((DependencyObject)(object)parts.ListLayer), 0.0, height);
		double num3 = num + num2;
		if (num3 > height && num3 > 0.0)
		{
			num *= height / num3;
		}
		return num;
	}

	private double ResolveDoubleResource(string key, double fallback, double minimum, double maximum)
	{
		double num = ((parts.Owner.TryFindResource(key) is double num2) ? num2 : fallback);
		if (!double.IsFinite(num))
		{
			return fallback;
		}
		return Math.Clamp(num, minimum, maximum);
	}

	private void HandleActivationFailure(Exception exception)
	{
		Deactivate();
		horizontalEffect = null;
		verticalEffect = null;
		activationFailureLatched = true;
		LogFailureOnce(exception);
	}

	private void Deactivate()
	{
		parts.BlurBandVerticalHost.Effect = null;
		parts.BlurBandHorizontalHost.Effect = null;
		parts.BlurBandViewport.Visibility = Visibility.Collapsed;
		parts.DirectListHost.Clip = null;
		((DependencyObject)parts.ListLayer).ClearValue(VerticalEdgeOpacityMask.TopMinimumOpacityProperty);
		((DependencyObject)parts.ListLayer).ClearValue(VerticalEdgeOpacityMask.TopIntermediateOpacityProperty);
	}

	private static void LogFailureOnce(Exception exception)
	{
		if (Interlocked.Exchange(ref progressiveBlurFailureLogged, 1) == 0)
		{
			Log.Warning(exception, "Progressive blur effect activation failed; opacity fade fallback will be used.");
		}
	}
}
