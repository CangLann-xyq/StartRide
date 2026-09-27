using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Download;

public sealed record DownloadInstallRequest(string MinecraftVersion, string MinecraftVersionType, string InstanceName, LoaderKind Loader, string? LoaderVersion, string? FabricApiVersionId, string? QuiltStandardLibraryVersionId, string LoaderDisplayName, DownloadSourcePreference DownloadSourcePreference, int DownloadSpeedLimitMbPerSecond);
