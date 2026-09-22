using System.Collections.Generic;
using Launcher.App.Models;
using Launcher.App.Resources;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Account;

internal static class AccountSkinModelOptionFactory
{
	public static IEnumerable<AccountSkinModelOption> Create()
	{
		return new global::_003C_003Ez__ReadOnlyArray<AccountSkinModelOption>(new AccountSkinModelOption[2]
		{
			new AccountSkinModelOption
			{
				Model = MinecraftSkinModel.Classic,
				Title = Strings.Account_SkinModelClassicTitle,
				Description = Strings.Account_SkinModelClassicDescription
			},
			new AccountSkinModelOption
			{
				Model = MinecraftSkinModel.Slim,
				Title = Strings.Account_SkinModelSlimTitle,
				Description = Strings.Account_SkinModelSlimDescription
			}
		});
	}
}
