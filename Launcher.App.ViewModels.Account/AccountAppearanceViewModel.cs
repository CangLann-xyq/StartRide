using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Launcher.App.Services;
using Launcher.Application.Accounts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Account;

public sealed class AccountAppearanceViewModel : ObservableObject, IDisposable
{
	private readonly AccountListViewModel accountList;

	private readonly AccountAppearanceOperationCoordinator operations;

	public AccountProfileViewModel Profile { get; }

	public AccountSkinLibraryViewModel SkinLibrary { get; }

	public AccountCapeViewModel Cape { get; }

	public AccountAppearanceViewModel(AccountListViewModel accountList, IMicrosoftAccountService microsoftAccountService, IThirdPartyAccountService thirdPartyAccountService, IAccountSkinLibraryService skinLibraryService, AccountSkinModelDialogViewModel skinModelDialog, IAccountDialogService dialogService, IFilePickerService filePickerService, IMinecraftSkinFileValidator skinFileValidator, ILogger<AccountAppearanceViewModel>? logger = null, IFloatingMessageService? floatingMessageService = null)
	{
		this.accountList = accountList;
		ILogger<AccountAppearanceViewModel> logger2 = logger ?? NullLogger<AccountAppearanceViewModel>.Instance;
		operations = new AccountAppearanceOperationCoordinator();
		MicrosoftAccountOperationRetryHandler microsoftOperationRetryHandler = new MicrosoftAccountOperationRetryHandler(accountList, dialogService);
		Profile = new AccountProfileViewModel(accountList, microsoftAccountService, thirdPartyAccountService, operations, microsoftOperationRetryHandler, floatingMessageService, logger2);
		SkinLibrary = new AccountSkinLibraryViewModel(accountList, microsoftAccountService, skinLibraryService, skinModelDialog, dialogService, filePickerService, skinFileValidator, Profile, microsoftOperationRetryHandler, logger2);
		Cape = new AccountCapeViewModel(accountList, microsoftAccountService, Profile, microsoftOperationRetryHandler, logger2);
		Profile.PropertyChanged += Child_PropertyChanged;
		SkinLibrary.PropertyChanged += Child_PropertyChanged;
		Cape.PropertyChanged += Child_PropertyChanged;
		accountList.PropertyChanged += AccountList_PropertyChanged;
		ApplySelectedAccount(accountList.SelectedAccount);
	}

	public Task RefreshAccountsSilentlyAsync()
	{
		return Profile.RefreshAccountsSilentlyAsync();
	}

	public Task RefreshCurrentSecondaryContentAsync()
	{
		if (accountList.SelectedAccount != null && !Profile.IsBusy)
		{
			return Cape.RefreshAsync();
		}
		return Task.CompletedTask;
	}

	public void Dispose()
	{
		accountList.PropertyChanged -= AccountList_PropertyChanged;
		Profile.PropertyChanged -= Child_PropertyChanged;
		SkinLibrary.PropertyChanged -= Child_PropertyChanged;
		Cape.PropertyChanged -= Child_PropertyChanged;
		operations.Dispose();
	}

	private void AccountList_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "SelectedAccount")
		{
			ApplySelectedAccount(accountList.SelectedAccount);
		}
	}

	private void ApplySelectedAccount(LauncherAccount? account)
	{
		Profile.SetAccount(account);
		SkinLibrary.SetAccount(account);
		Cape.SetAccount(account);
		OnPropertyChanged("Profile");
		OnPropertyChanged("SkinLibrary");
		OnPropertyChanged("Cape");
	}

	private void Child_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender == Profile)
		{
			OnPropertyChanged("Profile");
		}
		else if (sender == SkinLibrary)
		{
			OnPropertyChanged("SkinLibrary");
		}
		else if (sender == Cape)
		{
			OnPropertyChanged("Cape");
		}
	}
}
