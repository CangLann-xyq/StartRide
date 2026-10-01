using System;
using System.Linq;
using System.Threading.Tasks;
using Launcher.Application.Accounts;
using StartRide.App.ViewModels.Account;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using StartRide.Core;

namespace StartRide.Services;

public static class StartRideAccountIdRepair
{
    private const string SteamIdPrefix = "steam-";

    public static async Task<int> RunAsync(AccountListViewModel accountList, ILogger? logger = null)
    {
        int repaired = Normalize(accountList, logger);
        if (repaired > 0 && accountList != null)
        {
            await accountList.PersistAccountOrderAsync();
        }

        return repaired;
    }

    public static int Normalize(AccountListViewModel? accountList, ILogger? logger = null)
    {
        if (accountList == null)
        {
            return 0;
        }

        int repaired = 0;
        foreach (LauncherAccount account in accountList.Accounts.Select(item => item.Account).ToArray())
        {
            if (account == null)
            {
                continue;
            }

            string? expected = ResolveExpectedId(account);
            if (expected == null || string.Equals(account.Uuid, expected, StringComparison.Ordinal))
            {
                continue;
            }

            LauncherAccount corrected = AccountMapper.WithOfflineUuid(account, account.OfflineUuidGenerationMode, expected);
            if (accountList.TryReplaceAccount(account.Id, corrected))
            {
                repaired++;
                logger?.LogInformation(
                    "Player id repaired. AccountId={AccountId} From={From} To={To}",
                    account.Id,
                    account.Uuid,
                    expected);
            }
        }

        return repaired;
    }

    private static string? ResolveExpectedId(LauncherAccount account)
    {
        if (account.Id != null && account.Id.StartsWith(SteamIdPrefix, StringComparison.Ordinal))
        {
            string steamId = account.Id.Substring(SteamIdPrefix.Length);
            return StartRidePlayerId.TryNormalize(steamId, out string normalized) ? normalized : null;
        }

        if (account.OfflineUuidGenerationMode == OfflineUuidGenerationMode.Standard
            && StartRidePlayerId.IsUuidFormat(account.Uuid))
        {
            return StartRidePlayerId.Create(account.DisplayName);
        }

        return null;
    }
}
