using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace StartRide.App.Views.General;

public partial class GeneralPageView : UserControl, IComponentConnector
{
	public FrameworkElement RootElement => PageRoot;

	public GeneralPageView()
	{
		InitializeComponent();
	}
}
