using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;

namespace Launcher.App.Controls.Account;

internal sealed class Viewport3DIdleRenderCache
{
	internal const long MaximumTextureBytes = 8388608L;

	private readonly Viewport3D viewport;

	private readonly string viewportName;

	private int generation;

	internal bool IsActive => viewport.CacheMode is BitmapCache;

	internal Viewport3DIdleRenderCache(Viewport3D viewport, string viewportName)
	{
		this.viewport = viewport;
		this.viewportName = viewportName;
	}

	internal void Disable(string reason)
	{
		generation++;
		if (viewport.CacheMode is BitmapCache)
		{
			viewport.CacheMode = null;
		}
	}

	internal void QueueEnable(Func<bool> isSceneStable)
	{
		int requestedGeneration = ++generation;
		((DispatcherObject)viewport).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			TryEnable(requestedGeneration, isSceneStable);
		}, (DispatcherPriority)7, Array.Empty<object>());
	}

	private void TryEnable(int requestedGeneration, Func<bool> isSceneStable)
	{
		if (requestedGeneration != generation || IsActive || !viewport.IsLoaded || !isSceneStable())
		{
			return;
		}
		DpiScale dpi = VisualTreeHelper.GetDpi(viewport);
		if (!TryEstimateTextureBytes(viewport.ActualWidth, viewport.ActualHeight, dpi.DpiScaleX, dpi.DpiScaleY, RenderCapability.Tier, out long _, out string _))
		{
			return;
		}
		try
		{
			viewport.CacheMode = CreateCache();
		}
		catch (Exception exception)
		{
			viewport.CacheMode = null;
			Log.Warning(exception, "Account {ViewportName} Viewport3D cache initialization failed; continuing with live rendering", viewportName);
		}
	}

	internal static BitmapCache CreateCache()
	{
		BitmapCache obj = new BitmapCache
		{
			RenderAtScale = 1.0,
			EnableClearType = false,
			SnapsToDevicePixels = true
		};
		((Freezable)obj).Freeze();
		return obj;
	}

	internal static bool TryEstimateTextureBytes(double width, double height, double dpiScaleX, double dpiScaleY, int renderTier, out long estimatedTextureBytes, out string reason)
	{
		estimatedTextureBytes = 0L;
		if (renderTier >> 16 < 2)
		{
			reason = "RenderTierBelow2";
			return false;
		}
		if (!double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(dpiScaleX) || !double.IsFinite(dpiScaleY) || width <= 0.0 || height <= 0.0 || dpiScaleX <= 0.0 || dpiScaleY <= 0.0)
		{
			reason = "InvalidSizeOrDpi";
			return false;
		}
		long num = (long)Math.Ceiling(width * dpiScaleX);
		long num2 = (long)Math.Ceiling(height * dpiScaleY);
		try
		{
			estimatedTextureBytes = checked(num * num2 * 4);
		}
		catch (OverflowException)
		{
			estimatedTextureBytes = long.MaxValue;
		}
		if (estimatedTextureBytes > 8388608)
		{
			reason = "TextureBudgetExceeded";
			return false;
		}
		reason = "None";
		return true;
	}
}
