using Launcher.App.Resources;
using Launcher.App.Utilities;
using Launcher.App.ViewModels.Shared;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Resources;

public sealed class ResourcesModInstallTargetItemViewModel
{
	public GameInstance? Instance { get; }

	public string Title { get; }

	public string Subtitle { get; }

	public string? IconSource { get; }

	public string? IconKey { get; }

	public bool IsLocalDownload { get; }

	public bool IsNewInstanceInstall { get; }

	public bool IsServerInstall { get; }

	public bool IsFirstVisible { get; private set; }

	public bool IsLastVisible { get; private set; }

	public bool IsPreviousItemHighlighted => false;

	private ResourcesModInstallTargetItemViewModel(GameInstance? instance, string title, string subtitle, string? iconSource, string? iconKey, bool isLocalDownload, bool isNewInstanceInstall, bool isServerInstall)
	{
		Instance = instance;
		Title = title;
		Subtitle = subtitle;
		IconSource = iconSource;
		IconKey = iconKey;
		IsLocalDownload = isLocalDownload;
		IsNewInstanceInstall = isNewInstanceInstall;
		IsServerInstall = isServerInstall;
	}

	public void SetVisiblePosition(bool isFirstVisible, bool isLastVisible)
	{
		IsFirstVisible = isFirstVisible;
		IsLastVisible = isLastVisible;
	}

	public static ResourcesModInstallTargetItemViewModel FromInstance(GameInstance instance)
	{
		return new ResourcesModInstallTargetItemViewModel(instance, GameInstanceDisplayFormatter.GetName(instance), GameInstanceDisplayFormatter.GetSubtitle(instance), MinecraftVersionIconResolver.Resolve(instance, instance.VersionType, instance.MinecraftVersion), null, isLocalDownload: false, isNewInstanceInstall: false, isServerInstall: false);
	}

	public static ResourcesModInstallTargetItemViewModel CreateNewInstanceInstall(string title)
	{
		return new ResourcesModInstallTargetItemViewModel(null, title, string.Empty, null, "main_menu_instance_download", isLocalDownload: false, isNewInstanceInstall: true, isServerInstall: false);
	}

	public static ResourcesModInstallTargetItemViewModel CreateServerInstall(string title)
	{
		return new ResourcesModInstallTargetItemViewModel(null, title, string.Empty, null, "server", isLocalDownload: false, isNewInstanceInstall: false, isServerInstall: true);
	}

	public static ResourcesModInstallTargetItemViewModel CreateLocalDownload(string? title = null)
	{
		return new ResourcesModInstallTargetItemViewModel(null, title ?? Strings.Resources_ModInstallTargetLocal, string.Empty, null, "save_as", isLocalDownload: true, isNewInstanceInstall: false, isServerInstall: false);
	}
}
