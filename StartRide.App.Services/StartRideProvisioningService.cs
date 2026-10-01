using System;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace StartRide.App.Services;

public sealed class StartRideProvisioningService : ITerracottaProvisioningService
{
	private static readonly TerracottaModule Ready =
		new TerracottaModule("startride-relay", "x64", string.Empty, string.Empty);

	public TerracottaModule? TryGetAvailable() => Ready;

	public Task<TerracottaModule> EnsureAvailableAsync(
		IProgress<LauncherProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(Ready);
	}
}
