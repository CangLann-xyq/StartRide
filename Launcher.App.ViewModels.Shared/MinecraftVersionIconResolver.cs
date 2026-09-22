using System;
using Launcher.Domain.Models;
using StartRide.Core;
// StartRide.Core 里也有一个 GameInstance（联机房间模型），与实例模型重名 → 用别名钉死
using GameInstance = Launcher.Domain.Models.GameInstance;

namespace Launcher.App.ViewModels.Shared;

/// <summary>
/// 版本/实例列表项前面那个"这是哪个游戏"的图标。
///
/// StartRide 时代这里按 Minecraft 版本类型返回草方块 / 泥土块 / 工作台图标，
/// 实例没显式指定图标时，首页启动卡片和已装车辆列表就会挂着 MC 方块图。
/// StartRide 只有一个游戏（BeamNG.drive），所以除了"实例自己指定过图标"这一种情况，
/// 一律返回 BeamNG 官方 logo。版本类型归一化（正式版/快照版…）仍然保留，
/// 类型标签和筛选用得到它。
/// </summary>
internal static class MinecraftVersionIconResolver
{
	/// <summary>列表项默认图标：BeamNG.drive 官方 logo。</summary>
	public const string DefaultGameIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultReleaseIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultSnapshotIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultBetaIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultAlphaIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultFabricIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultForgeIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultNeoForgeIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultQuiltIconSource = BrandingIcons.BeamNgLogo;

	public static string Resolve(GameInstance instance, string? versionType = null, string? minecraftVersion = null)
	{
		// 用户在实例设置里显式选过图标就用它；否则统一挂 BeamNG logo。
		if (!string.IsNullOrWhiteSpace(instance.IconSource))
		{
			return BrandingIcons.Normalize(instance.IconSource);
		}
		return DefaultGameIconSource;
	}

	public static string Resolve(string? versionType = null, string? minecraftVersion = null)
	{
		return DefaultGameIconSource;
	}

	public static string NormalizeVersionType(string? type)
	{
		switch (type?.Trim().ToLowerInvariant().Replace("-", "_"))
		{
		case "release":
			return "release";
		case "snapshot":
			return "snapshot";
		case "april_fools":
		case "aprilfools":
			return "april_fools";
		case "ancient":
			return "ancient";
		case "oldbeta":
		case "old_beta":
		case "beta":
			return "old_beta";
		case "oldalpha":
		case "old_alpha":
		case "alpha":
			return "old_alpha";
		default:
			return string.Empty;
		}
	}
}
