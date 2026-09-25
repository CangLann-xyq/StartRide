using System;
using System.Threading.Tasks;
using StartRide.App.Services;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Download;

internal sealed class DownloadInstallProgress : IProgress<LauncherProgress>, IDisposable
{
	private static readonly TimeSpan UiUpdateInterval = TimeSpan.FromMilliseconds(120.0);

	private readonly object syncRoot = new object();

	private readonly DownloadTaskItem installTask;

	private readonly long installSequence;

	private readonly Action<DownloadTaskItem, LauncherProgress, long> reportProgress;

	private readonly IUiDispatcher uiDispatcher;

	private LauncherProgress? pendingProgress;

	private DateTimeOffset lastFlushedAt = DateTimeOffset.MinValue;

	private bool isFlushQueued;

	private bool isDisposed;

	public DownloadInstallProgress(DownloadTaskItem installTask, long installSequence, Action<DownloadTaskItem, LauncherProgress, long> reportProgress, IUiDispatcher uiDispatcher)
	{
		this.installTask = installTask;
		this.installSequence = installSequence;
		this.reportProgress = reportProgress;
		this.uiDispatcher = uiDispatcher;
	}

	public void Report(LauncherProgress value)
	{
		TimeSpan delay;
		lock (syncRoot)
		{
			if (isDisposed)
			{
				return;
			}
			pendingProgress = value;
			if (isFlushQueued)
			{
				return;
			}
			TimeSpan timeSpan = DateTimeOffset.UtcNow - lastFlushedAt;
			delay = ((timeSpan >= UiUpdateInterval) ? TimeSpan.Zero : (UiUpdateInterval - timeSpan));
			isFlushQueued = true;
		}
		QueueFlush(delay);
	}

	public void Dispose()
	{
		lock (syncRoot)
		{
			isDisposed = true;
			pendingProgress = null;
			isFlushQueued = false;
		}
	}

	private void QueueFlush(TimeSpan delay)
	{
		if (delay <= TimeSpan.Zero)
		{
			PostFlush();
		}
		else
		{
			FlushAfterDelayAsync(delay);
		}
	}

	private async Task FlushAfterDelayAsync(TimeSpan delay)
	{
		try
		{
			await Task.Delay(delay);
			PostFlush();
		}
		catch (ObjectDisposedException)
		{
		}
	}

	private void PostFlush()
	{
		if (!uiDispatcher.HasAccess)
		{
			uiDispatcher.Post(Flush);
		}
		else
		{
			Flush();
		}
	}

	private void Flush()
	{
		LauncherProgress launcherProgress;
		lock (syncRoot)
		{
			if (isDisposed)
			{
				return;
			}
			launcherProgress = pendingProgress;
			pendingProgress = null;
			lastFlushedAt = DateTimeOffset.UtcNow;
			isFlushQueued = false;
		}
		if ((object)launcherProgress != null)
		{
			reportProgress(installTask, launcherProgress, installSequence);
		}
	}
}
