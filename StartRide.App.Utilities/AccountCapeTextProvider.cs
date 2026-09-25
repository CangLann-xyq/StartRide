using StartRide.App.Resources;
using Launcher.Application.Accounts;

namespace StartRide.App.Utilities;

internal static class AccountCapeTextProvider
{
	public static string GetDisplayName(AccountCapeOption cape)
	{
		if (cape.IsNone)
		{
			return Strings.Cape_NoneState;
		}
		if (!string.IsNullOrWhiteSpace(cape.DisplayName))
		{
			return cape.DisplayName;
		}
		return Strings.Cape_UnnamedDisplayName;
	}
}
