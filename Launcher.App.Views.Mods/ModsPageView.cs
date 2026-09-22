using System.Windows;
using System.Windows.Controls;
using Launcher.App.ViewModels.Mods;

namespace Launcher.App.Views.Mods;

public partial class ModsPageView : UserControl
{
	public FrameworkElement RootElement => PageRoot;

	public ModsPageView()
	{
		InitializeComponent();
		DataContext = new ModsPageViewModel();
	}

	/// <summary>下载按钮点击：从 DataContext 取当前模组条目，触发异步下载。</summary>
	private async void OnDownloadModClick(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement fe && fe.DataContext is RepositoryModItem item
			&& DataContext is ModsPageViewModel vm)
		{
			await vm.DownloadModAsync(item);
		}
	}

	/// <summary>在线列表滚动到底（剩余不足 320px）自动续拉下一波页面。搜索过滤时跳过（避免短列表连环触发）。</summary>
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
		if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 320)
		{
			_ = vm.TryLoadMoreAsync();
		}
	}
}
