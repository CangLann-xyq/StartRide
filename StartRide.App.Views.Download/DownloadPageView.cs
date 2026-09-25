using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using StartRide.App.Services;
using StartRide.App.Utilities;
using StartRide.App.ViewModels.Download;

namespace StartRide.App.Views.Download;

public partial class DownloadPageView : UserControl, IComponentConnector
{
	private readonly SlidingContentTransitionCoordinator stepTransition;

	private readonly DownloadVersionListView downloadVersionList;

	private INotifyPropertyChanged? currentViewModelNotifier;

	private INotifyPropertyChanged? currentVersionListNotifier;

	public FrameworkElement RootElement => PageRoot;

	public DownloadPageView()
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Expected O, but got Unknown
		InitializeComponent();
		FrameworkElement frameworkElement = FindDownloadStepHost();
		downloadVersionList = FindStepContent<DownloadVersionListView>((DependencyObject)(object)frameworkElement, "DownloadVersionList", "Download version list view was not found.");
		stepTransition = new SlidingContentTransitionCoordinator(this, frameworkElement, FindStepContent<FrameworkElement>((DependencyObject)(object)frameworkElement, "VersionListStep", "Version list step was not found."), FindStepContent<FrameworkElement>((DependencyObject)(object)frameworkElement, "InstanceOptionsStep", "Instance options step was not found."), new global::_003C_003Ez__ReadOnlySingleElementList<FrameworkElement>(FindFloatingButton("InstallStep")));
		base.Loaded += DownloadPageView_OnLoaded;
		base.DataContextChanged += new DependencyPropertyChangedEventHandler(DownloadPageView_OnDataContextChanged);
	}

	private void DownloadPageView_OnLoaded(object sender, RoutedEventArgs e)
	{
		stepTransition.Sync(IsInstanceOptionsStep());
	}

	private void DownloadPageView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged -= DownloadPageViewModel_OnPropertyChanged;
		}
		if (currentVersionListNotifier != null)
		{
			currentVersionListNotifier.PropertyChanged -= DownloadVersionListViewModel_OnPropertyChanged;
		}
		currentViewModelNotifier = e.NewValue as INotifyPropertyChanged;
		if (currentViewModelNotifier != null)
		{
			currentViewModelNotifier.PropertyChanged += DownloadPageViewModel_OnPropertyChanged;
		}
		currentVersionListNotifier = (e.NewValue as DownloadPageViewModel)?.VersionList;
		if (currentVersionListNotifier != null)
		{
			currentVersionListNotifier.PropertyChanged += DownloadVersionListViewModel_OnPropertyChanged;
		}
		stepTransition.Sync(IsInstanceOptionsStep());
	}

	private void DownloadPageViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "ContentRefreshToken")
		{
			RefreshRightContentView();
		}
		if (e.PropertyName == "CurrentStep" && sender is DownloadPageViewModel downloadPageViewModel)
		{
			stepTransition.AnimateTo(downloadPageViewModel.CurrentStep == DownloadPageStep.InstanceOptions);
		}
	}

	private void DownloadVersionListViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if ((propertyName == "SelectedVersionCategory" || propertyName == "VersionSearchQuery") ? true : false)
		{
			ResetVersionListScrollPosition();
		}
	}

	private void RefreshRightContentView()
	{
		stepTransition.Sync(IsInstanceOptionsStep());
		ResetVersionListScrollPosition();
	}

	private void ResetVersionListScrollPosition()
	{
		downloadVersionList.ScrollViewer.ScrollToVerticalOffset(0.0);
		downloadVersionList.RefreshViewport();
	}

	private void SecondaryMenuOptionButton_OnRefreshRequested(object sender, RoutedEventArgs e)
	{
		RefreshRightContentView();
	}

	private bool IsInstanceOptionsStep()
	{
		if (base.DataContext is DownloadPageViewModel downloadPageViewModel)
		{
			return downloadPageViewModel.CurrentStep == DownloadPageStep.InstanceOptions;
		}
		return false;
	}

	private FrameworkElement FindDownloadStepHost()
	{
		return (DownloadVersionListFrame.ListContent as FrameworkElement) ?? throw new InvalidOperationException("Download step host content is not available.");
	}

	private static T FindStepContent<T>(DependencyObject root, string tag, string errorMessage) where T : FrameworkElement
	{
		return VisualTreeSearch.FindDescendant(root, (T element) => object.Equals(element.Tag, tag)) ?? throw new InvalidOperationException(errorMessage);
	}

	private Button FindFloatingButton(string tag)
	{
		object? floatingContent = DownloadVersionListFrame.FloatingContent;
		return VisualTreeSearch.FindDescendant((DependencyObject)(((floatingContent is DependencyObject) ? floatingContent : null) ?? throw new InvalidOperationException("Download version floating content is not available.")), (Button button) => object.Equals(button.Tag, tag)) ?? throw new InvalidOperationException("Download version floating button '" + tag + "' was not found.");
	}

}
