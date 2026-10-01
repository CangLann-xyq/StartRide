using System.Collections.Generic;
using StartRide.App.Models;
using StartRide.App.Resources;

namespace StartRide.App.ViewModels.Account;

internal static class AccountTypeOptionFactory
{

	public static IEnumerable<AccountTypeOption> Create()
	{
		return new global::_003C_003Ez__ReadOnlyArray<AccountTypeOption>(new AccountTypeOption[1]
		{
			new AccountTypeOption
			{
				Kind = "Steam",
				Title = Strings.Account_TypeSteamTitle,
				Description = Strings.Account_TypeSteamDescription,
				Icon = "\ue8d3",
				IconKey = "account_page/account_page_add_account_dialog_steam"
			}
		});
	}
}
