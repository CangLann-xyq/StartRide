using System.Collections.Generic;
using StartRide.App.Resources;

namespace StartRide.App.ViewModels.GameSettings;

internal static class GameSettingsDetailSectionFactory
{
	public static IReadOnlyList<GameSettingsDetailSectionItem> Create()
	{

		return new global::_003C_003Ez__ReadOnlyArray<GameSettingsDetailSectionItem>(new GameSettingsDetailSectionItem[4]
		{
			new GameSettingsDetailSectionItem("general", Strings.GameSettings_DetailGeneral, "instance_setting_page/general_setting"),
			new GameSettingsDetailSectionItem("launch", Strings.GameSettings_DetailLaunch, "instance_setting_page/launch"),
			new GameSettingsDetailSectionItem("mod_management", Strings.GameSettings_DetailModManagement, "instance_setting_page/mod"),
			new GameSettingsDetailSectionItem("backup", Strings.GameSettings_DetailBackup, "instance_setting_page/backup")
		});
	}
}
