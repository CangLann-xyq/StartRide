using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StartRide.App.Controls;
using StartRide.App.Diagnostics;
using StartRide.App.Models;
using Serilog;

namespace StartRide.App.Services;

public sealed class PageTransitionService
{
	internal const double TransitionOffset = 22.0;

	internal static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(240.0);

	private static readonly IReadOnlyList<string> DefaultPageOrder = NavigationCatalog.PageOrder;

	internal const double WarmupOpacity = 1.0 / 255.0;

	private const int WarmupCompositionFrames = 1;

	private static readonly TimeSpan CompositionWaitTimeout = TimeSpan.FromMilliseconds(250.0);

	private static readonly TimeSpan TransitionWatchdogTimeout = TimeSpan.FromSeconds(2.0);

	private readonly Dispatcher dispatcher;

	private readonly Func<string, FrameworkElement?> resolvePageRoot;

	private readonly Func<IReadOnlyList<string>> resolvePageOrder;

	private readonly TransitionRenderCacheFactory renderCacheFactory;

	private readonly ICompositionFrameSource compositionFrames;

	private string? currentPage;

	private int transitionToken;

	private IDisposable? blurRefreshLease;

	private TransitionRenderCacheScope? renderCacheScope;

	private UiInteractionScope? interactionScope;

	private FrameworkElement? activeTarget;

	private EventHandler? pendingCompositionWait;

	private DispatcherTimer? compositionWaitTimeout;

	private DispatcherTimer? transitionWatchdog;

	private long warmupStartedAtTimestamp;

	private DispatcherBusyProbe? warmupBusyProbe;

	private bool hasEnteredTransitionGate;

	public PageTransitionService(Dispatcher dispatcher, Func<string, FrameworkElement?> resolvePageRoot, string? initialPage)
		: this(dispatcher, resolvePageRoot, initialPage, null)
	{
	}

	public PageTransitionService(Dispatcher dispatcher, Func<string, FrameworkElement?> resolvePageRoot, string? initialPage, IReadOnlyList<string>? pageOrder)
		: this(dispatcher, resolvePageRoot, initialPage, pageOrder, TransitionRenderCacheScope.TryAcquire)
	{
	}

	public static PageTransitionService CreateWithDynamicOrder(Dispatcher dispatcher, Func<string, FrameworkElement?> resolvePageRoot, string? initialPage, Func<IReadOnlyList<string>> resolvePageOrder)
	{
		return new PageTransitionService(dispatcher, resolvePageRoot, initialPage, resolvePageOrder, TransitionRenderCacheScope.TryAcquire, null);
	}

	internal PageTransitionService(Dispatcher dispatcher, Func<string, FrameworkElement?> resolvePageRoot, string? initialPage, IReadOnlyList<string>? pageOrder, TransitionRenderCacheFactory renderCacheFactory, ICompositionFrameSource? compositionFrames = null)
		: this(dispatcher, resolvePageRoot, initialPage, () => (pageOrder == null || pageOrder.Count <= 0) ? DefaultPageOrder : pageOrder, renderCacheFactory, compositionFrames)
	{
	}

	private PageTransitionService(Dispatcher dispatcher, Func<string, FrameworkElement?> resolvePageRoot, string? initialPage, Func<IReadOnlyList<string>> resolvePageOrder, TransitionRenderCacheFactory renderCacheFactory, ICompositionFrameSource? compositionFrames)
	{
		this.dispatcher = dispatcher;
		UiTransitionGate.AttachDispatcher(dispatcher);
		this.resolvePageRoot = resolvePageRoot;
		this.resolvePageOrder = resolvePageOrder;
		this.renderCacheFactory = renderCacheFactory;
		this.compositionFrames = compositionFrames ?? CompositionTargetFrameSource.Instance;
		currentPage = initialPage;
	}

	public void MoveTo(string newPage)
	{
		if (string.Equals(currentPage, newPage, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		CancelActiveTransition(requestFinalRefresh: true);
		string oldPage = currentPage;
		currentPage = newPage;
		double startOffset = GetTransitionStartOffset(oldPage, newPage);
		FrameworkElement target = resolvePageRoot(newPage);
		if (target == null)
		{
			return;
		}
		int token = ++transitionToken;
		EnterTransitionGate();
		StartTransitionWatchdog();
		PreparePageForTransition(target, startOffset);
		activeTarget = target;
		target.Unloaded += ActiveTarget_Unloaded;
		warmupStartedAtTimestamp = Stopwatch.GetTimestamp();
		ReleaseWarmupBusyProbe();
		if (UiPerformanceLog.IsEnabled)
		{
			warmupBusyProbe = DispatcherBusyProbe.TryAttach(dispatcher);
		}
		WaitForCompositionFrames(1, delegate
		{
			if (IsTransitionCurrent(newPage, token))
			{
				PrepareRenderPathForTransition(newPage, target);
				WaitForCompositionFrames(1, delegate
				{
					AnimatePage(newPage, target, startOffset, token);
				});
			}
		});
	}

	private bool IsTransitionCurrent(string page, int token)
	{
		if (token == transitionToken)
		{
			return string.Equals(currentPage, page, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private void WaitForCompositionFrames(int frameCount, Action continuation)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		CancelPendingCompositionWait();
		int remaining = Math.Max(frameCount, 1);
		EventHandler handler = null;
		handler = delegate
		{
			if (--remaining <= 0 && TryCompleteCompositionWait(handler))
			{
				continuation();
			}
		};
		pendingCompositionWait = handler;
		compositionFrames.Subscribe(handler);
		DispatcherTimer val = new DispatcherTimer((DispatcherPriority)9, dispatcher)
		{
			Interval = CompositionWaitTimeout
		};
		val.Tick += delegate
		{
			if (TryCompleteCompositionWait(handler))
			{
				Log.Debug("Page transition warm-up timed out waiting for a composition frame. TimeoutMs={TimeoutMs}", CompositionWaitTimeout.TotalMilliseconds);
				continuation();
			}
		};
		compositionWaitTimeout = val;
		val.Start();
	}

	private bool TryCompleteCompositionWait(EventHandler handler)
	{
		compositionFrames.Unsubscribe(handler);
		if ((object)pendingCompositionWait != handler)
		{
			return false;
		}
		pendingCompositionWait = null;
		StopCompositionWaitTimeout();
		return true;
	}

	private void StopCompositionWaitTimeout()
	{
		DispatcherTimer? obj = compositionWaitTimeout;
		if (obj != null)
		{
			obj.Stop();
		}
		compositionWaitTimeout = null;
	}

	private void StartTransitionWatchdog()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		StopTransitionWatchdog();
		DispatcherTimer val = new DispatcherTimer((DispatcherPriority)9, dispatcher)
		{
			Interval = TransitionWatchdogTimeout
		};
		val.Tick += delegate
		{
			Log.Debug("Page transition did not finish in time and was force-completed. Page={Page} TimeoutMs={TimeoutMs}", currentPage, TransitionWatchdogTimeout.TotalMilliseconds);
			transitionToken++;
			CancelActiveTransition(requestFinalRefresh: true);
		};
		transitionWatchdog = val;
		val.Start();
	}

	private void StopTransitionWatchdog()
	{
		DispatcherTimer? obj = transitionWatchdog;
		if (obj != null)
		{
			obj.Stop();
		}
		transitionWatchdog = null;
	}

	private void ReleaseWarmupBusyProbe()
	{
		warmupBusyProbe?.Dispose();
		warmupBusyProbe = null;
	}

	private void CancelPendingCompositionWait()
	{
		StopCompositionWaitTimeout();
		if (pendingCompositionWait != null)
		{
			compositionFrames.Unsubscribe(pendingCompositionWait);
			pendingCompositionWait = null;
		}
	}

	public void SyncTo(string? page)
	{
		transitionToken++;
		ExitTransitionGate();
		CancelPendingCompositionWait();
		CancelActiveTransition(requestFinalRefresh: true);
		currentPage = page;
	}

	private double GetTransitionStartOffset(string? oldPage, string newPage)
	{
		if (string.IsNullOrWhiteSpace(oldPage))
		{
			return 22.0;
		}
		IReadOnlyList<string> order = resolvePageOrder() ?? DefaultPageOrder;
		int num = IndexOfPage(order, oldPage);
		int num2 = IndexOfPage(order, newPage);
		if (num < 0 || num2 < 0 || num == num2)
		{
			return 22.0;
		}
		if (num2 <= num)
		{
			return -22.0;
		}
		return 22.0;
	}

	private static int IndexOfPage(IReadOnlyList<string> order, string page)
	{
		for (int i = 0; i < order.Count; i++)
		{
			if (string.Equals(order[i], page, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return -1;
	}

	private static TranslateTransform EnsureTranslateTransform(FrameworkElement target)
	{
		if (target.RenderTransform is TranslateTransform result)
		{
			return result;
		}
		return (TranslateTransform)(target.RenderTransform = new TranslateTransform());
	}

	private static void PreparePageForTransition(FrameworkElement target, double startOffset)
	{
		target.BeginAnimation(UIElement.OpacityProperty, null);
		target.Opacity = 1.0 / 255.0;
		TranslateTransform translateTransform = EnsureTranslateTransform(target);
		translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
		translateTransform.Y = startOffset;
	}

	private void PrepareRenderPathForTransition(string page, FrameworkElement target)
	{
		if (BackdropBlurRefreshCoordinator.HasActiveImageBackdropBlur(target))
		{
			blurRefreshLease = BackdropBlurRefreshCoordinator.BeginContinuousRefresh(target);
			return;
		}
		renderCacheScope = renderCacheFactory("Page:" + page, new global::_003C_003Ez__ReadOnlySingleElementList<FrameworkElement>(target));
		if (TransitionRenderCacheScope.RequiresContinuousRefreshFallback(renderCacheScope.FallbackReason))
		{
			blurRefreshLease = BackdropBlurRefreshCoordinator.BeginContinuousRefresh(target);
		}
	}

	private void AnimatePage(string page, FrameworkElement target, double startOffset, int token)
	{
		if (!IsTransitionCurrent(page, token))
		{
			return;
		}
		TranslateTransform transform = EnsureTranslateTransform(target);
		target.BeginAnimation(UIElement.OpacityProperty, null);
		transform.BeginAnimation(TranslateTransform.YProperty, null);
		target.Opacity = 1.0 / 255.0;
		transform.Y = startOffset;
		interactionScope = UiPerformanceLog.BeginInteraction("PageTransition", page, target);
		interactionScope.RenderPath = UiRenderPaths.Resolve(renderCacheScope?.IsActive ?? false, blurRefreshLease != null);
		interactionScope.SurfaceWidth = target.ActualWidth;
		interactionScope.SurfaceHeight = target.ActualHeight;
		interactionScope.HasAncestorBitmapCache = renderCacheScope?.IsActive ?? false;
		interactionScope.WarmupMs = ((warmupStartedAtTimestamp == 0L) ? 0.0 : Stopwatch.GetElapsedTime(warmupStartedAtTimestamp).TotalMilliseconds);
		warmupStartedAtTimestamp = 0L;
		if (warmupBusyProbe != null)
		{
			interactionScope.WarmupBusyMs = warmupBusyProbe.TotalBusyMs;
			interactionScope.WarmupWorstOperationMs = warmupBusyProbe.WorstOperationMs;
			interactionScope.WarmupWorstOperation = warmupBusyProbe.WorstOperationDetail;
			ReleaseWarmupBusyProbe();
		}
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation
		{
			From = 1.0 / 255.0,
			To = 1.0,
			Duration = TransitionDuration,
			EasingFunction = easingFunction,
			FillBehavior = FillBehavior.Stop
		};
		doubleAnimation.Completed += delegate
		{
			if (token == transitionToken && string.Equals(currentPage, page, StringComparison.OrdinalIgnoreCase))
			{
				target.Opacity = 1.0;
			}
		};
		DoubleAnimation doubleAnimation2 = new DoubleAnimation
		{
			From = startOffset,
			To = 0.0,
			Duration = TransitionDuration,
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			},
			FillBehavior = FillBehavior.Stop
		};
		doubleAnimation2.Completed += delegate
		{
			if (token == transitionToken && string.Equals(currentPage, page, StringComparison.OrdinalIgnoreCase))
			{
				CompleteTransition(target, transform);
			}
		};
		target.BeginAnimation(UIElement.OpacityProperty, doubleAnimation, HandoffBehavior.SnapshotAndReplace);
		transform.BeginAnimation(TranslateTransform.YProperty, doubleAnimation2, HandoffBehavior.SnapshotAndReplace);
	}

	private void ReleaseBlurRefreshLease()
	{
		blurRefreshLease?.Dispose();
		blurRefreshLease = null;
	}

	private void ReleaseInteractionScope()
	{
		interactionScope?.Dispose();
		interactionScope = null;
	}

	private void CompleteTransition(FrameworkElement target, TranslateTransform transform)
	{
		target.BeginAnimation(UIElement.OpacityProperty, null);
		target.Opacity = 1.0;
		transform.BeginAnimation(TranslateTransform.YProperty, null);
		transform.Y = 0.0;
		ReleaseTransitionResources(target, requestFinalRefresh: true);
	}

	private void EnterTransitionGate()
	{
		if (!hasEnteredTransitionGate)
		{
			hasEnteredTransitionGate = true;
			UiTransitionGate.Enter();
		}
	}

	private void ExitTransitionGate()
	{
		if (hasEnteredTransitionGate)
		{
			hasEnteredTransitionGate = false;
			UiTransitionGate.Exit();
		}
	}

	private void CancelActiveTransition(bool requestFinalRefresh)
	{
		StopTransitionWatchdog();
		CancelPendingCompositionWait();
		ReleaseWarmupBusyProbe();
		FrameworkElement frameworkElement = activeTarget;
		if (frameworkElement == null)
		{
			ExitTransitionGate();
			ReleaseInteractionScope();
			ReleaseBlurRefreshLease();
			renderCacheScope?.Dispose();
			renderCacheScope = null;
		}
		else
		{
			frameworkElement.BeginAnimation(UIElement.OpacityProperty, null);
			frameworkElement.Opacity = 1.0;
			TranslateTransform translateTransform = EnsureTranslateTransform(frameworkElement);
			translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
			translateTransform.Y = 0.0;
			ReleaseTransitionResources(frameworkElement, requestFinalRefresh);
		}
	}

	private void ReleaseTransitionResources(FrameworkElement target, bool requestFinalRefresh)
	{
		StopTransitionWatchdog();
		ExitTransitionGate();
		target.Unloaded -= ActiveTarget_Unloaded;
		ReleaseInteractionScope();
		ReleaseBlurRefreshLease();
		bool flag = renderCacheScope?.IsActive ?? false;
		renderCacheScope?.Dispose();
		renderCacheScope = null;
		activeTarget = null;
		if (requestFinalRefresh & flag)
		{
			BackdropBlurRefreshCoordinator.RequestScopeRefresh(target);
		}
	}

	private void ActiveTarget_Unloaded(object sender, RoutedEventArgs e)
	{
		transitionToken++;
		CancelActiveTransition(requestFinalRefresh: false);
	}
}
