using System;
using System.Collections.Generic;

namespace Launcher.App.ViewModels.Shared;

internal static class MinecraftAprilFoolsVersionClassifier
{
	private static readonly HashSet<string> VersionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "15w14a", "1.RV-Pre1", "3D Shareware v1.34", "20w14infinite", "22w13oneblockatatime", "23w13a_or_b", "24w14potato", "25w14craftmine", "26w14a" };

	public static bool IsAprilFoolsVersion(string? versionId)
	{
		if (!string.IsNullOrWhiteSpace(versionId))
		{
			return VersionIds.Contains(versionId.Trim());
		}
		return false;
	}
}
