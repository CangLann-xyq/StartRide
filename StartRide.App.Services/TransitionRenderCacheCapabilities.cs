using System.Windows;
using System.Windows.Media;

namespace StartRide.App.Services;

internal readonly record struct TransitionRenderCacheCapabilities(int RenderingTier, Size MaximumTextureSize, long MaximumEstimatedBytes)
{
	internal static TransitionRenderCacheCapabilities Current
	{
		get
		{
			return new TransitionRenderCacheCapabilities(RenderCapability.Tier >> 16, RenderCapability.MaxHardwareTextureSize, 67108864L);
		}
	}

	internal const long DefaultMaximumEstimatedBytes = 67108864L;
}
