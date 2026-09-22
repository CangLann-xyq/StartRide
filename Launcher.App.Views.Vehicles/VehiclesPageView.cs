using System.Windows;
using System.Windows.Controls;
using Launcher.App.ViewModels.Vehicles;

namespace Launcher.App.Views.Vehicles;

public partial class VehiclesPageView : UserControl
{
	public FrameworkElement RootElement => PageRoot;

	public VehiclesPageView()
	{
		InitializeComponent();
		DataContext = new VehiclesPageViewModel();
	}
}
