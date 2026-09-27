using System;
using Launcher.Application.Accounts;
using Launcher.Domain.Models;
using StartRide.Core;

namespace StartRide.Services;

/// <summary>
/// StartRide 自有的联机 ID 生成与校验，用来顶掉框架原实现。
///
/// 原实现用的是 Minecraft 的"离线 UUID"：<c>MD5("OfflinePlayer:" + 昵称)</c>。这带来两个实际问题：
/// <list type="number">
/// <item>StartRide 的玩家身份会和 Minecraft 离线服务器共用同一个身份空间——同一个昵称在两边
/// 算出完全相同的 ID，界面上还写着"UUID"；</item>
/// <item>BeamNG.drive 的联机跟 Minecraft 毫无关系，这个算法纯属跟着反编译来源一起带过来的。</item>
/// </list>
///
/// 算法本体在 <see cref="StartRidePlayerId"/>，这里只负责把它接到框架的
/// <see cref="IOfflineAccountUuidService"/> 上——接口名和参数名里的 "Uuid" 是框架历史遗留，
/// 不再改动调用方，改由 DI 覆盖注册（见 <c>App.cs</c>）整体接管，
/// 这样连线两端（新建账号、改名重算、切换生成方式、手动校验）一次全换掉，
/// 不会出现"改了三处、漏了一处还在生成 MC UUID"的情况。
/// </summary>
public sealed class StartRideOfflineIdService : IOfflineAccountUuidService
{
    /// <summary>按生成方式产出联机 ID。</summary>
    public string CreateUuid(string accountName, OfflineUuidGenerationMode mode)
    {
        return CreateUuid(accountName, mode, null);
    }

    /// <summary>
    /// 按生成方式产出联机 ID。
    /// </summary>
    /// <param name="accountName">玩家昵称。</param>
    /// <param name="mode">生成方式（<c>Standard</c> 跟着昵称算，<c>Manual</c> 用玩家自己填的）。</param>
    /// <param name="existingUuid">
    /// 玩家已有的 ID。自定义方式下必须原样保留——"自定义"的意思就是改名不换身份。
    /// 但它可能是早期版本留下的 UUID，所以先规范化（大小写、分隔符）再存。
    /// </param>
    public string CreateUuid(string accountName, OfflineUuidGenerationMode mode, string? existingUuid)
    {
        if (mode == OfflineUuidGenerationMode.Manual && StartRidePlayerId.TryNormalize(existingUuid, out string kept))
        {
            return kept;
        }

        if (mode == OfflineUuidGenerationMode.Random)
        {
            // 随机方式已经不在界面上提供了（旧账号可能还是这个值），
            // 仍然给一个同格式的结果，避免出现空 ID 或另一种形态的 ID。
            return StartRidePlayerId.Create(Guid.NewGuid().ToString("N"));
        }

        return StartRidePlayerId.Create(accountName);
    }

    /// <summary>
    /// 校验并规范化玩家手动填写的联机 ID。
    /// 同时接受 StartRide 自有格式与标准 UUID——后者是早期版本生成的样子，
    /// 用户把它粘进"自定义"输入框时必须还能用。
    /// </summary>
    public bool TryNormalizeUuid(string text, out string uuid)
    {
        return StartRidePlayerId.TryNormalize(text, out uuid);
    }
}
