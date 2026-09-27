using System.Collections.Generic;
using StartRide.App.Models;
using StartRide.App.Resources;

namespace StartRide.App.ViewModels.Account;

internal static class AccountTypeOptionFactory
{
	/// <summary>
	/// StartRide 不提供离线版 BeamNG 下载，因此只保留「Steam 账户」一种类型：
	/// 直接读取本机 Steam 登录信息完成授权，不再有本地昵称（离线）账户。
	/// </summary>
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
