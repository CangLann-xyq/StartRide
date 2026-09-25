using System;
using System.Windows;

namespace StartRide.App.Controls;

internal static class ProgressiveBlurLayoutCalculator
{
	internal static ProgressiveBlurRenderLayout Calculate(double width, double height, double blurLength, double visibleBlurBandHeight, double maximumRadius, double renderScale, DpiScale dpiScale)
	{
		double num = Math.Clamp(AlignToDevicePixel(visibleBlurBandHeight, dpiScale.DpiScaleY), 0.0, height);
		double num2 = Math.Clamp(AlignToDevicePixel(blurLength, dpiScale.DpiScaleY), 0.0, num);
		double directListStart = num2;
		double num3 = Math.Min(height, num + 4.0);
		double num4 = CalculateLowResolutionDimension(width, dpiScale.DpiScaleX, renderScale);
		double num5 = CalculateLowResolutionDimension(num3, dpiScale.DpiScaleY, renderScale);
		double num6 = num4 / width;
		double num7 = num5 / num3;
		return new ProgressiveBlurRenderLayout(num4, num5, width / num4, num3 / num5, Math.Clamp(blurLength * num7, 0.0, num5), Math.Max(0.0, maximumRadius * num6), Math.Max(0.0, maximumRadius * num7), num3, num2, directListStart);
	}

	private static double AlignToDevicePixel(double value, double dpiScale)
	{
		return Math.Round(value * dpiScale, MidpointRounding.AwayFromZero) / dpiScale;
	}

	private static double CalculateLowResolutionDimension(double fullSize, double dpiScale, double renderScale)
	{
		double num = Math.Max(1.0, Math.Round(fullSize * dpiScale * renderScale, MidpointRounding.AwayFromZero));
		return Math.Min(fullSize, num / dpiScale);
	}
}
