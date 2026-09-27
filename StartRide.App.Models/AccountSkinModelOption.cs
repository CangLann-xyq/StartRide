using Launcher.Domain.Models;

namespace StartRide.App.Models;

public sealed class AccountSkinModelOption
{
	public required MinecraftSkinModel Model { get; init; }

	public required string Title { get; init; }

	public required string Description { get; init; }
}
