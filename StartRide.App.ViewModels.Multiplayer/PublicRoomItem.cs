using StartRide.App.Resources;
using StartRide.Core;

namespace StartRide.App.ViewModels.Multiplayer;

public sealed record PublicRoomItem(Room Room)
{
	public string RoomCode => Room.Id;

	public string Name => string.IsNullOrWhiteSpace(Room.Name) ? Room.Id : Room.Name;

	public string Host => string.IsNullOrWhiteSpace(Room.Host) ? Strings.Multiplayer_LobbyOwnerPlaceholder : Room.Host;

	public string Map => string.IsNullOrWhiteSpace(Room.Map) ? "—" : Room.Map;

	public string Mode => string.IsNullOrWhiteSpace(Room.Mode) ? "freeroam" : Room.Mode;

	public string PlayerCount => $"{Room.Players}/{Room.Capacity} 人";

	public int Players => Room.Players;

	public int Capacity => Room.Capacity;

	public bool HasPassword => !string.IsNullOrWhiteSpace(Room.Password);
}
