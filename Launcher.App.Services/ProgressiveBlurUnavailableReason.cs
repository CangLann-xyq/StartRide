namespace Launcher.App.Services;

internal enum ProgressiveBlurUnavailableReason
{
	None,
	RenderingTierTooLow,
	PixelShader30Unsupported,
	ShaderLoadFailed,
	ShaderRejected
}
