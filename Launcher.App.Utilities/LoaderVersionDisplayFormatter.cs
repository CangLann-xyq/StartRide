using System;
using Launcher.Domain.Models;

namespace Launcher.App.Utilities;

internal static class LoaderVersionDisplayFormatter
{
	public static string Format(LoaderKind loader, string? loaderVersion)
	{
		if (string.IsNullOrWhiteSpace(loaderVersion))
		{
			return string.Empty;
		}
		string text = loaderVersion.Trim();
		if (loader != LoaderKind.Fabric)
		{
			return text;
		}
		int num = text.IndexOf("+mixin", StringComparison.OrdinalIgnoreCase);
		if (num >= 0)
		{
			text = text.Substring(0, num);
		}
		int num2 = text.IndexOfAny(new char[4] { ' ', '/', ',', '(' });
		if (num2 > 0 && text.Contains("mixin", StringComparison.OrdinalIgnoreCase))
		{
			text = text.Substring(0, num2);
		}
		return text.Trim();
	}
}
