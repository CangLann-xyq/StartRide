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

	/// <summary>下载按钮点击：从 DataContext 取当前模组条目，触发异步下载。</summary>
	private async void OnDownloadModClick(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement fe && fe.DataContext is RepositoryModItem item
			&& DataContext is ModsPageViewModel vm)
		{
			await vm.DownloadModAsync(item);
		}
	}

	/// <summary>
	/// 在线列表滚动到底（剩余不足 480px）自动续拉下一波页面。
	/// ⚠️ 全库共 ~89 页 / 8800+ 条，一屏只显示十几条，所以这个阈值要留得宽一点，
	/// 否则用户要一路拖到底部才会触发。搜索过滤时跳过（避免短列表连环触发）。
	/// </summary>
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
