using Launcher.Domain.Models;

namespace StartRide.App.Models;

public sealed class OfflineUuidModeOption
{
	public required OfflineUuidGenerationMode Mode { get; init; }

	public required string Title { get; init; }

	public required string Description { get; init; }

	public override string ToString()
	{
		return Title;
	}
}
