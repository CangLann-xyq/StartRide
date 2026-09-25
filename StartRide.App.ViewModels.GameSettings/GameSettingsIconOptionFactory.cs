using System.Collections.Generic;
using StartRide.App.Resources;
using StartRide.Core;

namespace StartRide.App.ViewModels.GameSettings;

/// <summary>
/// 实例图标可选项。
///
/// 原版是 12 个 Minecraft 方块（草方块/泥土/工作台…），在 StartRide 里既没有意义，
/// 也会让"改实例图标"的对话框看起来还是 MC 启动器。这里换成两个真实存在的品牌标志。
/// </summary>
internal static class GameSettingsIconOptionFactory
{
	public static IReadOnlyList<GameSettingsIconOption> Create()
	{
		return new global::_003C_003Ez__ReadOnlyArray<GameSettingsIconOption>(new GameSettingsIconOption[2]
		{
			new GameSettingsIconOption(Strings.GameSettings_IconBeamNgLogo, BrandingIcons.BeamNgLogo),
			new GameSettingsIconOption(Strings.GameSettings_IconStartRideLogo, BrandingIcons.StartRideIcon)
		});
	}
}
