using System.Threading;

namespace StartRide.App.Diagnostics;

internal static class DisplayFrameIntervalEstimator
{
	internal const double DefaultIntervalMs = 16.666666666666668;

	private const double MinimumIntervalMs = 4.0;

	private const double MaximumIntervalMs = 41.666666666666664;

	private static double currentIntervalMs = 16.666666666666668;

	internal static double CurrentIntervalMs => Volatile.Read(in currentIntervalMs);

	internal static void Observe(double intervalMs)
	{
		if (!double.IsNaN(intervalMs) && !(intervalMs < 4.0) && !(intervalMs > 41.666666666666664) && intervalMs < Volatile.Read(in currentIntervalMs))
		{
			Volatile.Write(ref currentIntervalMs, intervalMs);
		}
	}

	internal static void ResetForTesting()
	{
		Volatile.Write(ref currentIntervalMs, 16.666666666666668);
	}
}
