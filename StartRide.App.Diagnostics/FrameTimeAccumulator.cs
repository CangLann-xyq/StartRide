using System;

namespace StartRide.App.Diagnostics;

internal sealed class FrameTimeAccumulator
{
	internal const int MaximumSampledIntervals = 512;

	private const double JankIntervalFactor = 1.5;

	private readonly double[] sampledIntervalsMs = new double[512];

	private readonly int[] cycleBuckets = new int[5];

	private int sampledCount;

	private int consecutiveLongFrames;

	private TimeSpan? lastRenderingTime;

	internal int FrameCount { get; private set; }

	internal int JankFrameCount { get; private set; }

	internal double MaxIntervalMs { get; private set; }

	internal double TotalIntervalMs { get; private set; }

	internal int MaxConsecutiveLongFrames { get; private set; }

	internal bool IsSampleBufferSaturated => sampledCount >= 512;

	internal double AverageIntervalMs
	{
		get
		{
			if (FrameCount != 0)
			{
				return TotalIntervalMs / (double)FrameCount;
			}
			return 0.0;
		}
	}

	internal double EstimatedFramesPerSecond
	{
		get
		{
			if (!(TotalIntervalMs <= 0.0))
			{
				return (double)FrameCount * 1000.0 / TotalIntervalMs;
			}
			return 0.0;
		}
	}

	internal int GetCycleFrameCount(int cycles)
	{
		if (cycles < 1 || cycles >= cycleBuckets.Length)
		{
			return 0;
		}
		return cycleBuckets[cycles];
	}

	internal double AddRenderingTime(TimeSpan renderingTime)
	{
		TimeSpan? timeSpan = lastRenderingTime;
		if (timeSpan.HasValue)
		{
			TimeSpan valueOrDefault = timeSpan.GetValueOrDefault();
			if (renderingTime <= valueOrDefault)
			{
				return 0.0;
			}
			lastRenderingTime = renderingTime;
			double totalMilliseconds = (renderingTime - valueOrDefault).TotalMilliseconds;
			DisplayFrameIntervalEstimator.Observe(totalMilliseconds);
			FrameCount++;
			TotalIntervalMs += totalMilliseconds;
			if (totalMilliseconds > MaxIntervalMs)
			{
				MaxIntervalMs = totalMilliseconds;
			}
			if (totalMilliseconds > DisplayFrameIntervalEstimator.CurrentIntervalMs * 1.5)
			{
				JankFrameCount++;
			}
			if (sampledCount < 512)
			{
				sampledIntervalsMs[sampledCount++] = totalMilliseconds;
			}
			int num = Math.Clamp((int)Math.Round(totalMilliseconds / DisplayFrameIntervalEstimator.CurrentIntervalMs), 1, cycleBuckets.Length - 1);
			cycleBuckets[num]++;
			if (num >= 3)
			{
				consecutiveLongFrames++;
				if (consecutiveLongFrames > MaxConsecutiveLongFrames)
				{
					MaxConsecutiveLongFrames = consecutiveLongFrames;
				}
			}
			else
			{
				consecutiveLongFrames = 0;
			}
			return totalMilliseconds;
		}
		lastRenderingTime = renderingTime;
		return 0.0;
	}

	internal double GetPercentileIntervalMs(double percentile)
	{
		if (sampledCount == 0)
		{
			return 0.0;
		}
		double[] array = new double[sampledCount];
		Array.Copy(sampledIntervalsMs, array, sampledCount);
		Array.Sort(array);
		int value = (int)Math.Ceiling(percentile / 100.0 * (double)sampledCount) - 1;
		return array[Math.Clamp(value, 0, sampledCount - 1)];
	}
}
