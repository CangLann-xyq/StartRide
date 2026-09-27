using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Launcher.Application.Accounts;

namespace StartRide.App.ViewModels.Account;

public sealed class AccountDetailsViewModel : ObservableObject
{
	private readonly AccountPageViewModel parent;

	public AccountListViewModel AccountList => parent.AccountList;

	public AccountAppearanceViewModel Appearance => parent.Appearance;

	public AccountOfflineUuidViewModel OfflineUuid => parent.OfflineUuid;

	public LauncherAccount? SelectedAccount => parent.SelectedAccount;

	public bool CanRenameSelectedAccount
	{
		get
		{
			LauncherAccount selectedAccount = SelectedAccount;
			if (selectedAccount != null)
			{
				return !selectedAccount.IsThirdParty;
			}
			return false;
		}
	}

	public ICommand RequestRenameAccountCommand => parent.RequestRenameAccountCommand;

	public AccountDetailsViewModel(AccountPageViewModel parent)
	{
		this.parent = parent;
		parent.PropertyChanged += OnParentPropertyChanged;
		parent.AccountList.PropertyChanged += OnChildPropertyChanged;
		parent.Appearance.PropertyChanged += OnChildPropertyChanged;
		parent.OfflineUuid.PropertyChanged += OnChildPropertyChanged;
	}

	private void OnParentPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		OnPropertyChanged(e.PropertyName);
	}

	private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender == parent.AccountList)
		{
			OnPropertyChanged("AccountList");
			if (e.PropertyName == "SelectedAccount")
			{
				OnPropertyChanged("SelectedAccount");
				OnPropertyChanged("CanRenameSelectedAccount");
			}
		}
		else if (sender == parent.Appearance)
		{
			OnPropertyChanged("Appearance");
		}
		else if (sender == parent.OfflineUuid)
		{
			OnPropertyChanged("OfflineUuid");
		}
	}
}
