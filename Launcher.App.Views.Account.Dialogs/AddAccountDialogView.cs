using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Launcher.App.ViewModels.Account;

namespace Launcher.App.Views.Account.Dialogs;

public partial class AddAccountDialogView : UserControl, IComponentConnector
{
	internal string ThirdPartyPassword => ThirdPartyPasswordBox.Password;

	public AddAccountDialogView()
	{
		InitializeComponent();
	}

	internal void ClearThirdPartyPassword()
	{
		ThirdPartyPasswordBox.Clear();
	}

	private void ThirdPartyPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
	{
		if (base.DataContext is AccountPageViewModel accountPageViewModel)
		{
			accountPageViewModel.Dialog.ThirdParty.UpdatePasswordState(ThirdPartyPasswordBox.Password.Length > 0);
		}
	}

	private void ThirdPartyCredentialsPanel_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		object newValue = e.NewValue;
		if (newValue is bool && !(bool)newValue && ThirdPartyPasswordBox != null && (!(base.DataContext is AccountPageViewModel accountPageViewModel) || (!accountPageViewModel.Dialog.IsThirdPartyProfileSelectionStep && !accountPageViewModel.Dialog.IsThirdPartyImportProgressStep && !accountPageViewModel.Dialog.IsThirdPartyImportResultStep)))
		{
			ThirdPartyPasswordBox.Clear();
		}
	}

}
