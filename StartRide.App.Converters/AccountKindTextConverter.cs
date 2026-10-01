using System;
using System.Globalization;
using System.Windows.Data;
using StartRide.App.Resources;
using Launcher.Application.Accounts;
using Launcher.Domain.Models;

namespace StartRide.App.Converters;

public sealed class AccountKindTextConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		LauncherAccount launcherAccount = value as LauncherAccount;
		if (launcherAccount != null && launcherAccount.Id.StartsWith("steam-", StringComparison.Ordinal))
		{
			return Strings.Account_TypeSteamTitle;
		}
		LauncherAccountKind? launcherAccountKind = ((launcherAccount != null) ? new LauncherAccountKind?(launcherAccount.Kind) : ((value is LauncherAccountKind value2) ? new LauncherAccountKind?(value2) : ((LauncherAccountKind?)null)));
		if (launcherAccount != null && launcherAccount.IsThirdParty && !string.IsNullOrWhiteSpace(launcherAccount.ThirdPartyPlatformName))
		{
			return string.Format(culture, Strings.Account_ThirdPartyPlatformFormat, Strings.Account_TypeThirdPartyTitle, launcherAccount.ThirdPartyPlatformName.Trim());
		}
		if (launcherAccountKind.HasValue)
		{
			return launcherAccountKind switch
			{
				LauncherAccountKind.Offline => Strings.Account_TypeOfflineTitle,
				LauncherAccountKind.Microsoft => Strings.Account_TypeMicrosoftTitle,
				LauncherAccountKind.ThirdParty => Strings.Account_TypeThirdPartyTitle,
				_ => string.Empty,
			};
		}
		return string.Empty;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
