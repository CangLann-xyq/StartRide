using System;
using System.Linq;
using System.Threading.Tasks;
using Launcher.Application.Accounts;
using Launcher.App.ViewModels.Account;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using StartRide.Core;

namespace StartRide.Services;

/// <summary>
/// 启动时校正联机 ID。
///
/// 为什么需要它：<c>AccountMapper</c> 在框架层是"按昵称重新推导联机 ID"的思路——任何一次
/// 账号对象重建（换头像、换披风、迁移数据……）都可能把联机 ID 重算一遍，而它用的算法是
/// 从上游继承下来的 Minecraft 离线 UUID（<c>MD5("OfflinePlayer:" + 昵称)</c>）。
/// 结果就是：明明已经改成 SteamID64 的账号，下次启动一看又变回了 <c>196a00b6-…</c> 这种 UUID。
/// 这个类是同一个 DLL 改不动的兜底：在账号加载完成后统一扫一遍，把错的值写回去。
///
/// 校正规则（只在能明确判断"这是错的"时才动手，不碰用户自己填过的值）：
/// <list type="number">
/// <item><b>Steam 导入的账户</b>（<c>Id</c> 形如 <c>steam-7656119…</c>）：联机 ID 必须是与
/// 之一一对应的那个 SteamID64。这既是稳定的跨设备身份，也天然不是 Minecraft 的东西。</item>
/// <item><b>离线账户 + 自动生成模式</b>：如果现有值是 36 位标准 UUID，说明是早期版本的
/// Minecraft 离线算法留下的，按 StartRide 自己的算法重算。用户手动指定过的 ID 一律不动。</item>
/// </list>
/// </summary>
public static class StartRideAccountIdRepair
{
    /// <summary>Steam 账户的 Id 前缀（与 SteamLoginClient 写入时保持一致）。</summary>
    private const string SteamIdPrefix = "steam-";

    /// <summary>
    /// 扫一遍账号列表，把不符合规则的联机 ID 写回正确值。返回被改动的账号数量。
    /// </summary>
    public static async Task<int> RunAsync(AccountListViewModel accountList, ILogger? logger = null)
    {
        int repaired = Normalize(accountList, logger);
        if (repaired > 0 && accountList != null)
        {
            await accountList.PersistAccountOrderAsync();
        }

        return repaired;
    }

    /// <summary>
    /// 只改内存、不落盘。给落盘收敛点用（见 <c>AccountListViewModel.PersistAccountOrderAsync</c>）。
    /// </summary>
    public static int Normalize(AccountListViewModel? accountList, ILogger? logger = null)
    {
        if (accountList == null)
        {
            return 0;
        }

        int repaired = 0;
        // 快照一份再改：TryReplaceAccount 会替换集合里的元素。
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

    /// <summary>算出这个账号的联机 ID 应该是多少；无法判断时返回 null（表示不要动）。</summary>
    private static string? ResolveExpectedId(LauncherAccount account)
    {
        // 1. Steam 导入的账户：联机 ID = Account Id 里那个 SteamID64。
        if (account.Id != null && account.Id.StartsWith(SteamIdPrefix, StringComparison.Ordinal))
        {
            string steamId = account.Id.Substring(SteamIdPrefix.Length);
            return StartRidePlayerId.TryNormalize(steamId, out string normalized) ? normalized : null;
        }

        // 2. 离线账户 + 自动生成模式：把早期版本留下的 Minecraft 离线 UUID 换成自有格式。
        if (account.OfflineUuidGenerationMode == OfflineUuidGenerationMode.Standard
            && StartRidePlayerId.IsUuidFormat(account.Uuid))
        {
            return StartRidePlayerId.Create(account.DisplayName);
        }

        return null;
    }
}
