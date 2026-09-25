using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using StartRide.App.ViewModels.Settings;

namespace StartRide.App.Views.Settings;

public partial class GeneralSettingsView : UserControl, IComponentConnector
{
	private bool isRestoringMinecraftDirectorySelection;

	public GeneralSettingsView()
	{
		InitializeComponent();
	}

	private void MinecraftDirectoryListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (!IsMinecraftDirectorySelectionBlocked() || !(sender is ListBox stop))
		{
			return;
		}
		object originalSource = e.OriginalSource;
		DependencyObject val = (DependencyObject)((originalSource is DependencyObject) ? originalSource : null);
		if (val != null && FindAncestor<ButtonBase>(val, (DependencyObject)(object)stop) == null)
		{
			ListBoxItem listBoxItem = FindAncestor<ListBoxItem>(val, (DependencyObject)(object)stop);
			if (listBoxItem != null && !listBoxItem.IsSelected)
			{
				e.Handled = true;
			}
		}
	}

	private void MinecraftDirectoryListBox_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Invalid comparison between Unknown and I4
		if (!IsMinecraftDirectorySelectionBlocked() || !(sender is ListBox stop))
		{
			return;
		}
		object originalSource = e.OriginalSource;
		DependencyObject val = (DependencyObject)((originalSource is DependencyObject) ? originalSource : null);
		if (val != null && FindAncestor<ButtonBase>(val, (DependencyObject)(object)stop) == null)
		{
			Key key = e.Key;
			if ((int)key - 18 <= 8)
			{
				e.Handled = true;
			}
		}
	}

	private void MinecraftDirectoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (isRestoringMinecraftDirectorySelection || !IsMinecraftDirectorySelectionBlocked() || !(sender is ListBox listBox) || !(base.DataContext is GeneralSettingsViewModel generalSettingsViewModel) || listBox.SelectedItem == generalSettingsViewModel.SelectedMinecraftDirectory)
		{
			return;
		}
		isRestoringMinecraftDirectorySelection = true;
		try
		{
			listBox.SelectedItem = generalSettingsViewModel.SelectedMinecraftDirectory;
		}
		finally
		{
			isRestoringMinecraftDirectorySelection = false;
		}
	}

	private bool IsMinecraftDirectorySelectionBlocked()
	{
		if (base.DataContext is GeneralSettingsViewModel generalSettingsViewModel)
		{
			return generalSettingsViewModel.IsMinecraftDirectoryChangeBlocked;
		}
		return false;
	}

	private static T? FindAncestor<T>(DependencyObject source, DependencyObject stop) where T : DependencyObject
	{
		DependencyObject val = source;
		while (val != null && val != stop)
		{
			T val2 = (T)(object)((val is T) ? val : null);
			if (val2 != null)
			{
				return val2;
			}
			val = GetParent(val);
		}
		return default(T);
	}

	private static DependencyObject? GetParent(DependencyObject current)
	{
		if ((!(current is Visual) && !(current is Visual3D)) || 1 == 0)
		{
			return LogicalTreeHelper.GetParent(current);
		}
		return VisualTreeHelper.GetParent(current);
	}
}
