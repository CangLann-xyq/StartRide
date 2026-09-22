using System.Windows;
using System.Windows.Controls;
using Launcher.App.ViewModels.Replays;

namespace Launcher.App.Views.Replays;

public partial class ReplaysPageView : UserControl
{
	public FrameworkElement RootElement => PageRoot;

	public ReplaysPageView()
	{
		InitializeComponent();
		DataContext = new ReplaysPageViewModel();
	}
}
