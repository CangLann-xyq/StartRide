using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Launcher.Application.Services;
using Serilog;
using Serilog.Events;

namespace Launcher.App.Diagnostics;

internal static class UiPerformanceLog
{
	private static readonly ConcurrentDictionary<string, byte> LoggedFallbacks = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

	private static readonly HashSet<ScrollViewer> LoggedScrollSurfaces = new HashSet<ScrollViewer>();

	private static readonly HashSet<FrameworkElement> LoggedPageSurfaces = new HashSet<FrameworkElement>();

	private static int hasLoggedRenderEnvironment;

	internal const string InstrumentationVariableName = "STARTRIDE_UI_PERF_DIAG";

	private static readonly bool IsInstrumentationRequested = IsTruthy(Environment.GetEnvironmentVariable("STARTRIDE_UI_PERF_DIAG"));

	internal static bool IsEnabled
	{
		get
		{
			if (IsInstrumentationRequested)
			{
				return Log.IsEnabled(LogEventLevel.Debug);
			}
			return false;
		}
	}

	internal static bool IsTruthy(string? value)
	{
		string text = value?.Trim();
		if (text != null)
		{
			if (!text.Equals("1", StringComparison.Ordinal) && !text.Equals("true", StringComparison.OrdinalIgnoreCase) && !text.Equals("yes", StringComparison.OrdinalIgnoreCase))
			{
				return text.Equals("on", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	internal static UiInteractionScope BeginInteraction(string kind, string detail, FrameworkElement? layoutProbe = null)
	{
		return new UiInteractionScope(kind, detail, IsEnabled && HasUiThreadAccess(), layoutProbe);
	}

	internal static void LogScrollSurface(ScrollViewer scrollViewer, string detail)
	{
		if (IsEnabled && LoggedScrollSurfaces.Add(scrollViewer))
		{
			object content = scrollViewer.Content;
			DependencyObject val = (DependencyObject)((content is DependencyObject) ? content : null);
			VisualTreeSize visualTreeSize = VisualTreeStats.Measure((DependencyObject?)(object)scrollViewer);
			Log.Debug("Scroll surface inspected. Detail={ScrollDetail} CanContentScroll={CanContentScroll} ContentPanel={ContentPanel} ExtentHeight={ExtentHeight:F0} ViewportHeight={ViewportHeight:F0} ScrollableHeight={ScrollableHeight:F0} VisualElements={VisualElementCount} VisualDepth={VisualDepth} TreeTruncated={TreeTruncated} Effects={EffectCount} VisibleEffects={VisibleEffectCount} DropShadows={DropShadowCount} CardShadows={CardShadowCount} EffectBreakdown={EffectBreakdown} VisibleEffectHosts={VisibleEffectHosts}", detail, scrollViewer.CanContentScroll, ((object)val)?.GetType().Name ?? "<none>", scrollViewer.ExtentHeight, scrollViewer.ViewportHeight, scrollViewer.ScrollableHeight, visualTreeSize.ElementCount, visualTreeSize.MaxDepth, visualTreeSize.IsTruncated, visualTreeSize.EffectCount, visualTreeSize.VisibleEffectCount, visualTreeSize.DropShadowCount, visualTreeSize.CardShadowCount, visualTreeSize.EffectBreakdown, visualTreeSize.VisibleEffectHosts);
		}
	}

	internal static void LogPageSurface(FrameworkElement page, string detail)
	{
		if (IsEnabled && LoggedPageSurfaces.Add(page))
		{
			VisualTreeSize visualTreeSize = VisualTreeStats.Measure((DependencyObject?)(object)page);
			Log.Debug("Page surface inspected. Page={PageDetail} Width={SurfaceWidth:F0} Height={SurfaceHeight:F0} Megapixels={SurfaceMegapixels:F3} VisualElements={VisualElementCount} VisualDepth={VisualDepth} TreeTruncated={TreeTruncated} Effects={EffectCount} VisibleEffects={VisibleEffectCount} DropShadows={DropShadowCount} CardShadows={CardShadowCount} EffectBreakdown={EffectBreakdown}", detail, page.ActualWidth, page.ActualHeight, page.ActualWidth * page.ActualHeight / 1000000.0, visualTreeSize.ElementCount, visualTreeSize.MaxDepth, visualTreeSize.IsTruncated, visualTreeSize.EffectCount, visualTreeSize.VisibleEffectCount, visualTreeSize.DropShadowCount, visualTreeSize.CardShadowCount, visualTreeSize.EffectBreakdown);
		}
	}

	internal static void LogRenderEnvironment(SystemMemorySnapshot? memorySnapshot, Visual? dpiSource)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		if (Log.IsEnabled(LogEventLevel.Debug) && Interlocked.Exchange(ref hasLoggedRenderEnvironment, 1) == 0)
		{
			double? num = TryGetDpiScale(dpiSource);
			object[] obj = new object[11]
			{
				RenderCapability.Tier >> 16,
				RenderOptions.ProcessRenderMode,
				RenderCapability.IsPixelShaderVersionSupported(3, 0),
				null,
				null,
				null,
				null,
				null,
				null,
				null,
				null
			};
			Size maxHardwareTextureSize = RenderCapability.MaxHardwareTextureSize;
			obj[3] = (int)maxHardwareTextureSize.Width;
			maxHardwareTextureSize = RenderCapability.MaxHardwareTextureSize;
			obj[4] = (int)maxHardwareTextureSize.Height;
			obj[5] = num?.ToString("F2") ?? "unknown";
			obj[6] = Environment.ProcessorCount;
			obj[7] = (((object)memorySnapshot == null) ? (-1) : (memorySnapshot.TotalMemoryBytes / 1048576));
			obj[8] = (((object)memorySnapshot == null) ? (-1) : (memorySnapshot.AvailableMemoryBytes / 1048576));
			obj[9] = RuntimeInformation.ProcessArchitecture;
			obj[10] = RuntimeInformation.OSDescription;
			Log.Debug("Render environment evaluated. RenderTier={RenderTier} ProcessRenderMode={ProcessRenderMode} PixelShader30Supported={PixelShader30Supported} MaxTextureSize={MaxTextureWidth}x{MaxTextureHeight} DpiScale={DpiScale} ProcessorCount={ProcessorCount} TotalMemoryMb={TotalMemoryMb} AvailableMemoryMb={AvailableMemoryMb} ProcessArchitecture={ProcessArchitecture} OsDescription={OsDescription}", obj);
		}
	}

	internal static void LogTransitionRenderCacheFallback(string transitionKind, string fallbackReason, long estimatedBytes)
	{
		if (IsEnabled && LoggedFallbacks.TryAdd(transitionKind + "|" + fallbackReason, 0))
		{
			Log.Debug("Transition render cache unavailable; falling back to live rendering. TransitionKind={TransitionKind} Reason={FallbackReason} EstimatedBytes={EstimatedBytes} RenderTier={RenderTier}", transitionKind, fallbackReason, estimatedBytes, RenderCapability.Tier >> 16);
		}
	}

	internal static void LogContinuousBackdropRefresh(double durationMs, int scopeCount, long batchCount, long refreshCount)
	{
		if (IsEnabled)
		{
			Log.Debug("Continuous backdrop refresh completed. DurationMs={DurationMs:F1} ScopeCount={ScopeCount} BatchCount={BatchCount} RefreshCount={RefreshCount} RefreshPerBatch={RefreshPerBatch:F2}", durationMs, scopeCount, batchCount, refreshCount, (batchCount == 0L) ? 0.0 : ((double)refreshCount / (double)batchCount));
		}
	}

	internal static void ResetForTesting()
	{
		Interlocked.Exchange(ref hasLoggedRenderEnvironment, 0);
		LoggedFallbacks.Clear();
		LoggedScrollSurfaces.Clear();
	}

	private static bool HasUiThreadAccess()
	{
		System.Windows.Application current = System.Windows.Application.Current;
		if (current == null)
		{
			return false;
		}
		return ((DispatcherObject)current).Dispatcher.CheckAccess();
	}

	private static double? TryGetDpiScale(Visual? dpiSource)
	{
		if (dpiSource == null)
		{
			return null;
		}
		try
		{
			return VisualTreeHelper.GetDpi(dpiSource).DpiScaleX;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}
}
