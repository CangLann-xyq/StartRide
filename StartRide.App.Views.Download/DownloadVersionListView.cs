using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using StartRide.App.Behaviors;
using StartRide.App.Controls;
using StartRide.App.Utilities;
using StartRide.App.ViewModels.Download;

namespace StartRide.App.Views.Download;

public partial class DownloadVersionListView : UserControl, IComponentConnector
{
	private const double VersionItemHeight = 58.0;

	private ScrollViewer? scrollViewer;

	public ScrollViewer ScrollViewer
	{
		get
		{
			AttachScrollViewer();
			return scrollViewer ?? throw new InvalidOperationException("Download version list scroll viewer is not available.");
		}
	}

	public double EstimatedVersionItemHeight => 58.0;

	public DownloadVersionListView()
	{
		InitializeComponent();
		base.Loaded += delegate
		{
			AttachScrollViewer();
		};
	}

	public Button? FindVersionButton(DownloadVersionItem selectedVersion)
	{
		if (!(DownloadVersionListBox.ItemContainerGenerator.ContainerFromItem(selectedVersion) is ListBoxItem root))
		{
			return null;
		}
		return VisualTreeSearch.FindDescendant((DependencyObject)(object)root, (ListPageItemButton _) => true)?.InnerButton;
	}

	public bool ContainsVersion(DownloadVersionItem selectedVersion)
	{
		return DownloadVersionListBox.Items.Contains(selectedVersion);
	}

	public bool IsVersionRendered(DownloadVersionItem selectedVersion)
	{
		return DownloadVersionListBox.ItemContainerGenerator.ContainerFromItem(selectedVersion) is ListBoxItem;
	}

	public bool RealizeVersion(DownloadVersionItem selectedVersion)
	{
		if (!ContainsVersion(selectedVersion))
		{
			return false;
		}
		DownloadVersionListBox.ScrollIntoView(selectedVersion);
		DownloadVersionListBox.UpdateLayout();
		return IsVersionRendered(selectedVersion);
	}

	public double GetVersionTopOffset(DownloadVersionItem selectedVersion)
	{
		int num = DownloadVersionListBox.Items.IndexOf(selectedVersion);
		if (num < 0)
		{
			return 0.0;
		}
		return (double)num * 58.0;
	}

	public void RefreshViewport()
	{
		DownloadVersionListBox.UpdateLayout();
		VirtualizedListItemStateBehavior.Refresh((DependencyObject)(object)DownloadVersionListBox);
	}

	private void AttachScrollViewer()
	{
		DownloadVersionListBox.ApplyTemplate();
		ScrollViewer scrollViewer = VisualTreeSearch.FindDescendant((DependencyObject)(object)DownloadVersionListBox, (ScrollViewer _) => true);
		if (this.scrollViewer != scrollViewer)
		{
			this.scrollViewer = scrollViewer;
		}
	}

}
