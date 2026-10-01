using System.Windows;
using System.Windows.Controls;
using StartRide.App.ViewModels.Replays;

namespace StartRide.App.Views.Replays;

public partial class ReplaysPageView : UserControl
{
	public FrameworkElement RootElement => PageRoot;

	public ReplaysPageViewModel ViewModel => (ReplaysPageViewModel)DataContext;

	public ReplaysPageView()
	{
		InitializeComponent();
		DataContext = new ReplaysPageViewModel();
	}
}
