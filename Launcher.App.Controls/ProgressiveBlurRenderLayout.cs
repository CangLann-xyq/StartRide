namespace Launcher.App.Controls;

internal readonly record struct ProgressiveBlurRenderLayout(double LowResolutionWidth, double LowResolutionHeight, double UpscaleX, double UpscaleY, double ScaledBlurLength, double HorizontalMaximumRadius, double VerticalMaximumRadius, double TextureHeight, double PresentationHeight, double DirectListStart);
