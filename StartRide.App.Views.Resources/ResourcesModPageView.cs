using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using StartRide.App.Behaviors;
using StartRide.App.Services;
using StartRide.App.Utilities;
using StartRide.App.ViewModels.Resources;

namespace StartRide.App.Views.Resources;

public partial class ResourcesModPageView : UserControl, IComponentConnector
{
	private const double LoadMoreThreshold = 320.0;

	private readonly SlidingContentTransitionCoordinator stepTransition;

	private readonly SlidingContentTransitionCoordinator detailsTransition;

	private ScrollViewer? scrollViewer;

	private ScrollViewer? versionScrollViewer;

	private INotifyPropertyChanged? currentViewModelNotifier;

	private INotifyPropertyChanged? currentVersionsNotifier;

	private bool isVersionAutoLoadQueued;

	public ScrollViewer ScrollViewer
	{
		get
		{
			AttachScrollViewer();
			return scrollViewer ?? throw new InvalidOperationException("Resources mod list scroll viewer is not available.");
		}
	}

	public ResourcesModPageView()
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		InitializeComponent();
		stepTransition = new SlidingContentTransitionCoordinator(this, ModStepHost, ProjectListStep, ProjectDetailsStep);
		detailsTransition = new SlidingContentTransitionCoordinator(this, DetailsStepHost, InstallTargetStep, ProjectVersionsStep);
		base.Loaded += delegate
		{
			AttachScrollViewers();
			stepTransition.Sync(IsProjectContentStep());
			detailsTransition.Sync(IsProjectVersionsStep());
		};
		base.Unloaded += delegate
		{
			DetachScrollViewers();
			DetachViewModelNotifier();
		};
		base.DataContextChanged += new DependencyPropertyChangedEventHandler(ResourcesModPageView_OnDataContextChanged);
	}

	public void RefreshViewport()
	{
		ResourcesModListBox.UpdateLayout();
		VirtualizedListItemStateBehavior.Refresh((DependencyObject)(object)ResourcesModListBox);
	}

	private void ResourcesModPageView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		DetachViewModelNotifier();
		currentViewModelNotifier = e.NewValue as INotifyPropertyChanged;
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged += ResourcesModPageViewModel_OnPropertyChanged;
		}
		if (e.NewValue is ResourcesModPageViewModel resourcesModPageViewModel)
		{
			currentVersionsNotifier = resourcesModPageViewModel.Versions;
			currentVersionsNotifier.PropertyChanged += ResourcesProjectVersionsViewModel_OnPropertyChanged;
		}
		stepTransition.Sync(IsProjectContentStep());
		detailsTransition.Sync(IsProjectVersionsStep());
	}

	private void AttachScrollViewers()
	{
		AttachScrollViewer();
		AttachVersionScrollViewer();
	}

	private void DetachViewModelNotifier()
	{
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged -= ResourcesModPageViewModel_OnPropertyChanged;
		}
		if (currentVersionsNotifier != null)
		{
			currentVersionsNotifier.PropertyChanged -= ResourcesProjectVersionsViewModel_OnPropertyChanged;
		}
		currentViewModelNotifier = null;
		currentVersionsNotifier = null;
	}

	private void ResourcesModPageViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "CurrentStep" && sender is ResourcesModPageViewModel resourcesModPageViewModel)
		{
			stepTransition.AnimateTo(resourcesModPageViewModel.CurrentStep != ResourcesModPageStep.ProjectList);
			detailsTransition.AnimateTo(resourcesModPageViewModel.CurrentStep == ResourcesModPageStep.ProjectVersions);
			if (resourcesModPageViewModel.CurrentStep == ResourcesModPageStep.ProjectVersions)
			{
				AttachVersionScrollViewer();
				QueueVersionAutoLoadIfNeeded();
			}
		}
	}

	private void ResourcesProjectVersionsViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		bool flag = sender is ResourcesProjectVersionsViewModel;
		if (flag)
		{
			bool flag2;
			switch (e.PropertyName)
			{
			case "IsLoading":
			case "IsLoadingMore":
			case "HasMore":
			case "VisibleVersionCount":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = flag2;
		}
		if (flag)
		{
			QueueVersionAutoLoadIfNeeded();
		}
	}

	private void AttachScrollViewer()
	{
		ResourcesModListBox.ApplyTemplate();
		ScrollViewer scrollViewer = VisualTreeSearch.FindDescendant((DependencyObject)(object)ResourcesModListBox, (ScrollViewer _) => true);
		if (this.scrollViewer != scrollViewer)
		{
			DetachScrollViewer();
			this.scrollViewer = scrollViewer;
			if (this.scrollViewer != null)
			{
				this.scrollViewer.ScrollChanged += ScrollViewer_OnScrollChanged;
			}
		}
	}

	private void AttachVersionScrollViewer()
	{
		ResourcesModVersionListBox.ApplyTemplate();
		ScrollViewer scrollViewer = VisualTreeSearch.FindDescendant((DependencyObject)(object)ResourcesModVersionListBox, (ScrollViewer _) => true);
		if (versionScrollViewer != scrollViewer)
		{
			DetachVersionScrollViewer();
			versionScrollViewer = scrollViewer;
			if (versionScrollViewer != null)
			{
				versionScrollViewer.ScrollChanged += VersionScrollViewer_OnScrollChanged;
			}
		}
	}

	private void DetachScrollViewer()
	{
		if (scrollViewer != null)
		{
			scrollViewer.ScrollChanged -= ScrollViewer_OnScrollChanged;
		}
		scrollViewer = null;
	}

	private void DetachVersionScrollViewer()
	{
		if (versionScrollViewer != null)
		{
			versionScrollViewer.ScrollChanged -= VersionScrollViewer_OnScrollChanged;
		}
		versionScrollViewer = null;
	}

	private void DetachScrollViewers()
	{
		DetachScrollViewer();
		DetachVersionScrollViewer();
	}

	private void ScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (base.DataContext is ResourcesModPageViewModel resourcesModPageViewModel && sender is ScrollViewer { ScrollableHeight: 0 } scrollViewer && scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset <= 320.0)
		{
			resourcesModPageViewModel.BeginLoadMoreProjects();
		}
	}

	private void VersionScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (base.DataContext is ResourcesModPageViewModel resourcesModPageViewModel && sender is ScrollViewer { ScrollableHeight: 0 } scrollViewer && scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset <= 320.0)
		{
			resourcesModPageViewModel.BeginLoadMoreAvailableVersions();
		}
	}

	private void QueueVersionAutoLoadIfNeeded()
	{
		if (!isVersionAutoLoadQueued)
		{
			isVersionAutoLoadQueued = true;
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				isVersionAutoLoadQueued = false;
				BeginVersionAutoLoadIfNeeded();
			}, (DispatcherPriority)6, Array.Empty<object>());
		}
	}

	private void BeginVersionAutoLoadIfNeeded()
	{
		if (!(base.DataContext is ResourcesModPageViewModel { CurrentStep: ResourcesModPageStep.ProjectVersions } resourcesModPageViewModel) || !resourcesModPageViewModel.Versions.HasMore || resourcesModPageViewModel.Versions.IsLoading || resourcesModPageViewModel.Versions.IsLoadingMore)
		{
			return;
		}
		AttachVersionScrollViewer();
		if (versionScrollViewer != null)
		{
			ResourcesModVersionListBox.UpdateLayout();
			if (versionScrollViewer.ScrollableHeight <= 0.0)
			{
				resourcesModPageViewModel.BeginLoadMoreAvailableVersions();
			}
		}
	}

	private bool IsProjectContentStep()
	{
		if (base.DataContext is ResourcesModPageViewModel resourcesModPageViewModel)
		{
			return resourcesModPageViewModel.CurrentStep != ResourcesModPageStep.ProjectList;
		}
		return false;
	}

	private bool IsProjectVersionsStep()
	{
		if (base.DataContext is ResourcesModPageViewModel resourcesModPageViewModel)
		{
			return resourcesModPageViewModel.CurrentStep == ResourcesModPageStep.ProjectVersions;
		}
		return false;
	}

}
