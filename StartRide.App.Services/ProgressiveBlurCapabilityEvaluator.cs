using System;

namespace StartRide.App.Services;

internal static class ProgressiveBlurCapabilityEvaluator
{
	internal const int MinimumRenderingTier = 2;

	internal static ProgressiveBlurCapabilitySnapshot Evaluate(int renderingTier, bool isPixelShader30Supported, bool isShaderLoaded, bool isShaderRejected, Exception? initializationException = null)
	{
		if (isShaderRejected)
		{
			return new ProgressiveBlurCapabilitySnapshot(IsAvailable: false, renderingTier, isPixelShader30Supported, ProgressiveBlurUnavailableReason.ShaderRejected, null);
		}
		if (renderingTier < 2)
		{
			return new ProgressiveBlurCapabilitySnapshot(IsAvailable: false, renderingTier, isPixelShader30Supported, ProgressiveBlurUnavailableReason.RenderingTierTooLow, null);
		}
		if (!isPixelShader30Supported)
		{
			return new ProgressiveBlurCapabilitySnapshot(IsAvailable: false, renderingTier, IsPixelShader30Supported: false, ProgressiveBlurUnavailableReason.PixelShader30Unsupported, null);
		}
		if (!isShaderLoaded)
		{
			return new ProgressiveBlurCapabilitySnapshot(IsAvailable: false, renderingTier, IsPixelShader30Supported: true, ProgressiveBlurUnavailableReason.ShaderLoadFailed, initializationException);
		}
		return new ProgressiveBlurCapabilitySnapshot(IsAvailable: true, renderingTier, IsPixelShader30Supported: true, ProgressiveBlurUnavailableReason.None, null);
	}
}
