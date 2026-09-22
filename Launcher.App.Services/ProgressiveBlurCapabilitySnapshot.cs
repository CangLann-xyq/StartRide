using System;

namespace Launcher.App.Services;

internal readonly record struct ProgressiveBlurCapabilitySnapshot(bool IsAvailable, int RenderingTier, bool IsPixelShader30Supported, ProgressiveBlurUnavailableReason UnavailableReason, Exception? InitializationException);
