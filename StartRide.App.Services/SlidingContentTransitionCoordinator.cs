using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StartRide.App.Controls;
using StartRide.App.Diagnostics;

namespace StartRide.App.Services;

public sealed class SlidingContentTransitionCoordinator
{
	private sealed record LayerTransforms(ScaleTransform Scale, TranslateTransform Translate);

	internal static readonly TimeSpan StepTransitionDuration = TimeSpan.FromMilliseconds(240.0);

	internal static readonly TimeSpan FloatingElementFadeDuration = TimeSpan.FromMilliseconds(180.0);

	internal const double DefaultTransitionScale = 0.985;

	private readonly FrameworkElement loadedElement;

	private readonly FrameworkElement contentHost;

	private readonly FrameworkElement primaryLayer;

	private readonly FrameworkElement secondaryLayer;

	private readonly IReadOnlyList<FrameworkElement> secondaryFloatingElements;

	private readonly bool useSlideTransition;

	private readonly bool useScaleTransition;

	private readonly double transitionScale;

	private readonly TransitionRenderCacheFactory renderCacheFactory;

	private bool isSecondaryLayerVisible;

	private int transitionToken;

	private IDisposable? blurRefreshLease;

	private TransitionRenderCacheScope? renderCacheScope;

	private UiInteractionScope? interactionScope;

	public SlidingContentTransitionCoordinator(FrameworkElement loadedElement, FrameworkElement contentHost, FrameworkElement primaryLayer, FrameworkElement secondaryLayer, IEnumerable<FrameworkElement>? secondaryFloatingElements = null, bool useSlideTransition = true, bool useScaleTransition = false, double transitionScale = 0.985)
		: this(loadedElement, contentHost, primaryLayer, secondaryLayer, secondaryFloatingElements, useSlideTransition, useScaleTransition, transitionScale, TransitionRenderCacheScope.TryAcquire)
	{
	}

	internal SlidingContentTransitionCoordinator(FrameworkElement loadedElement, FrameworkElement contentHost, FrameworkElement primaryLayer, FrameworkElement secondaryLayer, IEnumerable<FrameworkElement>? secondaryFloatingElements, bool useSlideTransition, bool useScaleTransition, double transitionScale, TransitionRenderCacheFactory renderCacheFactory)
	{
		this.loadedElement = loadedElement;
		this.contentHost = contentHost;
		this.primaryLayer = primaryLayer;
		this.secondaryLayer = secondaryLayer;
		this.secondaryFloatingElements = secondaryFloatingElements?.ToArray() ?? Array.Empty<FrameworkElement>();
		this.useSlideTransition = useSlideTransition;
		this.useScaleTransition = useScaleTransition;
		this.transitionScale = transitionScale;
		this.renderCacheFactory = renderCacheFactory;
		loadedElement.Unloaded += LoadedElement_Unloaded;
	}

	public void Sync(bool showSecondaryLayer)
	{
		ReleaseTransitionResources(requestFinalRefresh: true);
		transitionToken++;
		isSecondaryLayerVisible = showSecondaryLayer;
		ResetLayer(primaryLayer, !showSecondaryLayer);
		ResetLayer(secondaryLayer, showSecondaryLayer);
		SyncFloatingElements(showSecondaryLayer);
	}

	public void AnimateTo(bool showSecondaryLayer)
	{
		if (isSecondaryLayerVisible == showSecondaryLayer)
		{
			Sync(showSecondaryLayer);
			return;
		}
		if (!loadedElement.IsLoaded || (useSlideTransition && contentHost.ActualWidth <= 0.0))
		{
			Sync(showSecondaryLayer);
			return;
		}
		ResetLayer(primaryLayer, !isSecondaryLayerVisible);
		ResetLayer(secondaryLayer, isSecondaryLayerVisible);
		SyncFloatingElements(isSecondaryLayerVisible);
		ReleaseTransitionResources(requestFinalRefresh: true);
		FrameworkElement previousLayer = (isSecondaryLayerVisible ? secondaryLayer : primaryLayer);
		FrameworkElement nextLayer = (showSecondaryLayer ? secondaryLayer : primaryLayer);
		int num = (showSecondaryLayer ? 1 : (-1));
		double num2 = Math.Max(contentHost.ActualWidth, 1.0);
		int token = ++transitionToken;
		isSecondaryLayerVisible = showSecondaryLayer;
		LayerTransforms layerTransforms = EnsureLayerTransforms(previousLayer);
		LayerTransforms layerTransforms2 = EnsureLayerTransforms(nextLayer);
		previousLayer.Visibility = Visibility.Visible;
		previousLayer.Opacity = 1.0;
		layerTransforms.Translate.BeginAnimation(TranslateTransform.XProperty, null);
		layerTransforms.Translate.X = 0.0;
		layerTransforms.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		layerTransforms.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		layerTransforms.Scale.ScaleX = 1.0;
		layerTransforms.Scale.ScaleY = 1.0;
		nextLayer.Visibility = Visibility.Visible;
		nextLayer.Opacity = 0.0;
		layerTransforms2.Translate.BeginAnimation(TranslateTransform.XProperty, null);
		layerTransforms2.Translate.X = (useSlideTransition ? (num2 * (double)num) : 0.0);
		layerTransforms2.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		layerTransforms2.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		layerTransforms2.Scale.ScaleX = (useScaleTransition ? transitionScale : 1.0);
		layerTransforms2.Scale.ScaleY = (useScaleTransition ? transitionScale : 1.0);
		if (BackdropBlurRefreshCoordinator.HasActiveImageBackdropBlur(previousLayer, nextLayer))
		{
			blurRefreshLease = BackdropBlurRefreshCoordinator.BeginContinuousRefresh(previousLayer, nextLayer);
		}
		else
		{
			renderCacheScope = renderCacheFactory("Sliding:" + ((object)loadedElement).GetType().Name, new global::_003C_003Ez__ReadOnlyArray<FrameworkElement>(new FrameworkElement[2] { previousLayer, nextLayer }));
			if (TransitionRenderCacheScope.RequiresContinuousRefreshFallback(renderCacheScope.FallbackReason))
			{
				blurRefreshLease = BackdropBlurRefreshCoordinator.BeginContinuousRefresh(previousLayer, nextLayer);
			}
		}
		interactionScope = UiPerformanceLog.BeginInteraction("LayerTransition", ((object)loadedElement).GetType().Name, loadedElement);
		interactionScope.RenderPath = UiRenderPaths.Resolve(renderCacheScope?.IsActive ?? false, blurRefreshLease != null);
		AnimateFloatingElements(showSecondaryLayer, token);
		DoubleAnimation doubleAnimation = (useSlideTransition ? CreateTransitionAnimation(0.0, (0.0 - num2) * (double)num) : null);
		DoubleAnimation doubleAnimation2 = (useSlideTransition ? CreateTransitionAnimation(num2 * (double)num, 0.0) : null);
		DoubleAnimation animation = CreateTransitionAnimation(1.0, 0.0);
		DoubleAnimation doubleAnimation3 = CreateTransitionAnimation(0.0, 1.0);
		DoubleAnimation doubleAnimation4 = (useScaleTransition ? CreateTransitionAnimation(1.0, transitionScale) : null);
		DoubleAnimation doubleAnimation5 = (useScaleTransition ? CreateTransitionAnimation(transitionScale, 1.0) : null);
		(doubleAnimation2 ?? doubleAnimation3).Completed += delegate
		{
			if (token == transitionToken)
			{
				ResetLayer(previousLayer, isVisible: false);
				ResetLayer(nextLayer, isVisible: true);
				ReleaseTransitionResources(requestFinalRefresh: true);
			}
		};
		previousLayer.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
		nextLayer.BeginAnimation(UIElement.OpacityProperty, doubleAnimation3, HandoffBehavior.SnapshotAndReplace);
		if (doubleAnimation != null && doubleAnimation2 != null)
		{
			layerTransforms.Translate.BeginAnimation(TranslateTransform.XProperty, doubleAnimation, HandoffBehavior.SnapshotAndReplace);
			layerTransforms2.Translate.BeginAnimation(TranslateTransform.XProperty, doubleAnimation2, HandoffBehavior.SnapshotAndReplace);
		}
		if (doubleAnimation4 != null && doubleAnimation5 != null)
		{
			layerTransforms.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimation4, HandoffBehavior.SnapshotAndReplace);
			layerTransforms.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation4.Clone(), HandoffBehavior.SnapshotAndReplace);
			layerTransforms2.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimation5, HandoffBehavior.SnapshotAndReplace);
			layerTransforms2.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation5.Clone(), HandoffBehavior.SnapshotAndReplace);
		}
	}

	private void ReleaseBlurRefreshLease()
	{
		blurRefreshLease?.Dispose();
		blurRefreshLease = null;
	}

	private void ReleaseTransitionResources(bool requestFinalRefresh)
	{
		interactionScope?.Dispose();
		interactionScope = null;
		ReleaseBlurRefreshLease();
		bool flag = renderCacheScope?.IsActive ?? false;
		renderCacheScope?.Dispose();
		renderCacheScope = null;
		if (requestFinalRefresh & flag)
		{
			BackdropBlurRefreshCoordinator.RequestScopeRefresh(primaryLayer, secondaryLayer);
		}
	}

	private void LoadedElement_Unloaded(object sender, RoutedEventArgs e)
	{
		transitionToken++;
		ReleaseTransitionResources(requestFinalRefresh: false);
	}

	private void SyncFloatingElements(bool showSecondaryLayer)
	{
		foreach (FrameworkElement secondaryFloatingElement in secondaryFloatingElements)
		{
			ResetFloatingElement(secondaryFloatingElement, showSecondaryLayer);
		}
	}

	private void ResetLayer(FrameworkElement layer, bool isVisible)
	{
		layer.BeginAnimation(UIElement.OpacityProperty, null);
		LayerTransforms layerTransforms = EnsureLayerTransforms(layer);
		layerTransforms.Translate.BeginAnimation(TranslateTransform.XProperty, null);
		layerTransforms.Translate.X = 0.0;
		layerTransforms.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		layerTransforms.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		layerTransforms.Scale.ScaleX = 1.0;
		layerTransforms.Scale.ScaleY = 1.0;
		layer.Opacity = (isVisible ? 1 : 0);
		layer.Visibility = ((!isVisible) ? Visibility.Collapsed : Visibility.Visible);
	}

	private static void ResetFloatingElement(FrameworkElement element, bool isVisible)
	{
		element.BeginAnimation(UIElement.OpacityProperty, null);
		element.Opacity = (isVisible ? 1 : 0);
		element.Visibility = ((!isVisible) ? Visibility.Collapsed : Visibility.Visible);
		element.IsHitTestVisible = isVisible;
	}

	private void AnimateFloatingElements(bool showSecondaryLayer, int token)
	{
		foreach (FrameworkElement secondaryFloatingElement in secondaryFloatingElements)
		{
			if (showSecondaryLayer)
			{
				FadeFloatingElementIn(secondaryFloatingElement, token);
			}
			else
			{
				FadeFloatingElementOut(secondaryFloatingElement, token);
			}
		}
	}

	private void FadeFloatingElementIn(FrameworkElement element, int token)
	{
		element.BeginAnimation(UIElement.OpacityProperty, null);
		element.Visibility = Visibility.Visible;
		element.IsHitTestVisible = true;
		DoubleAnimation doubleAnimation = CreateFloatingElementFadeAnimation(element.Opacity, 1.0);
		doubleAnimation.Completed += delegate
		{
			if (token == transitionToken)
			{
				element.BeginAnimation(UIElement.OpacityProperty, null);
				element.Opacity = 1.0;
				element.Visibility = Visibility.Visible;
				element.IsHitTestVisible = true;
			}
		};
		element.BeginAnimation(UIElement.OpacityProperty, doubleAnimation, HandoffBehavior.SnapshotAndReplace);
	}

	private void FadeFloatingElementOut(FrameworkElement element, int token)
	{
		element.BeginAnimation(UIElement.OpacityProperty, null);
		element.IsHitTestVisible = false;
		DoubleAnimation doubleAnimation = CreateFloatingElementFadeAnimation(element.Opacity, 0.0);
		doubleAnimation.Completed += delegate
		{
			if (token == transitionToken)
			{
				element.BeginAnimation(UIElement.OpacityProperty, null);
				element.Opacity = 0.0;
				element.Visibility = Visibility.Collapsed;
				element.IsHitTestVisible = false;
			}
		};
		element.BeginAnimation(UIElement.OpacityProperty, doubleAnimation, HandoffBehavior.SnapshotAndReplace);
	}

	private static DoubleAnimation CreateFloatingElementFadeAnimation(double from, double to)
	{
		return new DoubleAnimation(from, to, FloatingElementFadeDuration)
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			},
			FillBehavior = FillBehavior.Stop
		};
	}

	private static DoubleAnimation CreateTransitionAnimation(double from, double to)
	{
		return new DoubleAnimation(from, to, StepTransitionDuration)
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			},
			FillBehavior = FillBehavior.Stop
		};
	}

	private LayerTransforms EnsureLayerTransforms(FrameworkElement layer)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		if (useScaleTransition && object.Equals(((DependencyObject)layer).ReadLocalValue(UIElement.RenderTransformOriginProperty), DependencyProperty.UnsetValue))
		{
			layer.RenderTransformOrigin = new Point(0.5, 0.5);
		}
		if (layer.RenderTransform is TransformGroup transformGroup)
		{
			ScaleTransform scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
			if (scaleTransform == null)
			{
				scaleTransform = new ScaleTransform();
				transformGroup.Children.Insert(0, scaleTransform);
			}
			TranslateTransform translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
			if (translateTransform == null)
			{
				translateTransform = new TranslateTransform();
				transformGroup.Children.Add(translateTransform);
			}
			return new LayerTransforms(scaleTransform, translateTransform);
		}
		if (layer.RenderTransform is TranslateTransform translateTransform2)
		{
			ScaleTransform scaleTransform2 = new ScaleTransform();
			TransformGroup transformGroup2 = new TransformGroup();
			transformGroup2.Children.Add(scaleTransform2);
			transformGroup2.Children.Add(translateTransform2);
			layer.RenderTransform = transformGroup2;
			return new LayerTransforms(scaleTransform2, translateTransform2);
		}
		if (layer.RenderTransform is ScaleTransform scaleTransform3)
		{
			TranslateTransform translateTransform3 = new TranslateTransform();
			TransformGroup transformGroup3 = new TransformGroup();
			transformGroup3.Children.Add(scaleTransform3);
			transformGroup3.Children.Add(translateTransform3);
			layer.RenderTransform = transformGroup3;
			return new LayerTransforms(scaleTransform3, translateTransform3);
		}
		ScaleTransform scaleTransform4 = new ScaleTransform();
		TranslateTransform translateTransform4 = new TranslateTransform();
		TransformGroup transformGroup4 = new TransformGroup();
		if (layer.RenderTransform != null && layer.RenderTransform != Transform.Identity)
		{
			transformGroup4.Children.Add(layer.RenderTransform);
		}
		transformGroup4.Children.Add(scaleTransform4);
		transformGroup4.Children.Add(translateTransform4);
		layer.RenderTransform = transformGroup4;
		return new LayerTransforms(scaleTransform4, translateTransform4);
	}
}
