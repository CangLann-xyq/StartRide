using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StartRide.App.Diagnostics;

namespace StartRide.App.Controls;

internal sealed class BackdropBlurRefreshCoordinator
{
	private sealed class ContinuousRefreshLease(BackdropBlurRefreshCoordinator coordinator, FrameworkElement[] scopes) : IDisposable
	{
		private BackdropBlurRefreshCoordinator? owner = coordinator;

		public void Dispose()
		{
			Interlocked.Exchange(ref owner, null)?.ReleaseContinuousScopes(scopes);
		}
	}

	private sealed class EmptyLease : IDisposable
	{
		internal static readonly EmptyLease Instance = new EmptyLease();

		public void Dispose()
		{
		}
	}

	private sealed record Registration(FrameworkElement Source, ScrollViewer? ScrollViewer);

	private const string ImageBackgroundEnabledResourceKey = "Is.ImageBackground.ControlTint.Enabled";

	private const string SurfaceBlurEnabledResourceKey = "Is.Surface.BackdropBlur.Enabled";

	private const string SecondaryMenuBlurEnabledResourceKey = "Is.SecondaryMenu.BackdropBlur.Enabled";

	private static readonly ConditionalWeakTable<Window, BackdropBlurRefreshCoordinator> Coordinators = new ConditionalWeakTable<Window, BackdropBlurRefreshCoordinator>();

	private readonly Window window;

	private readonly HashSet<BackdropBlurBorder> registeredControls = new HashSet<BackdropBlurBorder>();

	private readonly HashSet<BackdropBlurBorder> dirtyControls = new HashSet<BackdropBlurBorder>();

	private readonly HashSet<BackdropBlurBorder> pendingScrollGeometryControls = new HashSet<BackdropBlurBorder>();

	private readonly Dictionary<BackdropBlurBorder, Registration> registrations = new Dictionary<BackdropBlurBorder, Registration>();

	private readonly Dictionary<FrameworkElement, HashSet<BackdropBlurBorder>> controlsBySource = new Dictionary<FrameworkElement, HashSet<BackdropBlurBorder>>();

	private readonly Dictionary<ScrollViewer, HashSet<BackdropBlurBorder>> controlsByScrollViewer = new Dictionary<ScrollViewer, HashSet<BackdropBlurBorder>>();

	private readonly Dictionary<FrameworkElement, int> continuousScopes = new Dictionary<FrameworkElement, int>();

	private bool isWindowObservationActive;

	private bool isRenderingSubscribed;

	private bool isClosed;

	private bool isLayoutCheckPending;

	private TimeSpan? lastRenderingTime;

	private long totalBatchCount;

	private long totalRefreshCount;

	private long continuousSessionStartedTimestamp;

	private long continuousSessionStartBatchCount;

	private long continuousSessionStartRefreshCount;

	private int continuousSessionScopeCount;

	internal int RegisteredCount => registeredControls.Count;

	internal int PendingCount => dirtyControls.Count;

	internal int ObservedSourceCount => controlsBySource.Count;

	internal int ObservedScrollViewerCount => controlsByScrollViewer.Count;

	internal bool IsWindowLayoutObserved => isWindowObservationActive;

	internal bool IsRenderingActive => isRenderingSubscribed;

	internal bool IsContinuousRenderingActive
	{
		get
		{
			if (isRenderingSubscribed)
			{
				return continuousScopes.Count > 0;
			}
			return false;
		}
	}

	internal long TotalBatchCount => totalBatchCount;

	internal long TotalRefreshCount => totalRefreshCount;

	private BackdropBlurRefreshCoordinator(Window window)
	{
		this.window = window;
	}

	internal int GetScrollViewerControlCount(ScrollViewer scrollViewer)
	{
		if (!controlsByScrollViewer.TryGetValue(scrollViewer, out HashSet<BackdropBlurBorder> value))
		{
			return 0;
		}
		return value.Count;
	}

	internal static BackdropBlurRefreshCoordinator? TryGet(FrameworkElement element)
	{
		Window window = Window.GetWindow((DependencyObject)(object)element);
		if (window != null)
		{
			return Coordinators.GetValue(window, (Window window2) => new BackdropBlurRefreshCoordinator(window2));
		}
		return null;
	}

	internal static IDisposable BeginContinuousRefresh(params FrameworkElement[] scopes)
	{
		FrameworkElement[] array = scopes.Where((FrameworkElement scope) => scope != null).Distinct().ToArray();
		if (array.Length == 0)
		{
			return EmptyLease.Instance;
		}
		Window owner = Window.GetWindow((DependencyObject)(object)array[0]);
		if (owner == null)
		{
			return EmptyLease.Instance;
		}
		BackdropBlurRefreshCoordinator value = Coordinators.GetValue(owner, (Window window) => new BackdropBlurRefreshCoordinator(window));
		FrameworkElement[] array2 = array.Where((FrameworkElement scope) => Window.GetWindow((DependencyObject)(object)scope) == owner).ToArray();
		if (array2.Length == 0)
		{
			return EmptyLease.Instance;
		}
		value.AcquireContinuousScopes(array2);
		return new ContinuousRefreshLease(value, array2);
	}

	internal static bool HasActiveImageBackdropBlur(params FrameworkElement[] scopes)
	{
		FrameworkElement[] array = scopes.Where((FrameworkElement scope) => scope != null).Distinct().ToArray();
		if (array.Length == 0 || !IsImageControlBlurEnabled(array[0]))
		{
			return false;
		}
		Window owner = Window.GetWindow((DependencyObject)(object)array[0]);
		if (owner == null || !Coordinators.TryGetValue(owner, out BackdropBlurRefreshCoordinator value))
		{
			return false;
		}
		FrameworkElement[] ownedScopes = array.Where((FrameworkElement scope) => Window.GetWindow((DependencyObject)(object)scope) == owner).ToArray();
		if (ownedScopes.Length != 0)
		{
			return value.registeredControls.Any((BackdropBlurBorder control) => control.IsRefreshEligible && IsInsideAnyScope(control, ownedScopes));
		}
		return false;
	}

	internal static void RequestScopeRefresh(params FrameworkElement[] scopes)
	{
		FrameworkElement[] array = scopes.Where((FrameworkElement scope) => scope != null).Distinct().ToArray();
		if (array.Length == 0)
		{
			return;
		}
		Window owner = Window.GetWindow((DependencyObject)(object)array[0]);
		if (owner != null && Coordinators.TryGetValue(owner, out BackdropBlurRefreshCoordinator value))
		{
			value.QueueScopeRefresh(array.Where((FrameworkElement scope) => Window.GetWindow((DependencyObject)(object)scope) == owner));
		}
	}

	internal void Register(BackdropBlurBorder control, FrameworkElement source, ScrollViewer? scrollViewer)
	{
		if (isClosed)
		{
			return;
		}
		if (registrations.TryGetValue(control, out Registration value))
		{
			if (value.Source == source && value.ScrollViewer == scrollViewer)
			{
				return;
			}
			RemoveRegistration(control, value);
		}
		else
		{
			registeredControls.Add(control);
		}
		Registration value2 = new Registration(source, scrollViewer);
		registrations[control] = value2;
		AddSourceRegistration(source, control);
		if (scrollViewer != null)
		{
			AddScrollViewerRegistration(scrollViewer, control);
		}
		EnsureWindowObservation();
		UpdateRenderingSubscription();
	}

	internal void Unregister(BackdropBlurBorder control)
	{
		if (registrations.Remove(control, out Registration value))
		{
			RemoveRegistration(control, value);
		}
		registeredControls.Remove(control);
		dirtyControls.Remove(control);
		pendingScrollGeometryControls.Remove(control);
		if (registeredControls.Count == 0)
		{
			isLayoutCheckPending = false;
			pendingScrollGeometryControls.Clear();
			StopWindowObservation();
		}
		UpdateRenderingSubscription();
	}

	internal void RequestRefresh(BackdropBlurBorder control, BackdropBlurRefreshReason reason)
	{
		if (registeredControls.Contains(control))
		{
			AddDirty(control);
			UpdateRenderingSubscription();
		}
	}

	internal void ProcessPendingBatchForTesting()
	{
		ProcessRenderFrame(GetNextTestingRenderingTime());
	}

	internal void ProcessContinuousFrameForTesting()
	{
		ProcessRenderFrame(GetNextTestingRenderingTime());
	}

	internal void ProcessRenderFrameForTesting(TimeSpan renderingTime)
	{
		ProcessRenderFrame(renderingTime);
	}

	internal void ProcessLayoutUpdatedForTesting()
	{
		ProcessLayoutUpdated();
	}

	internal void ProcessScrollChangedForTesting(ScrollViewer scrollViewer)
	{
		ProcessScrollChanged(scrollViewer);
	}

	private TimeSpan GetNextTestingRenderingTime()
	{
		return (lastRenderingTime ?? TimeSpan.Zero) + TimeSpan.FromTicks(1L);
	}

	private void AddDirty(BackdropBlurBorder control)
	{
		dirtyControls.Add(control);
	}

	private void AddDirtyRange(IEnumerable<BackdropBlurBorder> controls)
	{
		foreach (BackdropBlurBorder control in controls)
		{
			if (registeredControls.Contains(control))
			{
				AddDirty(control);
			}
		}
		UpdateRenderingSubscription();
	}

	private void QueueScopeRefresh(IEnumerable<FrameworkElement> scopes)
	{
		FrameworkElement[] array = scopes.ToArray();
		if (array.Length == 0 || registeredControls.Count == 0)
		{
			return;
		}
		foreach (BackdropBlurBorder registeredControl in registeredControls)
		{
			if (registeredControl.IsRefreshEligible && IsInsideAnyScope(registeredControl, array))
			{
				registeredControl.InvalidatePreparedGeometry();
				AddDirty(registeredControl);
			}
		}
		UpdateRenderingSubscription();
	}

	private void ProcessRenderFrame(TimeSpan renderingTime)
	{
		if (lastRenderingTime == renderingTime)
		{
			return;
		}
		lastRenderingTime = renderingTime;
		if (isLayoutCheckPending || pendingScrollGeometryControls.Count > 0)
		{
			bool includeRemainingLayoutControls = isLayoutCheckPending;
			isLayoutCheckPending = false;
			ProcessPendingGeometryChecks(includeRemainingLayoutControls);
		}
		if (continuousScopes.Count > 0 && registeredControls.Count > 0)
		{
			foreach (BackdropBlurBorder registeredControl in registeredControls)
			{
				if (registeredControl.IsRefreshEligible && IsInsideContinuousScope(registeredControl))
				{
					registeredControl.InvalidatePreparedGeometry();
					AddDirty(registeredControl);
				}
			}
		}
		if (dirtyControls.Count > 0)
		{
			BackdropBlurBorder[] controls = dirtyControls.ToArray();
			dirtyControls.Clear();
			ProcessBatch(controls);
		}
		UpdateRenderingSubscription();
	}

	private void ProcessBatch(IReadOnlyList<BackdropBlurBorder> controls)
	{
		totalBatchCount++;
		foreach (BackdropBlurBorder control in controls)
		{
			if (registeredControls.Contains(control) && control.IsRefreshEligible)
			{
				control.RefreshBackdrop();
				totalRefreshCount++;
			}
		}
	}

	private void AcquireContinuousScopes(IEnumerable<FrameworkElement> scopes)
	{
		if (isClosed)
		{
			return;
		}
		if (continuousScopes.Count == 0)
		{
			BeginContinuousSession();
		}
		foreach (FrameworkElement scope in scopes)
		{
			continuousScopes.TryGetValue(scope, out var value);
			continuousScopes[scope] = value + 1;
		}
		continuousSessionScopeCount = Math.Max(continuousSessionScopeCount, continuousScopes.Count);
		UpdateRenderingSubscription();
	}

	private void ReleaseContinuousScopes(IEnumerable<FrameworkElement> scopes)
	{
		foreach (FrameworkElement scope in scopes)
		{
			if (continuousScopes.TryGetValue(scope, out var value))
			{
				if (value <= 1)
				{
					continuousScopes.Remove(scope);
				}
				else
				{
					continuousScopes[scope] = value - 1;
				}
			}
		}
		if (continuousScopes.Count == 0)
		{
			EndContinuousSession();
		}
		UpdateRenderingSubscription();
	}

	private void BeginContinuousSession()
	{
		continuousSessionStartedTimestamp = Stopwatch.GetTimestamp();
		continuousSessionStartBatchCount = totalBatchCount;
		continuousSessionStartRefreshCount = totalRefreshCount;
		continuousSessionScopeCount = 0;
	}

	private void EndContinuousSession()
	{
		if (continuousSessionStartedTimestamp != 0L)
		{
			double totalMilliseconds = Stopwatch.GetElapsedTime(continuousSessionStartedTimestamp).TotalMilliseconds;
			continuousSessionStartedTimestamp = 0L;
			UiPerformanceLog.LogContinuousBackdropRefresh(totalMilliseconds, continuousSessionScopeCount, totalBatchCount - continuousSessionStartBatchCount, totalRefreshCount - continuousSessionStartRefreshCount);
		}
	}

	private void UpdateRenderingSubscription()
	{
		bool flag = !isClosed && registeredControls.Count > 0 && (dirtyControls.Count > 0 || continuousScopes.Count > 0 || isLayoutCheckPending || pendingScrollGeometryControls.Count > 0);
		if (flag != isRenderingSubscribed)
		{
			if (flag)
			{
				CompositionTarget.Rendering += CompositionTarget_Rendering;
			}
			else
			{
				CompositionTarget.Rendering -= CompositionTarget_Rendering;
			}
			isRenderingSubscribed = flag;
		}
	}

	private void CompositionTarget_Rendering(object? sender, EventArgs e)
	{
		TimeSpan renderingTime = ((e is RenderingEventArgs e2) ? e2.RenderingTime : GetNextTestingRenderingTime());
		ProcessRenderFrame(renderingTime);
	}

	private bool IsInsideContinuousScope(BackdropBlurBorder control)
	{
		foreach (FrameworkElement key in continuousScopes.Keys)
		{
			if (key.IsVisible && (key == control || key.IsAncestorOf((DependencyObject)(object)control)))
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsInsideAnyScope(BackdropBlurBorder control, IReadOnlyList<FrameworkElement> scopes)
	{
		foreach (FrameworkElement scope in scopes)
		{
			if (scope.IsVisible && (scope == control || scope.IsAncestorOf((DependencyObject)(object)control)))
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsImageControlBlurEnabled(FrameworkElement scope)
	{
		object obj = scope.TryFindResource("Is.ImageBackground.ControlTint.Enabled");
		if (obj is bool && (bool)obj)
		{
			obj = scope.TryFindResource("Is.Surface.BackdropBlur.Enabled");
			if (!(obj is bool) || !(bool)obj)
			{
				obj = scope.TryFindResource("Is.SecondaryMenu.BackdropBlur.Enabled");
				if (obj is bool)
				{
					return (bool)obj;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private void EnsureWindowObservation()
	{
		if (!isWindowObservationActive)
		{
			window.LayoutUpdated += Window_LayoutUpdated;
			window.Closed += Window_Closed;
			isWindowObservationActive = true;
		}
	}

	private void StopWindowObservation()
	{
		if (isWindowObservationActive)
		{
			window.LayoutUpdated -= Window_LayoutUpdated;
			window.Closed -= Window_Closed;
			isWindowObservationActive = false;
		}
	}

	private void Window_LayoutUpdated(object? sender, EventArgs e)
	{
		ProcessLayoutUpdated();
	}

	private void ProcessLayoutUpdated()
	{
		if (registeredControls.Count != 0)
		{
			isLayoutCheckPending = true;
			UpdateRenderingSubscription();
		}
	}

	private void ProcessPendingGeometryChecks(bool includeRemainingLayoutControls)
	{
		HashSet<BackdropBlurBorder> hashSet = pendingScrollGeometryControls.ToHashSet();
		pendingScrollGeometryControls.Clear();
		foreach (BackdropBlurBorder item in hashSet)
		{
			CheckGeometryAndQueueRefresh(item);
		}
		if (!includeRemainingLayoutControls)
		{
			return;
		}
		foreach (BackdropBlurBorder registeredControl in registeredControls)
		{
			if (!hashSet.Contains(registeredControl))
			{
				CheckGeometryAndQueueRefresh(registeredControl);
			}
		}
	}

	private void CheckGeometryAndQueueRefresh(BackdropBlurBorder control)
	{
		if (registeredControls.Contains(control) && control.IsRefreshEligible && control.PrepareLayoutGeometryRefresh())
		{
			AddDirty(control);
		}
	}

	private void Window_Closed(object? sender, EventArgs e)
	{
		Shutdown();
	}

	private void Shutdown()
	{
		if (isClosed)
		{
			return;
		}
		isClosed = true;
		StopWindowObservation();
		if (isRenderingSubscribed)
		{
			CompositionTarget.Rendering -= CompositionTarget_Rendering;
			isRenderingSubscribed = false;
		}
		foreach (FrameworkElement key in controlsBySource.Keys)
		{
			key.SizeChanged -= Source_SizeChanged;
		}
		foreach (ScrollViewer key2 in controlsByScrollViewer.Keys)
		{
			key2.ScrollChanged -= ScrollViewer_ScrollChanged;
		}
		controlsBySource.Clear();
		controlsByScrollViewer.Clear();
		registrations.Clear();
		registeredControls.Clear();
		dirtyControls.Clear();
		pendingScrollGeometryControls.Clear();
		continuousScopes.Clear();
		isLayoutCheckPending = false;
		Coordinators.Remove(window);
	}

	private void AddSourceRegistration(FrameworkElement source, BackdropBlurBorder control)
	{
		if (!controlsBySource.TryGetValue(source, out HashSet<BackdropBlurBorder> value))
		{
			value = new HashSet<BackdropBlurBorder>();
			controlsBySource[source] = value;
			source.SizeChanged += Source_SizeChanged;
		}
		value.Add(control);
	}

	private void RemoveSourceRegistration(FrameworkElement source, BackdropBlurBorder control)
	{
		if (controlsBySource.TryGetValue(source, out HashSet<BackdropBlurBorder> value))
		{
			value.Remove(control);
			if (value.Count <= 0)
			{
				source.SizeChanged -= Source_SizeChanged;
				controlsBySource.Remove(source);
			}
		}
	}

	private void AddScrollViewerRegistration(ScrollViewer scrollViewer, BackdropBlurBorder control)
	{
		if (!controlsByScrollViewer.TryGetValue(scrollViewer, out HashSet<BackdropBlurBorder> value))
		{
			value = new HashSet<BackdropBlurBorder>();
			controlsByScrollViewer[scrollViewer] = value;
			scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
		}
		value.Add(control);
	}

	private void RemoveScrollViewerRegistration(ScrollViewer scrollViewer, BackdropBlurBorder control)
	{
		if (controlsByScrollViewer.TryGetValue(scrollViewer, out HashSet<BackdropBlurBorder> value))
		{
			value.Remove(control);
			if (value.Count <= 0)
			{
				scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
				controlsByScrollViewer.Remove(scrollViewer);
			}
		}
	}

	private void RemoveRegistration(BackdropBlurBorder control, Registration registration)
	{
		RemoveSourceRegistration(registration.Source, control);
		if (registration.ScrollViewer != null)
		{
			RemoveScrollViewerRegistration(registration.ScrollViewer, control);
		}
	}

	private void Source_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (!(sender is FrameworkElement key) || !controlsBySource.TryGetValue(key, out HashSet<BackdropBlurBorder> value))
		{
			return;
		}
		foreach (BackdropBlurBorder item in value)
		{
			item.InvalidatePreparedGeometry();
		}
		AddDirtyRange(value);
	}

	private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			ProcessScrollChanged(scrollViewer);
		}
	}

	private void ProcessScrollChanged(ScrollViewer scrollViewer)
	{
		if (!controlsByScrollViewer.TryGetValue(scrollViewer, out HashSet<BackdropBlurBorder> value))
		{
			return;
		}
		foreach (BackdropBlurBorder item in value)
		{
			if (registeredControls.Contains(item))
			{
				pendingScrollGeometryControls.Add(item);
			}
		}
		UpdateRenderingSubscription();
	}
}
