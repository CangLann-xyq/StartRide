using Launcher.App.Resources;
using Launcher.Domain.Models;

namespace Launcher.App.Utilities;

internal static class GameInstanceDisplayFormatter
{
	public static string GetName(GameInstance instance)
	{
		if (!string.IsNullOrWhiteSpace(instance.Name))
		{
			return instance.Name;
		}
		return GetVersionName(instance);
	}

	public static string GetMinecraftVersion(GameInstance instance)
	{
		if (!string.IsNullOrWhiteSpace(instance.MinecraftVersion))
		{
			return instance.MinecraftVersion;
		}
		return Strings.GameSettings_UnknownMinecraftVersion;
	}

	public static string GetVersionName(GameInstance instance)
	{
		if (!string.IsNullOrWhiteSpace(instance.VersionName))
		{
			return instance.VersionName;
		}
		return instance.MinecraftVersion;
	}

	public static string GetLoaderLabel(LoaderKind loader)
	{
		return LoaderDisplayNameProvider.GetDisplayName(loader);
	}

	public static string GetSubtitle(GameInstance instance)
	{
		string minecraftVersion = GetMinecraftVersion(instance);
		string loaderLabel = GetLoaderLabel(instance.Loader);
		string text = LoaderVersionDisplayFormatter.Format(instance.Loader, instance.LoaderVersion);
		if (instance.Loader == LoaderKind.Vanilla)
		{
			return string.Format(Strings.GameSettings_InstanceSubtitleVanillaFormat, minecraftVersion);
		}
		if (!string.IsNullOrWhiteSpace(text))
		{
			return string.Format(Strings.GameSettings_InstanceSubtitleLoaderFormat, minecraftVersion, loaderLabel, text);
		}
		return string.Format(Strings.GameSettings_InstanceSubtitleLoaderWithoutVersionFormat, minecraftVersion, loaderLabel);
	}
}
