using System.Windows;
using System.Windows.Controls;
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
}
