using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;

namespace Launcher.App.Diagnostics;

internal sealed class UiInteractionScope : IDisposable
{
	private readonly string kind;

	private readonly string detail;

	private readonly bool isSampling;

	private const double LongFrameCycleFactor = 3.0;

	private readonly FrameTimeAccumulator? accumulator;

	private readonly DispatcherBusyProbe? busyProbe;

	private readonly FrameworkElement? layoutProbe;

	private readonly long startedAtTimestamp;

	private int layoutPassCount;

	private (long Batches, long Refreshes) backdropStart;

	private (int Gen0, int Gen1, int Gen2) gcStart;

	private double scrollOffsetLast;

	private double scrollDistanceTotal;

	private bool hasScrollBaseline;

	private bool isDisposed;

	internal string RenderPath { get; set; } = "Unknown";

	internal double WarmupMs { get; set; }

	internal double WarmupBusyMs { get; set; }

	internal double WarmupWorstOperationMs { get; set; }

	internal string WarmupWorstOperation { get; set; } = "none";

	internal double SurfaceWidth { get; set; }

	internal double SurfaceHeight { get; set; }

	internal Func<(long Batches, long Refreshes)>? BackdropCounterReader { get; set; }

	internal int BackdropControlCount { get; set; }

	internal bool HasAncestorBitmapCache { get; set; }

	internal bool HasOpacityMask { get; set; }

	internal Func<double>? ScrollOffsetReader { get; set; }

	internal int SampledFrameCount => accumulator?.FrameCount ?? 0;

	internal UiInteractionScope(string kind, string detail, bool isSampling, FrameworkElement? layoutProbe = null)
	{
		this.kind = kind;
		this.detail = detail;
		this.isSampling = isSampling;
		if (isSampling)
		{
			accumulator = new FrameTimeAccumulator();
			System.Windows.Application current = System.Windows.Application.Current;
			busyProbe = DispatcherBusyProbe.TryAttach((current != null) ? ((DispatcherObject)current).Dispatcher : null);
			gcStart = (Gen0: GC.CollectionCount(0), Gen1: GC.CollectionCount(1), Gen2: GC.CollectionCount(2));
			startedAtTimestamp = Stopwatch.GetTimestamp();
			CompositionTarget.Rendering += CompositionTarget_Rendering;
			this.layoutProbe = layoutProbe;
			if (layoutProbe != null)
			{
				layoutProbe.LayoutUpdated += LayoutProbe_LayoutUpdated;
			}
		}
	}

	internal void CaptureBackdropBaseline()
	{
		backdropStart = BackdropCounterReader?.Invoke() ?? (0L, 0L);
		if (ScrollOffsetReader != null)
		{
			scrollOffsetLast = ScrollOffsetReader();
			hasScrollBaseline = true;
		}
	}

	public void Dispose()
	{
		if (isDisposed || !isSampling)
		{
			return;
		}
		isDisposed = true;
		CompositionTarget.Rendering -= CompositionTarget_Rendering;
		if (layoutProbe != null)
		{
			layoutProbe.LayoutUpdated -= LayoutProbe_LayoutUpdated;
		}
		busyProbe?.Dispose();
		if (accumulator != null)
		{
			double totalMilliseconds = Stopwatch.GetElapsedTime(startedAtTimestamp).TotalMilliseconds;
			(long, long) tuple = BackdropCounterReader?.Invoke() ?? (0L, 0L);
			AccumulateScrollDistance();
			double num = scrollDistanceTotal;
			if (accumulator.FrameCount != 0)
			{
				Log.Debug("UI interaction frames sampled. Kind={InteractionKind} Detail={InteractionDetail} RenderPath={RenderPath} DurationMs={DurationMs:F1} FrameCount={FrameCount} Fps={FramesPerSecond:F1} AvgFrameMs={AverageFrameMs:F2} P95FrameMs={P95FrameMs:F2} MaxFrameMs={MaxFrameMs:F2} JankFrames={JankFrameCount} DisplayFrameMs={DisplayFrameMs:F2} LayoutPasses={LayoutPassCount} LayoutPerFrame={LayoutPerFrame:F2} SurfaceWidth={SurfaceWidth:F0} SurfaceHeight={SurfaceHeight:F0} SurfaceMegapixels={SurfaceMegapixels:F3} BackdropControls={BackdropControlCount} BackdropBatches={BackdropBatches} BackdropRefreshes={BackdropRefreshes} BackdropRefreshPerFrame={BackdropRefreshPerFrame:F2} BitmapCache={HasAncestorBitmapCache} OpacityMask={HasOpacityMask} Cycle1={Cycle1} Cycle2={Cycle2} Cycle3={Cycle3} Cycle4Plus={Cycle4Plus} LongRun={MaxConsecutiveLongFrames} Gc0={Gc0} Gc1={Gc1} Gc2={Gc2} ScrollPx={ScrollPx:F0} ScrollPxPerSec={ScrollPxPerSec:F0} UiBusyMs={UiBusyMs:F1} UiBusyShare={UiBusyShare:F2} DispatcherOps={DispatcherOperationCount} WorstOpMs={WorstOperationMs:F1} WorstOp={WorstOperation} WarmupMs={WarmupMs:F1} WarmupBusyMs={WarmupBusyMs:F1} WarmupWorstOpMs={WarmupWorstOperationMs:F1} WarmupWorstOp={WarmupWorstOperation}", kind, detail, RenderPath, totalMilliseconds, accumulator.FrameCount, accumulator.EstimatedFramesPerSecond, accumulator.AverageIntervalMs, accumulator.GetPercentileIntervalMs(95.0), accumulator.MaxIntervalMs, accumulator.JankFrameCount, DisplayFrameIntervalEstimator.CurrentIntervalMs, layoutPassCount, (accumulator.FrameCount == 0) ? 0.0 : ((double)layoutPassCount / (double)accumulator.FrameCount), SurfaceWidth, SurfaceHeight, SurfaceWidth * SurfaceHeight / 1000000.0, BackdropControlCount, tuple.Item1 - backdropStart.Batches, tuple.Item2 - backdropStart.Refreshes, (accumulator.FrameCount == 0) ? 0.0 : ((double)(tuple.Item2 - backdropStart.Refreshes) / (double)accumulator.FrameCount), HasAncestorBitmapCache, HasOpacityMask, accumulator.GetCycleFrameCount(1), accumulator.GetCycleFrameCount(2), accumulator.GetCycleFrameCount(3), accumulator.GetCycleFrameCount(4), accumulator.MaxConsecutiveLongFrames, GC.CollectionCount(0) - gcStart.Gen0, GC.CollectionCount(1) - gcStart.Gen1, GC.CollectionCount(2) - gcStart.Gen2, num, (totalMilliseconds <= 0.0) ? 0.0 : (num * 1000.0 / totalMilliseconds), busyProbe?.TotalBusyMs ?? 0.0, (totalMilliseconds <= 0.0) ? 0.0 : ((busyProbe?.TotalBusyMs ?? 0.0) / totalMilliseconds), busyProbe?.TotalOperationCount ?? 0, busyProbe?.WorstOperationMs ?? 0.0, busyProbe?.WorstOperationDetail ?? "none", WarmupMs, WarmupBusyMs, WarmupWorstOperationMs, WarmupWorstOperation);
			}
		}
	}

	private void LayoutProbe_LayoutUpdated(object? sender, EventArgs e)
	{
		layoutPassCount++;
	}

	private void CompositionTarget_Rendering(object? sender, EventArgs e)
	{
		if (e is RenderingEventArgs e2)
		{
			double num = accumulator?.AddRenderingTime(e2.RenderingTime) ?? 0.0;
			if (num > 0.0)
			{
				LogLongFrame(num);
			}
		}
		busyProbe?.ResetFrame();
		AccumulateScrollDistance();
	}

	private void LogLongFrame(double intervalMs)
	{
		if (busyProbe != null)
		{
			double currentIntervalMs = DisplayFrameIntervalEstimator.CurrentIntervalMs;
			if (!(intervalMs <= currentIntervalMs * 3.0))
			{
				Log.Debug("UI long frame observed. Kind={InteractionKind} Detail={InteractionDetail} FrameMs={FrameMs:F1} DisplayFrameMs={DisplayFrameMs:F2} Cycles={FrameCycles} UiBusyMs={UiBusyMs:F1} UiBusyShare={UiBusyShare:F2} DispatcherOps={DispatcherOperationCount} LongestOpMs={LongestOperationMs:F1} LongestOp={LongestOperation}", kind, detail, intervalMs, currentIntervalMs, (!(currentIntervalMs <= 0.0)) ? ((int)Math.Round(intervalMs / currentIntervalMs)) : 0, busyProbe.FrameBusyMs, busyProbe.FrameBusyMs / intervalMs, busyProbe.FrameOperationCount, busyProbe.FrameLongestOperationMs, busyProbe.FrameLongestOperationDetail);
			}
		}
	}

	private void AccumulateScrollDistance()
	{
		if (hasScrollBaseline && ScrollOffsetReader != null)
		{
			double num = ScrollOffsetReader();
			scrollDistanceTotal += Math.Abs(num - scrollOffsetLast);
			scrollOffsetLast = num;
		}
	}
}
