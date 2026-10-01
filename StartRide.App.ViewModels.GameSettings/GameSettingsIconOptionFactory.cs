using System.Collections.Generic;
using StartRide.App.Resources;
using StartRide.Core;

namespace StartRide.App.ViewModels.GameSettings;

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
