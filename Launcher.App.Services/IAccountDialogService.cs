using System.Threading.Tasks;
using Launcher.App.Controls;
using Launcher.App.ViewModels.Account;
using Launcher.App.Views.Account.Dialogs;
using Launcher.Application.Accounts;
using Launcher.Domain.Models;

namespace Launcher.App.Services;

public interface IAccountDialogService : IMicrosoftAccountReauthenticationDialogService
{
	void Attach(AccountPageViewModel accountPage, DialogHost addAccountHost, AddAccountDialogView addAccountView, DialogHost deleteAccountHost, DialogHost renameAccountHost, DialogHost skinModelDialogHost, DialogHost skinManagerDialogHost);

	void ShowAddAccountDialog();

	void ShowThirdPartyAddAccountDialog(string authenticationServer);

	Task<bool> ShowThirdPartyReauthenticationDialogAsync(LauncherAccount account);

	void ShowDeleteAccountDialog(LauncherAccount account);

	void ShowRenameAccountDialog();

	void ShowSkinModelDialog(string skinFilePath);

	void ShowSkinModelDialog(MinecraftSkinModel skinModel);

	void ShowSkinFormatErrorDialog();

	void ShowSkinManagerDialog();

	void CancelAddAccountDialog();

	void BackAddAccountDialog();

	Task ConfirmAddAccountDialogAsync();

	void SelectAllThirdPartyProfiles();

	Task RetryThirdPartyProfileImportAsync();

	void CancelDeleteAccountDialog();

	Task ConfirmDeleteAccountDialogAsync();

	void CancelRenameAccountDialog();

	Task ConfirmRenameAccountDialogAsync();

	void CancelSkinModelDialog();

	Task ConfirmSkinModelDialogAsync();

	void CancelSkinManagerDialog();

	void Prewarm();
}
