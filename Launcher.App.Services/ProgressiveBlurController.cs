using System;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace Launcher.App.Services;

internal sealed class ProgressiveBlurController : IDisposable
{
	private readonly IUiDispatcher uiDispatcher;

	private readonly IProgressiveBlurSupport support;

	private readonly ILogger logger;

	private ProgressiveBlurCapabilitySnapshot? lastLoggedCapability;

	private bool? lastLoggedState;

	private bool isInitialized;

	private bool isDisposed;

	public ProgressiveBlurController(IUiDispatcher uiDispatcher, IProgressiveBlurSupport support, ILogger logger)
	{
		this.uiDispatcher = uiDispatcher ?? throw new ArgumentNullException("uiDispatcher");
		this.support = support ?? throw new ArgumentNullException("support");
		this.logger = logger ?? throw new ArgumentNullException("logger");
		support.AvailabilityChanged += Support_AvailabilityChanged;
	}

	public void Initialize()
	{
		if (!isDisposed && !isInitialized)
		{
			isInitialized = true;
			ApplyCurrent();
		}
	}

	public void Dispose()
	{
		if (!isDisposed)
		{
			support.AvailabilityChanged -= Support_AvailabilityChanged;
			support.Dispose();
			isDisposed = true;
		}
	}

	private void Support_AvailabilityChanged(object? sender, EventArgs e)
	{
		if (!isDisposed)
		{
			uiDispatcher.Invoke(ApplyCurrent);
		}
	}

	private void ApplyCurrent()
	{
		System.Windows.Application current = System.Windows.Application.Current;
		if (current != null)
		{
			ProgressiveBlurCapabilitySnapshot current2 = support.Current;
			current.Resources["Is.ProgressiveBlur.Enabled"] = current2.IsAvailable;
			LogCapability(current2);
			if (lastLoggedState != current2.IsAvailable)
			{
				logger.LogDebug("Progressive blur availability applied. ProgressiveBlurActive={ProgressiveBlurActive}", current2.IsAvailable);
				lastLoggedState = current2.IsAvailable;
			}
		}
	}

	private void LogCapability(ProgressiveBlurCapabilitySnapshot capability)
	{
		if (!(lastLoggedCapability == capability))
		{
			if (capability.UnavailableReason == ProgressiveBlurUnavailableReason.ShaderLoadFailed && capability.InitializationException != null)
			{
				logger.LogWarning(capability.InitializationException, "Progressive blur shader initialization failed; opacity fade fallback will be used. RenderTier={RenderTier} ShaderModel=3.0 HardwareOnly=True", capability.RenderingTier);
			}
			else if (capability.UnavailableReason == ProgressiveBlurUnavailableReason.ShaderRejected)
			{
				logger.LogWarning("Progressive blur shader was rejected by WPF; opacity fade fallback will be used. RenderTier={RenderTier} ShaderModel=3.0 HardwareOnly=True", capability.RenderingTier);
			}
			else
			{
				logger.LogDebug("Progressive blur capability evaluated. Supported={Supported} RenderTier={RenderTier} PixelShader30Supported={PixelShader30Supported} ShaderModel=3.0 Reason={Reason} HardwareOnly=True", capability.IsAvailable, capability.RenderingTier, capability.IsPixelShader30Supported, capability.UnavailableReason);
			}
			lastLoggedCapability = capability;
		}
	}
}
