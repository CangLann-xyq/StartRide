using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Services;

namespace Launcher.App.ViewModels.Download;

public sealed class DownloadTasksPageViewModel : ObservableObject
{
	private static readonly TimeSpan DefaultCompletedTaskRetention = TimeSpan.FromSeconds(3.0);

	private readonly TimeSpan completedTaskRetention;

	private readonly object backgroundTasksLock = new object();

	private readonly HashSet<Task> backgroundTasks = new HashSet<Task>();

	private readonly Dictionary<string, CancellationTokenSource> removalTokens = new Dictionary<string, CancellationTokenSource>();

	private readonly IUiDispatcher uiDispatcher;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<DownloadTaskItem?>? cancelTaskCommand;

	public ObservableCollection<DownloadTaskItem> Tasks { get; } = new ObservableCollection<DownloadTaskItem>();

	public bool HasTasks => Tasks.Count > 0;

	public bool HasRunningTasks => RunningTaskCount > 0;

	public bool HasActiveOperations
	{
		get
		{
			if (!HasRunningTasks)
			{
				return TrackedBackgroundTaskCount > 0;
			}
			return true;
		}
	}

	public int RunningTaskCount => Tasks.Count((DownloadTaskItem task) => task.State == DownloadTaskState.Running);

	internal int TrackedBackgroundTaskCount
	{
		get
		{
			lock (backgroundTasksLock)
			{
				return backgroundTasks.Count;
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<DownloadTaskItem?> CancelTaskCommand => cancelTaskCommand ?? (cancelTaskCommand = new RelayCommand<DownloadTaskItem>(CancelTask));

	public event EventHandler<DownloadTaskItem>? TaskStarted;

	public event EventHandler? ActivityChanged;

	public DownloadTasksPageViewModel()
		: this(ImmediateUiDispatcher.Instance, null)
	{
	}

	public DownloadTasksPageViewModel(TimeSpan? completedTaskRetention)
		: this(ImmediateUiDispatcher.Instance, completedTaskRetention)
	{
	}

	public DownloadTasksPageViewModel(IUiDispatcher uiDispatcher)
		: this(uiDispatcher, null)
	{
	}

	private DownloadTasksPageViewModel(IUiDispatcher uiDispatcher, TimeSpan? completedTaskRetention)
	{
		this.uiDispatcher = uiDispatcher;
		this.completedTaskRetention = completedTaskRetention ?? DefaultCompletedTaskRetention;
		Tasks.CollectionChanged += Tasks_CollectionChanged;
	}

	public DownloadTaskItem BeginTask(string title, string subtitle)
	{
		if (uiDispatcher.HasAccess)
		{
			return BeginTaskCore(title, subtitle);
		}
		DownloadTaskItem task = null;
		uiDispatcher.Invoke(delegate
		{
			task = BeginTaskCore(title, subtitle);
		});
		return task;
	}

	private DownloadTaskItem BeginTaskCore(string title, string subtitle)
	{
		DownloadTaskItem downloadTaskItem = new DownloadTaskItem(title, subtitle);
		downloadTaskItem.PropertyChanged += Task_PropertyChanged;
		Tasks.Insert(0, downloadTaskItem);
		TaskStarted?.Invoke(this, downloadTaskItem);
		return downloadTaskItem;
	}

	[RelayCommand]
	public void CancelTask(DownloadTaskItem? task)
	{
		if (task != null)
		{
			task.Cancel();
			RemoveTask(task, force: true);
		}
	}

	public void CancelAllRunningTasks()
	{
		foreach (DownloadTaskItem item in Tasks.ToList())
		{
			if (item.State == DownloadTaskState.Running)
			{
				item.Cancel();
			}
		}
	}

	public void TrackBackgroundTask(Task task)
	{
		ArgumentNullException.ThrowIfNull(task, "task");
		if (!task.IsCompleted)
		{
			lock (backgroundTasksLock)
			{
				backgroundTasks.Add(task);
			}
			NotifyActivityChanged();
			RemoveTrackedBackgroundTaskWhenCompletedAsync(task);
		}
	}

	public async Task<bool> WaitForTrackedBackgroundTasksAsync(TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
	{
		Task[] array;
		lock (backgroundTasksLock)
		{
			array = backgroundTasks.ToArray();
		}
		if (array.Length == 0)
		{
			return true;
		}
		try
		{
			await Task.WhenAll(array).WaitAsync(timeout, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return true;
		}
		catch (TimeoutException)
		{
			return false;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return false;
		}
		catch
		{
			return true;
		}
	}

	private void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.OldItems != null)
		{
			foreach (DownloadTaskItem oldItem in e.OldItems)
			{
				CancelScheduledRemoval(oldItem);
				oldItem.PropertyChanged -= Task_PropertyChanged;
			}
		}
		OnPropertyChanged("HasTasks");
		OnPropertyChanged("HasRunningTasks");
		OnPropertyChanged("RunningTaskCount");
		NotifyActivityChanged();
	}

	private void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is DownloadTaskItem downloadTaskItem && !(e.PropertyName != "State"))
		{
			if (downloadTaskItem.State == DownloadTaskState.Completed)
			{
				ScheduleRemoval(downloadTaskItem);
			}
			else
			{
				CancelScheduledRemoval(downloadTaskItem);
			}
			OnPropertyChanged("HasRunningTasks");
			OnPropertyChanged("RunningTaskCount");
			NotifyActivityChanged();
		}
	}

	private void ScheduleRemoval(DownloadTaskItem task)
	{
		CancelScheduledRemoval(task);
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		removalTokens[task.Id] = cancellationTokenSource;
		RemoveCompletedTaskAfterDelayAsync(task, cancellationTokenSource.Token);
	}

	private void CancelScheduledRemoval(DownloadTaskItem task)
	{
		if (removalTokens.Remove(task.Id, out CancellationTokenSource value))
		{
			value.Cancel();
			value.Dispose();
		}
	}

	private async Task RemoveCompletedTaskAfterDelayAsync(DownloadTaskItem task, CancellationToken cancellationToken)
	{
		try
		{
			await Task.Delay(completedTaskRetention, cancellationToken);
			if (!cancellationToken.IsCancellationRequested)
			{
				RemoveTask(task, force: false);
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task RemoveTrackedBackgroundTaskWhenCompletedAsync(Task task)
	{
		try
		{
			await task.ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
		}
		finally
		{
			lock (backgroundTasksLock)
			{
				backgroundTasks.Remove(task);
			}
			NotifyActivityChanged();
		}
	}

	private void NotifyActivityChanged()
	{
		if (!uiDispatcher.HasAccess)
		{
			uiDispatcher.Post(NotifyActivityChanged);
			return;
		}
		OnPropertyChanged("HasActiveOperations");
		ActivityChanged?.Invoke(this, EventArgs.Empty);
	}

	private void RemoveTask(DownloadTaskItem task, bool force)
	{
		if (!uiDispatcher.HasAccess)
		{
			uiDispatcher.Post(delegate
			{
				RemoveTask(task, force);
			});
		}
		else if (force || task.State == DownloadTaskState.Completed)
		{
			Tasks.Remove(task);
		}
	}
}
