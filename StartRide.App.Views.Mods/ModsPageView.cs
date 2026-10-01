using System.Windows;
using System.Windows.Controls;
using StartRide.App.ViewModels.Mods;

namespace StartRide.App.Views.Mods;

public partial class ModsPageView : UserControl
{
	public FrameworkElement RootElement => PageRoot;

	public ModsPageView()
	{
		InitializeComponent();
		DataContext = new ModsPageViewModel();
	}

	private async void OnDownloadModClick(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement fe && fe.DataContext is RepositoryModItem item
			&& DataContext is ModsPageViewModel vm)
		{
			await vm.DownloadModAsync(item);
		}
	}

	private void OnRepositoryScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (e.ExtentHeight <= 0 || DataContext is not ModsPageViewModel vm)
		{
			return;
		}
		if (!string.IsNullOrWhiteSpace(vm.SearchText))
		{
			return;
		}
		if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 480)
		{
			_ = vm.TryLoadMoreAsync();
		}
	}
}
