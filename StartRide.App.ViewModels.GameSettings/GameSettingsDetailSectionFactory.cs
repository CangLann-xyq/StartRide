using System.Collections.Generic;
using StartRide.App.Resources;

namespace StartRide.App.ViewModels.GameSettings;

internal static class GameSettingsDetailSectionFactory
{
	public static IReadOnlyList<GameSettingsDetailSectionItem> Create()
	{
		// StartRide：精简为 BeamNG 真实设置，删掉 Minecraft 专属的
		// java（Java 运行时）/ saves（存档）/ resource_packs（资源包）/ shaders（着色器）/ export（导出整合包）。
		return new global::_003C_003Ez__ReadOnlyArray<GameSettingsDetailSectionItem>(new GameSettingsDetailSectionItem[4]
		{
			new GameSettingsDetailSectionItem("general", Strings.GameSettings_DetailGeneral, "instance_setting_page/general_setting"),
			new GameSettingsDetailSectionItem("launch", Strings.GameSettings_DetailLaunch, "instance_setting_page/launch"),
			new GameSettingsDetailSectionItem("mod_management", Strings.GameSettings_DetailModManagement, "instance_setting_page/mod"),
			new GameSettingsDetailSectionItem("backup", Strings.GameSettings_DetailBackup, "instance_setting_page/backup")
		});
	}
}
