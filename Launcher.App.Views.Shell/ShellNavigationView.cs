using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Launcher.App.ViewModels.Download;
using Launcher.App.ViewModels.Shell;

namespace Launcher.App.Views.Shell;

public partial class ShellNavigationView : UserControl, IComponentConnector
{
	private int downloadTaskPulseDispatchQueued;

	private bool isDownloadTaskPulseRunning;

	private DateTimeOffset lastDownloadTaskPulseAt;

	private MainViewModel? viewModel;

	public ShellNavigationView()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		lastDownloadTaskPulseAt = DateTimeOffset.MinValue;
		InitializeComponent();
		base.DataContextChanged += new DependencyPropertyChangedEventHandler(ShellNavigationView_DataContextChanged);
		base.Unloaded += delegate
		{
			AttachViewModel(null);
		};
	}

	private void ShellNavigationView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		AttachViewModel(e.NewValue as MainViewModel);
	}

	private void AttachViewModel(MainViewModel? nextViewModel)
	{
		if (viewModel != nextViewModel)
		{
			if (viewModel != null)
			{
				viewModel.DownloadTasksPage.TaskStarted -= DownloadTasksPage_TaskStarted;
			}
			viewModel = nextViewModel;
			if (viewModel != null)
			{
				viewModel.DownloadTasksPage.TaskStarted += DownloadTasksPage_TaskStarted;
			}
		}
	}

	private void DownloadTasksPage_TaskStarted(object? sender, DownloadTaskItem e)
	{
		if (Interlocked.Exchange(ref downloadTaskPulseDispatchQueued, 1) != 1)
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(TryRunDownloadTaskPulse), (DispatcherPriority)7, Array.Empty<object>());
		}
	}

	private void TryRunDownloadTaskPulse()
	{
		Interlocked.Exchange(ref downloadTaskPulseDispatchQueued, 0);
		if (!isDownloadTaskPulseRunning)
		{
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			if (!(utcNow - lastDownloadTaskPulseAt < TimeSpan.FromMilliseconds(950.0)))
			{
				lastDownloadTaskPulseAt = utcNow;
				RunDownloadTaskPulse();
			}
		}
	}

	private void RunDownloadTaskPulse()
	{
		isDownloadTaskPulseRunning = true;
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(850.0);
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		DownloadTaskPulseCircle.BeginAnimation(UIElement.OpacityProperty, null);
		DownloadTaskPulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		DownloadTaskPulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		DownloadTaskPulseCircle.Opacity = 1.0;
		DownloadTaskPulseScale.ScaleX = 0.9;
		DownloadTaskPulseScale.ScaleY = 0.9;
		DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, timeSpan)
		{
			EasingFunction = easingFunction,
			FillBehavior = FillBehavior.Stop
		};
		doubleAnimation.Completed += delegate
		{
			DownloadTaskPulseCircle.BeginAnimation(UIElement.OpacityProperty, null);
			DownloadTaskPulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			DownloadTaskPulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			DownloadTaskPulseCircle.Opacity = 0.0;
			DownloadTaskPulseScale.ScaleX = 1.0;
			DownloadTaskPulseScale.ScaleY = 1.0;
			isDownloadTaskPulseRunning = false;
		};
		DownloadTaskPulseCircle.BeginAnimation(UIElement.OpacityProperty, doubleAnimation, HandoffBehavior.SnapshotAndReplace);
		DoubleAnimation doubleAnimation2 = new DoubleAnimation(0.9, 1.0, timeSpan)
		{
			EasingFunction = easingFunction,
			FillBehavior = FillBehavior.Stop
		};
		DownloadTaskPulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimation2, HandoffBehavior.SnapshotAndReplace);
		DownloadTaskPulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation2.Clone(), HandoffBehavior.SnapshotAndReplace);
	}

}
