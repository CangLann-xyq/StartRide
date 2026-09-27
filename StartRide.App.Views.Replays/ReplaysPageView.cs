using System.Windows;
using System.Windows.Controls;
using StartRide.App.ViewModels.Replays;

namespace StartRide.App.Views.Replays;

public partial class ReplaysPageView : UserControl
{
	public FrameworkElement RootElement => PageRoot;

	/// <summary>给 Shell 用的页面 VM（导航栏与页内视图模式的联动）。</summary>
	public ReplaysPageViewModel ViewModel => (ReplaysPageViewModel)DataContext;

	public ReplaysPageView()
	{
		InitializeComponent();
		DataContext = new ReplaysPageViewModel();
	}
}
