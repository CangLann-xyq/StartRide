using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace Launcher.App.Services;

public sealed class ClipboardService : IClipboardService
{
	private const int RetryCount = 3;

	private const int RetryDelayMilliseconds = 20;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger<ClipboardService> logger;

	public ClipboardService(IUiDispatcher uiDispatcher, ILogger<ClipboardService> logger)
	{
		this.uiDispatcher = uiDispatcher;
		this.logger = logger;
	}

	public async Task<bool> CopyTextAsync(string text, CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		bool copied = false;
		Exception lastException = null;
		await uiDispatcher.InvokeAsync(async delegate
		{
			for (int attempt = 0; attempt < 3; attempt++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					Clipboard.SetDataObject(text, copy: false);
					copied = true;
					break;
				}
				catch (Exception ex)
				{
					lastException = ex;
					if (attempt + 1 < 3)
					{
						await Task.Delay(20, cancellationToken);
					}
				}
			}
		});
		if (!copied)
		{
			logger.LogWarning(lastException, "Failed to write text to the Windows clipboard after retries.");
		}
		return copied;
	}

	public async Task<string?> GetTextAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		string text = null;
		bool completed = false;
		Exception lastException = null;
		await uiDispatcher.InvokeAsync(async delegate
		{
			for (int attempt = 0; attempt < 3; attempt++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					text = (Clipboard.ContainsText() ? Clipboard.GetText() : null);
					completed = true;
					break;
				}
				catch (Exception ex)
				{
					lastException = ex;
					if (attempt + 1 < 3)
					{
						await Task.Delay(20, cancellationToken);
					}
				}
			}
		});
		if (!completed)
		{
			logger.LogWarning(lastException, "Failed to read text from the Windows clipboard after retries.");
			return null;
		}
		return text;
	}
}
