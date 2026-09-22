using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Launcher.App.Views.Install;

public partial class InstallPageView : UserControl, IComponentConnector
{
	public FrameworkElement RootElement => PageRoot;

	public InstallPageView()
	{
		InitializeComponent();
	}

}
