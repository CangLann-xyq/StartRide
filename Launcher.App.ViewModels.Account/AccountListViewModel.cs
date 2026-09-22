using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Accounts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StartRide.Core;

namespace Launcher.App.ViewModels.Account;

public sealed class AccountListViewModel : ObservableObject
{
	private readonly IAccountStore accountStore;

	private readonly ILogger<AccountListViewModel> logger;

	private string? selectedAccountId;

	[ObservableProperty]
	private AccountItemViewModel? selectedItem;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<AccountItemViewModel>? selectAccountCommand;

	public ObservableCollection<AccountItemViewModel> Accounts { get; } = new ObservableCollection<AccountItemViewModel>();

	public LauncherAccount? SelectedAccount => SelectedItem?.Account;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public AccountItemViewModel? SelectedItem
	{
		get
		{
			return selectedItem;
		}
		set
		{
			if (!EqualityComparer<AccountItemViewModel>.Default.Equals(selectedItem, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedItem);
				selectedItem = value;
				OnSelectedItemChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedItem);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<AccountItemViewModel> SelectAccountCommand => selectAccountCommand ?? (selectAccountCommand = new RelayCommand<AccountItemViewModel>(SelectAccount));

	public AccountListViewModel(IAccountStore accountStore, ILogger<AccountListViewModel>? logger = null)
	{
		this.accountStore = accountStore;
		this.logger = logger ?? NullLogger<AccountListViewModel>.Instance;
	}

	public async Task InitializeAsync()
	{
		AccountStoreSnapshot accountStoreSnapshot = await accountStore.LoadAsync();
		selectedAccountId = accountStoreSnapshot.SelectedAccountId;
		ApplyAccounts(accountStoreSnapshot.Accounts);
	}

	public async Task PrimeAsync()
	{
		AccountStoreSnapshot accountStoreSnapshot;
		try
		{
			accountStoreSnapshot = await accountStore.LoadCachedAsync();
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Priming accounts from the local account state failed.");
			return;
		}
		if (accountStoreSnapshot.Accounts.Count != 0 || !string.IsNullOrWhiteSpace(accountStoreSnapshot.SelectedAccountId))
		{
			selectedAccountId = accountStoreSnapshot.SelectedAccountId;
			ApplyAccounts(accountStoreSnapshot.Accounts);
		}
	}

	[RelayCommand]
	public void SelectAccount(AccountItemViewModel item)
	{
		SelectItem(item, persistSelection: true);
	}

	public void SelectAccount(LauncherAccount account)
	{
		SelectAccount(account, persistSelection: true);
	}

	public void SelectAccount(LauncherAccount account, bool persistSelection)
	{
		AccountItemViewModel accountItemViewModel = Accounts.FirstOrDefault((AccountItemViewModel candidate) => string.Equals(candidate.Id, account.Id, StringComparison.Ordinal));
		if (accountItemViewModel != null)
		{
			SelectItem(accountItemViewModel, persistSelection);
		}
	}

	public void SelectItem(AccountItemViewModel item, bool persistSelection)
	{
		SelectedItem = item;
		selectedAccountId = item.Id;
		if (persistSelection)
		{
			PersistAccountOrderAsync();
		}
	}

	public async Task AddAndSelectAsync(LauncherAccount account)
	{
		AccountItemViewModel previousSelection = SelectedItem;
		AccountItemViewModel item = new AccountItemViewModel(account);
		Accounts.Add(item);
		SelectItem(item, persistSelection: false);
		try
		{
			await PersistAccountOrderAsync();
		}
		catch
		{
			Accounts.Remove(item);
			if (previousSelection != null && Accounts.Contains(previousSelection))
			{
				SelectItem(previousSelection, persistSelection: false);
			}
			else
			{
				ClearSelectedAccount();
			}
			throw;
		}
	}

	public async Task RemoveAsync(LauncherAccount account)
	{
		AccountItemViewModel accountItemViewModel = Accounts.FirstOrDefault((AccountItemViewModel candidate) => string.Equals(candidate.Id, account.Id, StringComparison.Ordinal));
		if (accountItemViewModel != null)
		{
			if (SelectedItem == accountItemViewModel)
			{
				ClearSelectedAccount();
			}
			Accounts.Remove(accountItemViewModel);
			await PersistAccountOrderAsync();
		}
	}

	public void ReplaceSelectedAccount(LauncherAccount oldAccount, LauncherAccount newAccount)
	{
		if (!TryReplaceAccount(oldAccount.Id, newAccount))
		{
			AccountItemViewModel item = new AccountItemViewModel(newAccount);
			Accounts.Add(item);
			SelectItem(item, persistSelection: false);
		}
	}

	public async Task ReplaceSelectedAccountAndPersistAsync(LauncherAccount oldAccount, LauncherAccount newAccount)
	{
		ReplaceSelectedAccount(oldAccount, newAccount);
		try
		{
			await PersistAccountOrderAsync();
		}
		catch
		{
			TryReplaceAccount(newAccount.Id, oldAccount);
			throw;
		}
	}

	public bool TryReplaceAccount(string accountId, LauncherAccount newAccount)
	{
		int num = -1;
		for (int i = 0; i < Accounts.Count; i++)
		{
			if (string.Equals(Accounts[i].Id, accountId, StringComparison.Ordinal))
			{
				num = i;
				break;
			}
		}
		if (num < 0)
		{
			return false;
		}
		bool num2 = SelectedItem == Accounts[num];
		AccountItemViewModel value = new AccountItemViewModel(newAccount);
		Accounts[num] = value;
		if (num2)
		{
			SelectedItem = value;
			selectedAccountId = newAccount.Id;
		}
		UpdateSelectionFlags();
		return true;
	}

	public LauncherAccount? FindAccount(string accountId)
	{
		return Accounts.FirstOrDefault((AccountItemViewModel item) => string.Equals(item.Id, accountId, StringComparison.Ordinal))?.Account;
	}

	public void ClearSelectedAccount()
	{
		SelectedItem = null;
		selectedAccountId = null;
	}

	public Task PersistAccountOrderAsync()
	{
		selectedAccountId = SelectedItem?.Id;
		Task persist = accountStore.SaveOrderAsync(selectedAccountId, Accounts.Select((AccountItemViewModel item) => item.Account).ToArray());

		// 云同步：账户列表（昵称/steamId/头像）实时上报云端
		try
		{
			AppState.Current.CloudSync.PushAccounts(Accounts.Select((AccountItemViewModel item) => new
			{
				id = item.Id,
				name = item.Account.DisplayName,
				kind = item.Account.Kind.ToString(),
				uuid = item.Account.Uuid,
			}));
		}
		catch
		{
			// 云同步失败不影响本地账户
		}

		return persist;
	}

	private void UpdateSelectionFlags()
	{
		foreach (AccountItemViewModel account in Accounts)
		{
			account.IsSelected = account == SelectedItem;
		}
	}

	private void ApplyAccounts(IEnumerable<LauncherAccount> accounts)
	{
		Accounts.Clear();
		foreach (LauncherAccount account in accounts)
		{
			Accounts.Add(new AccountItemViewModel(account));
		}
		AccountItemViewModel accountItemViewModel = Accounts.FirstOrDefault((AccountItemViewModel item) => string.Equals(item.Id, selectedAccountId, StringComparison.Ordinal));
		if (accountItemViewModel != null)
		{
			SelectItem(accountItemViewModel, persistSelection: false);
		}
		else
		{
			ClearSelectedAccount();
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedItemChanged(AccountItemViewModel? value)
	{
		UpdateSelectionFlags();
		OnPropertyChanged("SelectedAccount");
	}
}
