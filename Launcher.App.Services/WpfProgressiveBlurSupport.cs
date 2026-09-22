using System;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Launcher.App.Effects;

namespace Launcher.App.Services;

internal sealed class WpfProgressiveBlurSupport : IProgressiveBlurSupport, IDisposable
{
	private readonly object syncRoot = new object();

	private ProgressiveBlurCapabilitySnapshot current;

	private bool shaderRejected;

	private bool isDisposed;

	public ProgressiveBlurCapabilitySnapshot Current
	{
		get
		{
			lock (syncRoot)
			{
				return current;
			}
		}
	}

	public event EventHandler? AvailabilityChanged;

	public WpfProgressiveBlurSupport()
	{
		RenderCapability.TierChanged += RenderCapability_TierChanged;
		PixelShader.InvalidPixelShaderEncountered += PixelShader_InvalidPixelShaderEncountered;
		current = EvaluateCurrent();
	}

	public void Dispose()
	{
		lock (syncRoot)
		{
			if (isDisposed)
			{
				return;
			}
			isDisposed = true;
		}
		RenderCapability.TierChanged -= RenderCapability_TierChanged;
		PixelShader.InvalidPixelShaderEncountered -= PixelShader_InvalidPixelShaderEncountered;
	}

	private ProgressiveBlurCapabilitySnapshot EvaluateCurrent()
	{
		int num = RenderCapability.Tier >> 16;
		bool flag = RenderCapability.IsPixelShaderVersionSupported(3, 0);
		bool flag2;
		lock (syncRoot)
		{
			flag2 = shaderRejected;
		}
		if (flag2 || num < 2 || !flag)
		{
			return ProgressiveBlurCapabilityEvaluator.Evaluate(num, flag, isShaderLoaded: false, flag2);
		}
		bool isShaderLoaded = ProgressiveGaussianBlurShader.TryGet(out PixelShader _, out Exception exception);
		return ProgressiveBlurCapabilityEvaluator.Evaluate(num, flag, isShaderLoaded, isShaderRejected: false, exception);
	}

	private void Refresh()
	{
		lock (syncRoot)
		{
			if (isDisposed)
			{
				return;
			}
		}
		ProgressiveBlurCapabilitySnapshot progressiveBlurCapabilitySnapshot = EvaluateCurrent();
		lock (syncRoot)
		{
			if (isDisposed || current == progressiveBlurCapabilitySnapshot)
			{
				return;
			}
			current = progressiveBlurCapabilitySnapshot;
		}
		AvailabilityChanged?.Invoke(this, EventArgs.Empty);
	}

	private void RenderCapability_TierChanged(object? sender, EventArgs e)
	{
		Refresh();
	}

	private void PixelShader_InvalidPixelShaderEncountered(object? sender, EventArgs e)
	{
		lock (syncRoot)
		{
			if (isDisposed || shaderRejected)
			{
				return;
			}
			shaderRejected = true;
		}
		Refresh();
	}
}
