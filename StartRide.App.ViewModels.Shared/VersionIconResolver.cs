using System;
using Launcher.Domain.Models;
using StartRide.Core;
using GameInstance = Launcher.Domain.Models.GameInstance;

namespace StartRide.App.ViewModels.Shared;

internal static class VersionIconResolver
{
	public const string DefaultGameIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultReleaseIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultSnapshotIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultBetaIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultAlphaIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultFabricIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultForgeIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultNeoForgeIconSource = BrandingIcons.BeamNgLogo;

	public const string DefaultQuiltIconSource = BrandingIcons.BeamNgLogo;

	public static string Resolve(GameInstance instance, string? versionType = null, string? minecraftVersion = null)
	{
		if (!string.IsNullOrWhiteSpace(instance.IconSource))
		{
			return BrandingIcons.Normalize(instance.IconSource);
		}
		return DefaultGameIconSource;
	}

	public static string Resolve(string? versionType = null, string? minecraftVersion = null)
	{
		return DefaultGameIconSource;
	}

	public static string NormalizeVersionType(string? type)
	{
		switch (type?.Trim().ToLowerInvariant().Replace("-", "_"))
		{
		case "release":
			return "release";
		case "snapshot":
			return "snapshot";
		case "april_fools":
		case "aprilfools":
			return "april_fools";
		case "ancient":
			return "ancient";
		case "oldbeta":
		case "old_beta":
		case "beta":
			return "old_beta";
		case "oldalpha":
		case "old_alpha":
		case "alpha":
			return "old_alpha";
		default:
			return string.Empty;
		}
	}
}
