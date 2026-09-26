using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StartRide.App.ViewModels.Vehicles;

namespace StartRide.App.Views.Vehicles;

public partial class VehiclesPageView : UserControl
{
	public FrameworkElement RootElement => PageRoot;

	public VehiclesPageView()
	{
		InitializeComponent();
		DataContext = new VehiclesPageViewModel();
	}

	/// <summary>搜索框里按 Esc = 清空关键词（在 324 辆车里搜完想回全量列表，不用把字一个个删掉）。</summary>
	private void VehicleSearch_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape && DataContext is VehiclesPageViewModel vm)
		{
			vm.SearchText = "";
			e.Handled = true;
		}
	}

	/// <summary>
	/// 双击列表项 = 在资源管理器中定位这辆车。
	///
	/// ⚠️ 挂在 ListBox 的 **Preview**MouseLeftButtonDown 上，不要挂在项模板里的控件上：
	/// 列表项内部那个按钮在按下时会捕获鼠标，事件路由的起点就变成按钮自己，
	/// 挂在模板控件上会时灵时不灵；挂在 ListBox 的预览事件上则一定先经过这里
	/// （预览事件从窗口往下隧道路由，捕获与否都跑不掉），
	/// 再用 ContainerFromElement 把命中的元素换回 ListBoxItem 取数据，最稳。
	/// </summary>
	private void VehiclesList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ClickCount != 2 || DataContext is not VehiclesPageViewModel vm)
		{
			return;
		}
		if (e.OriginalSource is not DependencyObject source)
		{
			return;
		}
		if (ItemsControl.ContainerFromElement((ItemsControl)sender, source) is ListBoxItem row &&
			row.DataContext is VehicleItem vehicle)
		{
			vm.OpenVehicleLocationCommand.Execute(vehicle);
		}
	}
}
