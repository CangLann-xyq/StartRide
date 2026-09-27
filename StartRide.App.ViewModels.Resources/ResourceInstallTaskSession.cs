using System;
using System.Threading;
using StartRide.App.Utilities;
using StartRide.App.ViewModels.Download;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Resources;

internal sealed class ResourceInstallTaskSession
{
	private const double ModpackArchiveWeight = 5.0;

	private readonly DownloadTasksPageViewModel? owner;

	private readonly string initialMessage;

	private int dependencyCount;

	private int startedDependencyCount;

	private double primaryDownloadStart = 2.0;

	private bool primaryDownloadActive;

	private bool modpackImportActive;

	private readonly IProgress<LauncherProgress>? progress;

	public DownloadTaskItem? Task { get; }

	public CancellationToken CancellationToken => Task?.CancellationToken ?? CancellationToken.None;

	public bool IsCancellationRequested => Task?.IsCancellationRequested ?? false;

	public IProgress<LauncherProgress>? Progress => progress;

	private ResourceInstallTaskSession(DownloadTasksPageViewModel? owner, DownloadTaskItem? task, string initialMessage)
	{
		this.owner = owner;
		Task = task;
		this.initialMessage = initialMessage;
		progress = task?.CreateProgress(Report);
	}

	public static ResourceInstallTaskSession Begin(DownloadTasksPageViewModel? owner, string title, string subtitle, string initialMessage)
	{
		DownloadTaskItem task = owner?.BeginTask(title, subtitle);
		return new ResourceInstallTaskSession(owner, task, initialMessage);
	}

	public void BeginDependencies(int count)
	{
		dependencyCount = Math.Max(0, count);
		startedDependencyCount = 0;
		primaryDownloadActive = false;
		if (dependencyCount > 0)
		{
			Report(new LauncherProgress("Mod.DownloadingFile", initialMessage, 2.0));
		}
	}

	public void ReportDependencyStarted(LauncherProgress progress)
	{
		if (dependencyCount <= 0)
		{
			Report(progress);
			return;
		}
		startedDependencyCount = Math.Min(startedDependencyCount + 1, dependencyCount);
		int num = Math.Max(0, startedDependencyCount - 1);
		double value = 2.0 + 28.0 * (double)num / (double)dependencyCount;
		Report(progress with
		{
			Percent = value
		});
	}

	public void CompleteDependencies()
	{
		if (dependencyCount > 0)
		{
			Report(new LauncherProgress("Mod.DownloadingFile", initialMessage, 30.0));
		}
	}

	public void BeginPrimaryDownload(bool hasDependencies)
	{
		primaryDownloadStart = (hasDependencies ? 30 : 2);
		primaryDownloadActive = true;
		ReportToTask(new LauncherProgress("Mod.DownloadingFile", initialMessage, primaryDownloadStart));
	}

	public void BeginModpackImport()
	{
		modpackImportActive = true;
		ReportToTask(new LauncherProgress("Mod.DownloadingFile", initialMessage, 0.0));
	}

	public void Report(LauncherProgress progress)
	{
		if (modpackImportActive)
		{
			progress = MapModpackImportProgress(progress);
		}
		else if (primaryDownloadActive && progress.Stage == "Mod.DownloadingFile")
		{
			double? percent = progress.Percent;
			double num;
			if (percent.HasValue)
			{
				double valueOrDefault = percent.GetValueOrDefault();
				num = primaryDownloadStart + (96.0 - primaryDownloadStart) * Math.Clamp(valueOrDefault, 0.0, 100.0) / 100.0;
			}
			else
			{
				num = primaryDownloadStart;
			}
			double value = num;
			progress = progress with
			{
				Percent = value
			};
		}
		else if (primaryDownloadActive && progress.Stage == "Install.CompletingFiles")
		{
			progress = progress with
			{
				Percent = 99.0
			};
		}
		ReportToTask(progress);
	}

	private static LauncherProgress MapModpackImportProgress(LauncherProgress progress)
	{
		if (progress.Stage == "Mod.DownloadingFile")
		{
			double num = Math.Clamp(progress.Percent.GetValueOrDefault(), 0.0, 100.0);
			return progress with
			{
				Percent = 5.0 * num / 100.0
			};
		}
		double? percent = progress.Percent;
		if (percent.HasValue)
		{
			double valueOrDefault = percent.GetValueOrDefault();
			double num2 = Math.Clamp(valueOrDefault, 0.0, 99.0);
			double value = 5.0 + 94.0 * num2 / 99.0;
			return progress with
			{
				Percent = value
			};
		}
		return progress;
	}

	private void ReportToTask(LauncherProgress progress)
	{
		Task?.Report(progress with
		{
			Message = LauncherProgressTextFormatter.Format(progress)
		});
	}

	public void Complete(string message)
	{
		Task?.Complete(message);
	}

	public void Fail(string message)
	{
		Task?.Fail(message);
	}

	public void Dismiss()
	{
		if (Task != null)
		{
			owner?.CancelTask(Task);
		}
	}

	public bool CompleteCancellation()
	{
		if (!IsCancellationRequested || Task == null)
		{
			return false;
		}
		owner?.CancelTask(Task);
		return true;
	}
}
