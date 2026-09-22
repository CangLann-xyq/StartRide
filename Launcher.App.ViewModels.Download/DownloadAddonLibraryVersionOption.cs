using Launcher.App.Resources;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Download;

public sealed class DownloadAddonLibraryVersionOption
{
	public string Title { get; }

	public string? VersionId { get; }

	public string VersionNumber { get; }

	public bool IsInstallable { get; }

	public bool IsLatest { get; }

	public bool IsStable { get; }

	public string TagText
	{
		get
		{
			if (IsInstallable)
			{
				if (!IsLatest)
				{
					if (!IsStable)
					{
						return Strings.Download_LoaderVersionPreviewTag;
					}
					return Strings.Download_LoaderVersionStableTag;
				}
				return Strings.Download_AddonLibraryLatestTag;
			}
			return string.Empty;
		}
	}

	public static DownloadAddonLibraryVersionOption None { get; } = new DownloadAddonLibraryVersionOption(Strings.Download_AddonLibraryNone, null, string.Empty, isInstallable: false, isLatest: false, isStable: true);

	public DownloadAddonLibraryVersionOption(string title, string? versionId, string versionNumber, bool isInstallable, bool isLatest, bool isStable)
	{
		Title = title;
		VersionId = versionId;
		VersionNumber = versionNumber;
		IsInstallable = isInstallable;
		IsLatest = isLatest;
		IsStable = isStable;
	}

	public static DownloadAddonLibraryVersionOption FromVersion(ModrinthVersionInfo version, bool isLatest)
	{
		return new DownloadAddonLibraryVersionOption(string.IsNullOrWhiteSpace(version.VersionNumber) ? version.Name : version.VersionNumber, version.VersionId, version.VersionNumber, isInstallable: true, isLatest, version.IsStable);
	}

	public override string ToString()
	{
		return Title;
	}
}
