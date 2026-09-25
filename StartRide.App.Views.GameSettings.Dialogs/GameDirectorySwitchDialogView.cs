using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using StartRide.App.ViewModels.Settings;

namespace StartRide.App.Views.GameSettings.Dialogs;

public partial class GameDirectorySwitchDialogView : UserControl, IComponentConnector
{
	private bool isRestoringSelection;

	public GameDirectorySwitchDialogView()
	{
		InitializeComponent();
	}

	private void DirectoryListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (!IsSelectionBlocked() || !(sender is ListBox stop))
		{
			return;
		}
		object originalSource = e.OriginalSource;
		DependencyObject val = (DependencyObject)((originalSource is DependencyObject) ? originalSource : null);
		if (val != null)
		{
			ListBoxItem listBoxItem = FindAncestor<ListBoxItem>(val, (DependencyObject)(object)stop);
			if (listBoxItem != null && !listBoxItem.IsSelected)
			{
				e.Handled = true;
			}
		}
	}

	private void DirectoryListBox_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Invalid comparison between Unknown and I4
		if (IsSelectionBlocked())
		{
			Key key = e.Key;
			if ((int)key - 18 <= 8)
			{
				e.Handled = true;
			}
		}
	}

	private void DirectoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (isRestoringSelection || !IsSelectionBlocked() || !(sender is ListBox listBox) || !(base.DataContext is GameDirectorySwitchDialogViewModel minecraftDirectorySwitchDialogViewModel) || listBox.SelectedItem == minecraftDirectorySwitchDialogViewModel.SelectedDirectory)
		{
			return;
		}
		isRestoringSelection = true;
		try
		{
			listBox.SelectedItem = minecraftDirectorySwitchDialogViewModel.SelectedDirectory;
		}
		finally
		{
			isRestoringSelection = false;
		}
	}

	private bool IsSelectionBlocked()
	{
		if (base.DataContext is GameDirectorySwitchDialogViewModel minecraftDirectorySwitchDialogViewModel)
		{
			return minecraftDirectorySwitchDialogViewModel.IsChangeBlockedByActiveTasks;
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
