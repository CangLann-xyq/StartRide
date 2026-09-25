using System;
using Launcher.Domain.Models;

namespace StartRide.App.Services;

internal static class LauncherBackgroundPresentationPolicy
{
	public static LauncherBackgroundPresentation Resolve(string? backgroundEffect, int preferredOpacityPercent, bool enableImageControlBlur)
	{
		string text = LauncherBackgroundEffects.Normalize(backgroundEffect);
		bool flag = LauncherBackgroundEffects.IsAcrylic(text);
		bool flag2 = LauncherBackgroundEffects.IsImage(text);
		return new LauncherBackgroundPresentation(text, flag, flag2, flag2 & enableImageControlBlur, flag ? NormalizeOpacity(preferredOpacityPercent) : 100);
	}

	private static int NormalizeOpacity(int opacityPercent)
	{
		return Math.Clamp(opacityPercent, 0, 100);
	}
}
