using System;
using System.Collections.Generic;
using StartRide.App.Resources;

namespace StartRide.App.ViewModels.Multiplayer;

public sealed record MultiplayerLobbyPlayerItem(string DisplayName, string Subtitle, string LatencyText, string Role, bool IsHost, bool IsLocal, bool IsFirst, bool IsLast)
{
	public IReadOnlyList<string> RoleTags => new global::_003C_003Ez__ReadOnlySingleElementList<string>(Role);

	public IReadOnlyList<string> LocalTags
	{
		get
		{
			if (!IsLocal)
			{
				return Array.Empty<string>();
			}
			return new global::_003C_003Ez__ReadOnlySingleElementList<string>(Strings.Multiplayer_LobbyPlayerRoleSelf);
		}
	}
}
