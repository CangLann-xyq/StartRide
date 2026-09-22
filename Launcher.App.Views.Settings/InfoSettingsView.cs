using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Launcher.App.Views.Settings;

public partial class InfoSettingsView : UserControl, IComponentConnector
{
	public InfoSettingsView()
	{
		InitializeComponent();
	}

	private void CheckUpdatesButton_OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
	{
		e.Handled = true;
	}

}
