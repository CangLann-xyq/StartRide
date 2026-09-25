using System;
using System.Windows;
using System.Windows.Media.Effects;

namespace StartRide.App.Effects;

internal static class ProgressiveGaussianBlurShader
{
	internal const string PackUri = "pack://application:,,,/StartRide;component/Effects/Shaders/ProgressiveGaussianBlur.ps";

	private static readonly object SyncRoot = new object();

	private static PixelShader? pixelShader;

	private static Exception? initializationException;

	private static bool initializationAttempted;

	internal static bool TryGet(out PixelShader? shader, out Exception? exception)
	{
		lock (SyncRoot)
		{
			if (!initializationAttempted)
			{
				Initialize();
			}
			shader = pixelShader;
			exception = initializationException;
			return shader != null;
		}
	}

	private static void Initialize()
	{
		initializationAttempted = true;
		try
		{
			PixelShader obj = new PixelShader
			{
				ShaderRenderMode = ShaderRenderMode.HardwareOnly,
				UriSource = new Uri("pack://application:,,,/StartRide;component/Effects/Shaders/ProgressiveGaussianBlur.ps", UriKind.Absolute)
			};
			((Freezable)obj).Freeze();
			pixelShader = obj;
		}
		catch (Exception ex)
		{
			initializationException = ex;
		}
	}
}
