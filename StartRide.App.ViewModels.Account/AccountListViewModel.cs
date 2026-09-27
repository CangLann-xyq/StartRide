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

namespace StartRide.App.ViewModels.Account;

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
		// StartRide：账户列表落盘的唯一收敛点，落盘前统一校正联机 ID。
		// 上游框架在重建账号对象时会按昵称重算联机 ID（用的是继承来的 Minecraft 离线
		// UUID 算法），而换头像/静默刷新/重命名等多条流程各自都会落一次盘——谁最后落
		// 谁说了算，只在加载后校正一次会被后面的流程覆盖回去。详见
		// StartRide/Services/StartRideAccountIdRepair.cs。
		StartRide.Services.StartRideAccountIdRepair.Normalize(this, logger);
		selectedAccountId = SelectedItem?.Id;
		AccountItemViewModel[] snapshot = Accounts.ToArray();
		LogAccountSave(snapshot);
		Task persist = accountStore.SaveOrderAsync(selectedAccountId, snapshot.Select((AccountItemViewModel item) => item.Account).ToArray());

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

	/// <summary>
	/// StartRide：记录每一次账户状态落盘。这里被上游框架的十来处流程调用
	/// （换头像、改披风、重命名、重选账号……），是"到底是谁把联机 ID 改回 UUID"的
	/// 唯一收敛观测点，所以日志打在这一层，不打在调用方。
	/// </summary>
	private void LogAccountSave(AccountItemViewModel[] snapshot)
	{
		try
		{
			string callers = string.Join(
				" <- ",
				new System.Diagnostics.StackTrace()
					.GetFrames()
					.Select(frame => frame.GetMethod())
					.Where(method => method != null && method.DeclaringType != null)
					.Select(method => method.DeclaringType.Name + "." + method.Name)
					.Where(name => !name.StartsWith("LogAccountSave", StringComparison.Ordinal)
						&& !name.StartsWith("PersistAccountOrderAsync", StringComparison.Ordinal))
					.Take(3));
			logger.LogInformation(
				"ACCOUNT-SAVE selected={Selected} ids=[{Ids}] callers={Callers}",
				selectedAccountId,
				string.Join(",", snapshot.Select(item => item.Account.Uuid)),
				callers);
		}
		catch
		{
			// 诊断日志不能影响正常落盘
		}
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
		// StartRide：加载侧的唯一入口。落盘时框架会把 Uuid 按昵称重算成继承来的 Minecraft
		// 离线 UUID（实测：往文件里塞一个合法 GUID 哨兵，启动一次也被换掉），所以加载后
		// 必须立刻校正一次，否则界面上会出现一段"显示 MC UUID"的窗口期。
		StartRide.Services.StartRideAccountIdRepair.Normalize(this, logger);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedItemChanged(AccountItemViewModel? value)
	{
		UpdateSelectionFlags();
		OnPropertyChanged("SelectedAccount");
	}
}
