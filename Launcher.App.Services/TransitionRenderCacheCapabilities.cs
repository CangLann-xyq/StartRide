using System.Windows;
using System.Windows.Media;

namespace Launcher.App.Services;

internal readonly record struct TransitionRenderCacheCapabilities(int RenderingTier, Size MaximumTextureSize, long MaximumEstimatedBytes)
{
	internal static TransitionRenderCacheCapabilities Current
	{
		get
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			return new TransitionRenderCacheCapabilities(RenderCapability.Tier >> 16, RenderCapability.MaxHardwareTextureSize, 67108864L);
		}
	}

	internal const long DefaultMaximumEstimatedBytes = 67108864L;
}
