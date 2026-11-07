using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using StartRide.App.ViewModels.Account;

namespace StartRide.App.Views.Account.Dialogs;

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

	private void CopyActivationCode_OnClick(object sender, RoutedEventArgs e)
	{
		if (base.DataContext is AccountPageViewModel accountPageViewModel)
		{
			string code = accountPageViewModel.Dialog.SteamActivationCode;
			if (string.IsNullOrEmpty(code))
			{
				return;
			}
			try
			{
				Clipboard.SetText(code);
			}
			catch
			{
				// 剪贴板被别的进程占着时静默失败，用户还能手动选中复制
			}
		}
	}

	private void OpenActivationGroup_OnClick(object sender, RoutedEventArgs e)
	{
		if (base.DataContext is AccountPageViewModel accountPageViewModel)
		{
			string url = accountPageViewModel.Dialog.SteamActivationGroupUrl;
			if (string.IsNullOrEmpty(url))
			{
				return;
			}
			try
			{
				Process.Start(new ProcessStartInfo(url)
				{
					UseShellExecute = true
				});
			}
			catch
			{
				// 打不开浏览器就算了，链接本身在界面上可复制
			}
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
