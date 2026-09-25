using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using Serilog;

namespace StartRide.App.Diagnostics;

internal sealed class UiThreadStallMonitor : IDisposable
{
	internal static readonly TimeSpan ProbeInterval = TimeSpan.FromMilliseconds(500.0);

	internal const double StallThresholdMs = 200.0;

	private readonly DispatcherTimer timer;

	private long lastTickTimestamp;

	private long stallCount;

	private double worstStallMs;

	private bool isDisposed;

	internal long StallCount => Interlocked.Read(in stallCount);

	internal double WorstStallMs => worstStallMs;

	internal UiThreadStallMonitor(Dispatcher dispatcher)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		ArgumentNullException.ThrowIfNull(dispatcher, "dispatcher");
		timer = new DispatcherTimer((DispatcherPriority)4, dispatcher)
		{
			Interval = ProbeInterval
		};
		timer.Tick += Timer_Tick;
	}

	internal void Start()
	{
		if (!isDisposed && !timer.IsEnabled && UiPerformanceLog.IsEnabled)
		{
			lastTickTimestamp = Stopwatch.GetTimestamp();
			timer.Start();
		}
	}

	public void Dispose()
	{
		if (!isDisposed)
		{
			isDisposed = true;
			timer.Stop();
			timer.Tick -= Timer_Tick;
		}
	}

	internal static double CalculateStallMs(double elapsedMs, double probeIntervalMs)
	{
		return Math.Max(0.0, elapsedMs - probeIntervalMs);
	}

	private void Timer_Tick(object? sender, EventArgs e)
	{
		double totalMilliseconds = Stopwatch.GetElapsedTime(lastTickTimestamp).TotalMilliseconds;
		lastTickTimestamp = Stopwatch.GetTimestamp();
		if (!UiPerformanceLog.IsEnabled)
		{
			return;
		}
		double num = CalculateStallMs(totalMilliseconds, ProbeInterval.TotalMilliseconds);
		if (!(num < 200.0))
		{
			Interlocked.Increment(ref stallCount);
			if (num > worstStallMs)
			{
				worstStallMs = num;
			}
			Log.Warning("UI thread stalled; animations and scrolling were blocked. StallMs={StallMs:F0} ProbeIntervalMs={ProbeIntervalMs:F0} StallCount={StallCount} WorstStallMs={WorstStallMs:F0}", num, ProbeInterval.TotalMilliseconds, Interlocked.Read(in stallCount), worstStallMs);
		}
	}
}
