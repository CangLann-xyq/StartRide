using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.App.Resources;
using Launcher.App.Utilities;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Download;

public sealed class DownloadTaskItem : ObservableObject
{
	private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

	private readonly IDisposable speedMeterLifetime;

	private readonly IProgress<LauncherProgress> speedMeterProgress;

	[ObservableProperty]
	private DownloadTaskState state;

	[ObservableProperty]
	private string statusMessage = Strings.DownloadTask_Preparing;

	[ObservableProperty]
	private string downloadSpeedText = string.Empty;

	[ObservableProperty]
	private double progressPercent;

	public string Id { get; } = Guid.NewGuid().ToString("N");

	public string Title { get; }

	public string Subtitle { get; }

	public CancellationToken CancellationToken => cancellation.Token;

	public bool IsCancellationRequested => cancellation.IsCancellationRequested;

	public string StateText => State switch
	{
		DownloadTaskState.Completed => Strings.DownloadTask_Completed, 
		DownloadTaskState.Failed => Strings.DownloadTask_Failed, 
		_ => Strings.DownloadTask_Running, 
	};

	public string ProgressText => $"{Math.Clamp(ProgressPercent, 0.0, 100.0):0}%";

	public bool IsRunning => State == DownloadTaskState.Running;

	public bool IsFailed => State == DownloadTaskState.Failed;

	public bool HasDownloadSpeedText => !string.IsNullOrWhiteSpace(DownloadSpeedText);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public DownloadTaskState State
	{
		get
		{
			return state;
		}
		set
		{
			if (!EqualityComparer<DownloadTaskState>.Default.Equals(state, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.State);
				state = value;
				OnStateChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.State);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string StatusMessage
	{
		get
		{
			return statusMessage;
		}
		[MemberNotNull("statusMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(statusMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.StatusMessage);
				statusMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.StatusMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string DownloadSpeedText
	{
		get
		{
			return downloadSpeedText;
		}
		[MemberNotNull("downloadSpeedText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(downloadSpeedText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DownloadSpeedText);
				downloadSpeedText = value;
				OnDownloadSpeedTextChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DownloadSpeedText);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double ProgressPercent
	{
		get
		{
			return progressPercent;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(progressPercent, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ProgressPercent);
				progressPercent = value;
				OnProgressPercentChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ProgressPercent);
			}
		}
	}

	public DownloadTaskItem(string title, string subtitle)
	{
		DownloadTaskItem downloadTaskItem = this;
		Title = title;
		Subtitle = subtitle;
		SynchronizationContext synchronizationContext = SynchronizationContext.Current;
		speedMeterProgress = DownloadSpeedTaskProgress.Create(Report, ReportOnCapturedContext, out speedMeterLifetime);
		void ReportOnCapturedContext(LauncherProgress value)
		{
			if (synchronizationContext == null || SynchronizationContext.Current == synchronizationContext)
			{
				downloadTaskItem.Report(value);
			}
			else
			{
				synchronizationContext.Post(delegate(object? state)
				{
					var (downloadTaskItem2, progress) = ((DownloadTaskItem, LauncherProgress))state;
					downloadTaskItem2.Report(progress);
				}, (downloadTaskItem, value));
			}
		}
	}

	internal IProgress<LauncherProgress> CreateProgress(Action<LauncherProgress> report)
	{
		return DownloadSpeedTaskProgress.Forward(speedMeterProgress, report);
	}

	public void Report(LauncherProgress progress)
	{
		DownloadTaskState downloadTaskState = State;
		if ((uint)(downloadTaskState - 1) <= 1u)
		{
			return;
		}
		if ((object)progress.DownloadSpeedTelemetry != null)
		{
			DownloadSpeedText = LauncherProgressTextFormatter.FormatDownloadSpeed(progress.DownloadSpeedTelemetry);
			return;
		}
		State = DownloadTaskState.Running;
		StatusMessage = progress.Message;
		double? percent = progress.Percent;
		if (percent.HasValue)
		{
			double valueOrDefault = percent.GetValueOrDefault();
			ProgressPercent = Math.Clamp(Math.Max(ProgressPercent, valueOrDefault), 0.0, 99.0);
		}
	}

	public void Complete(string message)
	{
		speedMeterLifetime.Dispose();
		State = DownloadTaskState.Completed;
		StatusMessage = message;
		ProgressPercent = 100.0;
		DownloadSpeedText = string.Empty;
	}

	public void Fail(string message)
	{
		speedMeterLifetime.Dispose();
		State = DownloadTaskState.Failed;
		StatusMessage = message;
		DownloadSpeedText = string.Empty;
	}

	public void Cancel()
	{
		speedMeterLifetime.Dispose();
		if (!cancellation.IsCancellationRequested)
		{
			cancellation.Cancel();
		}
		DownloadSpeedText = string.Empty;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnStateChanged(DownloadTaskState value)
	{
		OnPropertyChanged("StateText");
		OnPropertyChanged("IsRunning");
		OnPropertyChanged("IsFailed");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDownloadSpeedTextChanged(string value)
	{
		OnPropertyChanged("HasDownloadSpeedText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnProgressPercentChanged(double value)
	{
		OnPropertyChanged("ProgressText");
	}
}
