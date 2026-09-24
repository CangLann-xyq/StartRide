using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Services;
using Launcher.Application.Accounts;

namespace Launcher.App.ViewModels.Account;

public sealed class AccountPageViewModel : ObservableObject
{
	private readonly IAccountDialogService dialogService;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestAddAccountCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<AccountItemViewModel>? requestDeleteAccountCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestRenameAccountCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestCancelAddAccountDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestBackAddAccountDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? requestConfirmAddAccountDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestSelectAllThirdPartyProfilesCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? requestRetryThirdPartyProfileImportCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestCancelDeleteAccountDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? requestConfirmDeleteAccountDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestCancelRenameAccountDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? requestConfirmRenameAccountDialogCommand;

	public AccountListViewModel AccountList { get; }

	public AccountDialogViewModel Dialog { get; }

	public AccountAppearanceViewModel Appearance { get; }

	public AccountOfflineUuidViewModel OfflineUuid { get; }

	public AccountPurchaseViewModel Purchase { get; }

	public AccountDetailsViewModel Details { get; }

	public LauncherAccount? SelectedAccount
	{
		get
		{
			return AccountList.SelectedAccount;
		}
		set
		{
			if (value == null)
			{
				AccountList.ClearSelectedAccount();
			}
			else
			{
				AccountList.SelectAccount(value);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestAddAccountCommand => requestAddAccountCommand ?? (requestAddAccountCommand = new RelayCommand(RequestAddAccount));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<AccountItemViewModel> RequestDeleteAccountCommand => requestDeleteAccountCommand ?? (requestDeleteAccountCommand = new RelayCommand<AccountItemViewModel>(RequestDeleteAccount));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestRenameAccountCommand => requestRenameAccountCommand ?? (requestRenameAccountCommand = new RelayCommand(RequestRenameAccount));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestCancelAddAccountDialogCommand => requestCancelAddAccountDialogCommand ?? (requestCancelAddAccountDialogCommand = new RelayCommand(RequestCancelAddAccountDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestBackAddAccountDialogCommand => requestBackAddAccountDialogCommand ?? (requestBackAddAccountDialogCommand = new RelayCommand(RequestBackAddAccountDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RequestConfirmAddAccountDialogCommand => requestConfirmAddAccountDialogCommand ?? (requestConfirmAddAccountDialogCommand = new AsyncRelayCommand(RequestConfirmAddAccountDialogAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestSelectAllThirdPartyProfilesCommand => requestSelectAllThirdPartyProfilesCommand ?? (requestSelectAllThirdPartyProfilesCommand = new RelayCommand(RequestSelectAllThirdPartyProfiles));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RequestRetryThirdPartyProfileImportCommand => requestRetryThirdPartyProfileImportCommand ?? (requestRetryThirdPartyProfileImportCommand = new AsyncRelayCommand(RequestRetryThirdPartyProfileImportAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestCancelDeleteAccountDialogCommand => requestCancelDeleteAccountDialogCommand ?? (requestCancelDeleteAccountDialogCommand = new RelayCommand(RequestCancelDeleteAccountDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RequestConfirmDeleteAccountDialogCommand => requestConfirmDeleteAccountDialogCommand ?? (requestConfirmDeleteAccountDialogCommand = new AsyncRelayCommand(RequestConfirmDeleteAccountDialogAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestCancelRenameAccountDialogCommand => requestCancelRenameAccountDialogCommand ?? (requestCancelRenameAccountDialogCommand = new RelayCommand(RequestCancelRenameAccountDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RequestConfirmRenameAccountDialogCommand => requestConfirmRenameAccountDialogCommand ?? (requestConfirmRenameAccountDialogCommand = new AsyncRelayCommand(RequestConfirmRenameAccountDialogAsync));

	public AccountPageViewModel(AccountListViewModel accountList, AccountDialogViewModel dialog, AccountAppearanceViewModel appearance, AccountOfflineUuidViewModel offlineUuid, AccountPurchaseViewModel purchase, IAccountDialogService dialogService)
	{
		AccountList = accountList;
		Dialog = dialog;
		Appearance = appearance;
		OfflineUuid = offlineUuid;
		Purchase = purchase;
		this.dialogService = dialogService;
		Details = new AccountDetailsViewModel(this);
		AccountList.PropertyChanged += delegate(object? _, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "SelectedAccount")
			{
				OnPropertyChanged("SelectedAccount");
			}
		};
	}

	public async Task InitializeAsync()
	{
		await AccountList.InitializeAsync();
		Appearance.RefreshAccountsSilentlyAsync();
		// StartRide：联机 ID 校正现在收敛在两个点上，不再需要在这里补一刀——
		// 加载侧在 AccountListViewModel.ApplyAccounts，落盘侧在 PersistAccountOrderAsync。
		// 详见 StartRide/Services/StartRideAccountIdRepair.cs。
	}

	public Task PrimeAsync()
	{
		return AccountList.PrimeAsync();
	}

	public void SelectAccount(LauncherAccount account)
	{
		AccountList.SelectAccount(account);
	}

	[RelayCommand]
	private void RequestAddAccount()
	{
		dialogService.ShowAddAccountDialog();
	}

	[RelayCommand]
	private void RequestDeleteAccount(AccountItemViewModel item)
	{
		dialogService.ShowDeleteAccountDialog(item.Account);
	}

	[RelayCommand]
	private void RequestRenameAccount()
	{
		dialogService.ShowRenameAccountDialog();
	}

	[RelayCommand]
	private void RequestCancelAddAccountDialog()
	{
		dialogService.CancelAddAccountDialog();
	}

	[RelayCommand]
	private void RequestBackAddAccountDialog()
	{
		dialogService.BackAddAccountDialog();
	}

	[RelayCommand]
	private Task RequestConfirmAddAccountDialogAsync()
	{
		return dialogService.ConfirmAddAccountDialogAsync();
	}

	[RelayCommand]
	private void RequestSelectAllThirdPartyProfiles()
	{
		dialogService.SelectAllThirdPartyProfiles();
	}

	[RelayCommand]
	private Task RequestRetryThirdPartyProfileImportAsync()
	{
		return dialogService.RetryThirdPartyProfileImportAsync();
	}

	[RelayCommand]
	private void RequestCancelDeleteAccountDialog()
	{
		dialogService.CancelDeleteAccountDialog();
	}

	[RelayCommand]
	private Task RequestConfirmDeleteAccountDialogAsync()
	{
		return dialogService.ConfirmDeleteAccountDialogAsync();
	}

	[RelayCommand]
	private void RequestCancelRenameAccountDialog()
	{
		dialogService.CancelRenameAccountDialog();
	}

	[RelayCommand]
	private Task RequestConfirmRenameAccountDialogAsync()
	{
		return dialogService.ConfirmRenameAccountDialogAsync();
	}
}
