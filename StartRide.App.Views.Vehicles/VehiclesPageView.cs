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

	private void VehicleSearch_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape && DataContext is VehiclesPageViewModel vm)
		{
			vm.SearchText = "";
			e.Handled = true;
		}
	}

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
