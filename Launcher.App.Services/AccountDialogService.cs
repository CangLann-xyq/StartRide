using System.Threading.Tasks;
using Launcher.App.Controls;
using Launcher.App.ViewModels.Account;
using Launcher.App.Views.Account.Dialogs;
using Launcher.Application.Accounts;
using Launcher.Domain.Models;

namespace Launcher.App.Services;

public sealed class AccountDialogService : IAccountDialogService, IMicrosoftAccountReauthenticationDialogService
{
	private AccountPageViewModel? accountPage;

	private DialogHost? addAccountHost;

	private AddAccountDialogView? addAccountView;

	private DialogHost? deleteAccountHost;

	private DialogHost? renameAccountHost;

	private DialogHost? skinModelDialogHost;

	private DialogHost? skinManagerDialogHost;

	private TaskCompletionSource<bool>? thirdPartyReauthenticationCompletion;

	private TaskCompletionSource<bool>? microsoftReauthenticationCompletion;

	public void Attach(AccountPageViewModel accountPage, DialogHost addAccountHost, AddAccountDialogView addAccountView, DialogHost deleteAccountHost, DialogHost renameAccountHost, DialogHost skinModelDialogHost, DialogHost skinManagerDialogHost)
	{
		this.accountPage = accountPage;
		this.addAccountHost = addAccountHost;
		this.addAccountView = addAccountView;
		this.deleteAccountHost = deleteAccountHost;
		this.renameAccountHost = renameAccountHost;
		this.skinModelDialogHost = skinModelDialogHost;
		this.skinManagerDialogHost = skinManagerDialogHost;
	}

	public void ShowAddAccountDialog()
	{
		if (accountPage != null && addAccountHost != null)
		{
			accountPage.Dialog.OpenAddAccountDialog();
			addAccountHost.Show();
		}
	}

	public void ShowThirdPartyAddAccountDialog(string authenticationServer)
	{
		if (accountPage == null || addAccountHost == null || addAccountView == null)
		{
			return;
		}
		if (addAccountHost.IsOpen && accountPage.Dialog.IsAddAccountDialogOpen && (accountPage.Dialog.IsAccountTypeStep || accountPage.Dialog.IsThirdPartyCredentialsStep))
		{
			bool isAccountTypeStep = accountPage.Dialog.IsAccountTypeStep;
			double actualHeight = addAccountHost.SurfaceBorder.ActualHeight;
			if (accountPage.Dialog.ApplyThirdPartyAuthenticationServer(authenticationServer))
			{
				addAccountView.ClearThirdPartyPassword();
			}
			if (isAccountTypeStep)
			{
				addAccountHost.AnimateSizeChange(actualHeight);
			}
		}
		else
		{
			addAccountView.ClearThirdPartyPassword();
			accountPage.Dialog.OpenThirdPartyAddAccountDialog(authenticationServer);
			addAccountHost.Show();
		}
	}

	public Task<bool> ShowThirdPartyReauthenticationDialogAsync(LauncherAccount account)
	{
		if (accountPage == null || addAccountHost == null || addAccountView == null || !account.IsThirdParty)
		{
			return Task.FromResult(result: false);
		}
		thirdPartyReauthenticationCompletion?.TrySetResult(result: false);
		thirdPartyReauthenticationCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		addAccountView.ClearThirdPartyPassword();
		accountPage.Dialog.OpenThirdPartyReauthenticationDialog(account);
		addAccountHost.Show();
		return thirdPartyReauthenticationCompletion.Task;
	}

	public Task<bool> ShowMicrosoftReauthenticationDialogAsync(LauncherAccount account)
	{
		if (accountPage == null || addAccountHost == null || !account.IsMicrosoft)
		{
			return Task.FromResult(result: false);
		}
		microsoftReauthenticationCompletion?.TrySetResult(result: false);
		microsoftReauthenticationCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		accountPage.Dialog.OpenMicrosoftReauthenticationDialog(account);
		addAccountHost.Show();
		return microsoftReauthenticationCompletion.Task;
	}

	public void ShowDeleteAccountDialog(LauncherAccount account)
	{
		if (accountPage != null && deleteAccountHost != null)
		{
			accountPage.Dialog.OpenDeleteAccountDialog(account);
			deleteAccountHost.Show();
		}
	}

	public void ShowRenameAccountDialog()
	{
		if (accountPage != null && renameAccountHost != null)
		{
			accountPage.Dialog.OpenRenameAccountDialog();
			if (accountPage.Dialog.IsRenameAccountDialogOpen)
			{
				renameAccountHost.Show();
			}
		}
	}

	public void ShowSkinModelDialog(string skinFilePath)
	{
		if (accountPage != null && skinModelDialogHost != null)
		{
			accountPage.Appearance.SkinLibrary.SkinModelDialog.Open(skinFilePath);
			skinModelDialogHost.Show();
		}
	}

	public void ShowSkinModelDialog(MinecraftSkinModel skinModel)
	{
		if (accountPage != null && skinModelDialogHost != null)
		{
			accountPage.Appearance.SkinLibrary.SkinModelDialog.OpenForExistingSkin(skinModel);
			skinModelDialogHost.Show();
		}
	}

	public void ShowSkinFormatErrorDialog()
	{
		if (accountPage != null && skinModelDialogHost != null)
		{
			accountPage.Appearance.SkinLibrary.SkinModelDialog.OpenFormatError();
			skinModelDialogHost.Show();
		}
	}

	public void ShowSkinManagerDialog()
	{
		if (accountPage != null && skinManagerDialogHost != null)
		{
			accountPage.Appearance.SkinLibrary.OpenManagerDialog();
			if (accountPage.Appearance.SkinLibrary.IsManagerDialogOpen)
			{
				skinManagerDialogHost.Show();
			}
		}
	}

	public void CancelAddAccountDialog()
	{
		if (accountPage == null || addAccountHost == null)
		{
			return;
		}
		bool isThirdPartyReauthenticationStep = accountPage.Dialog.IsThirdPartyReauthenticationStep;
		bool isMicrosoftReauthenticationMode = accountPage.Dialog.IsMicrosoftReauthenticationMode;
		accountPage.Dialog.CancelAddAccountDialog();
		if (!accountPage.Dialog.IsAddAccountDialogOpen)
		{
			addAccountView?.ClearThirdPartyPassword();
			addAccountHost.Hide(accountPage.Dialog.ResetAddAccountDialog);
			if (isThirdPartyReauthenticationStep)
			{
				thirdPartyReauthenticationCompletion?.TrySetResult(result: false);
				thirdPartyReauthenticationCompletion = null;
			}
			if (isMicrosoftReauthenticationMode)
			{
				microsoftReauthenticationCompletion?.TrySetResult(result: false);
				microsoftReauthenticationCompletion = null;
			}
		}
	}

	public void BackAddAccountDialog()
	{
		if (accountPage != null && addAccountHost != null)
		{
			double actualHeight = addAccountHost.SurfaceBorder.ActualHeight;
			accountPage.Dialog.BackToAddAccountTypeStep();
			addAccountHost.AnimateSizeChange(actualHeight);
		}
	}

	public async Task ConfirmAddAccountDialogAsync()
	{
		if (accountPage == null || addAccountHost == null)
		{
			return;
		}
		double previousHeight = addAccountHost.SurfaceBorder.ActualHeight;
		if (accountPage.Dialog.IsMicrosoftReauthenticationResultStep)
		{
			await CompleteMicrosoftReauthenticationAsync();
			return;
		}
		if (accountPage.Dialog.IsMicrosoftReauthenticationPromptStep)
		{
			await CompleteMicrosoftReauthenticationAsync();
			return;
		}
		if (accountPage.Dialog.IsAccountTypeStep && accountPage.Dialog.SelectedAccountTypeOption?.Kind == "Microsoft")
		{
			accountPage.Dialog.BeginMicrosoftAccountLogin();
			addAccountHost.AnimateSizeChange(previousHeight);
			double loginHeight = addAccountHost.SurfaceBorder.ActualHeight;
			await accountPage.Dialog.CompleteMicrosoftAccountLoginAsync();
			addAccountHost.AnimateSizeChange(loginHeight);
			return;
		}
		bool wasReauthentication = accountPage.Dialog.IsThirdPartyReauthenticationStep;
		bool isThirdPartyProfileSelectionStep = accountPage.Dialog.IsThirdPartyProfileSelectionStep;
		Task task = accountPage.Dialog.ConfirmAddAccountDialogAsync(addAccountView?.ThirdPartyPassword);
		if (isThirdPartyProfileSelectionStep)
		{
			addAccountHost.AnimateSizeChange(previousHeight);
		}
		await task;
		if (accountPage.Dialog.IsAddAccountDialogOpen)
		{
			addAccountHost.AnimateSizeChange(previousHeight);
			return;
		}
		addAccountView?.ClearThirdPartyPassword();
		addAccountHost.Hide(accountPage.Dialog.ResetAddAccountDialog);
		if (wasReauthentication)
		{
			thirdPartyReauthenticationCompletion?.TrySetResult(result: true);
			thirdPartyReauthenticationCompletion = null;
		}
	}

	private async Task CompleteMicrosoftReauthenticationAsync()
	{
		if (accountPage != null && addAccountHost != null)
		{
			double previousHeight = addAccountHost.SurfaceBorder.ActualHeight;
			if (await accountPage.Dialog.CompleteMicrosoftAccountReauthenticationAsync())
			{
				addAccountHost.Hide(accountPage.Dialog.ResetAddAccountDialog);
				microsoftReauthenticationCompletion?.TrySetResult(result: true);
				microsoftReauthenticationCompletion = null;
			}
			else if (accountPage.Dialog.IsAddAccountDialogOpen)
			{
				addAccountHost.AnimateSizeChange(previousHeight);
			}
		}
	}

	public void SelectAllThirdPartyProfiles()
	{
		accountPage?.Dialog.SelectAllThirdPartyProfiles();
	}

	public async Task RetryThirdPartyProfileImportAsync()
	{
		if (accountPage != null && addAccountHost != null)
		{
			double previousHeight = addAccountHost.SurfaceBorder.ActualHeight;
			Task task = accountPage.Dialog.RetryThirdPartyProfileImportAsync(addAccountView?.ThirdPartyPassword ?? string.Empty);
			addAccountHost.AnimateSizeChange(previousHeight);
			await task;
			if (accountPage.Dialog.IsAddAccountDialogOpen)
			{
				addAccountHost.AnimateSizeChange(previousHeight);
				return;
			}
			addAccountView?.ClearThirdPartyPassword();
			addAccountHost.Hide(accountPage.Dialog.ResetAddAccountDialog);
		}
	}

	public void CancelDeleteAccountDialog()
	{
		if (accountPage != null && deleteAccountHost != null)
		{
			accountPage.Dialog.CancelDeleteAccountDialog();
			deleteAccountHost.Hide();
		}
	}

	public async Task ConfirmDeleteAccountDialogAsync()
	{
		if (accountPage == null || deleteAccountHost == null)
		{
			return;
		}
		Task task = accountPage.Dialog.ConfirmDeleteAccountDialogAsync();
		if (!accountPage.Dialog.IsDeleteAccountDialogOpen)
		{
			deleteAccountHost.Hide();
			await task;
			return;
		}
		await task;
		if (!accountPage.Dialog.IsDeleteAccountDialogOpen)
		{
			deleteAccountHost.Hide();
		}
	}

	public void CancelRenameAccountDialog()
	{
		if (accountPage != null && renameAccountHost != null)
		{
			accountPage.Dialog.CancelRenameAccountDialog();
			if (!accountPage.Dialog.IsRenameAccountDialogOpen)
			{
				renameAccountHost.Hide(accountPage.Dialog.ResetRenameAccountDialog);
			}
		}
	}

	public async Task ConfirmRenameAccountDialogAsync()
	{
		if (accountPage != null && renameAccountHost != null)
		{
			double previousHeight = renameAccountHost.SurfaceBorder.ActualHeight;
			await accountPage.Dialog.ConfirmRenameAccountDialogAsync();
			if (accountPage.Dialog.IsRenameAccountDialogOpen)
			{
				renameAccountHost.AnimateSizeChange(previousHeight);
			}
			else
			{
				renameAccountHost.Hide(accountPage.Dialog.ResetRenameAccountDialog);
			}
		}
	}

	public void CancelSkinModelDialog()
	{
		if (accountPage != null && skinModelDialogHost != null)
		{
			accountPage.Appearance.SkinLibrary.SkinModelDialog.Cancel();
			skinModelDialogHost.Hide(accountPage.Appearance.SkinLibrary.SkinModelDialog.Reset);
		}
	}

	public async Task ConfirmSkinModelDialogAsync()
	{
		if (accountPage == null || skinModelDialogHost == null)
		{
			return;
		}
		Task task = accountPage.Appearance.SkinLibrary.ConfirmSkinModelDialogAsync();
		if (!accountPage.Appearance.SkinLibrary.SkinModelDialog.IsSkinModelDialogOpen)
		{
			skinModelDialogHost.Hide(accountPage.Appearance.SkinLibrary.SkinModelDialog.Reset);
			await task;
			return;
		}
		await task;
		if (!accountPage.Appearance.SkinLibrary.SkinModelDialog.IsSkinModelDialogOpen)
		{
			skinModelDialogHost.Hide(accountPage.Appearance.SkinLibrary.SkinModelDialog.Reset);
		}
	}

	public void CancelSkinManagerDialog()
	{
		if (accountPage != null && skinManagerDialogHost != null)
		{
			accountPage.Appearance.SkinLibrary.CloseManagerDialog();
			skinManagerDialogHost.Hide();
		}
	}

	public void Prewarm()
	{
		addAccountHost?.Prewarm();
		deleteAccountHost?.Prewarm();
		renameAccountHost?.Prewarm();
		skinModelDialogHost?.Prewarm();
		skinManagerDialogHost?.Prewarm();
	}
}
