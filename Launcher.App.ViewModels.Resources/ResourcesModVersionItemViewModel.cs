using Launcher.App.Resources;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Resources;

public sealed class ResourcesModVersionItemViewModel
{
	public ResourceProjectVersion Version { get; }

	public string? IconSource { get; }

	public string IconKey { get; }

	public string Title
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Version.Name))
			{
				return Version.Name;
			}
			return Version.VersionNumber;
		}
	}

	public string Subtitle
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Version.FileName))
			{
				return FormatSubtitle(Version.FileName);
			}
			return FormatSubtitle(Version.VersionNumber);
		}
	}

	public string TrailingText => Version.PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd") ?? string.Empty;

	private string VersionTypeText => Version.VersionType.Trim().ToLowerInvariant() switch
	{
		"release" => Strings.Download_ReleaseCategory, 
		"beta" => Strings.Download_BetaCategory, 
		"alpha" => Strings.Download_AlphaCategory, 
		_ => string.Empty, 
	};

	public ResourcesModVersionItemViewModel(ResourceProjectVersion version, ResourcesModProjectItemViewModel? project, string fallbackIconKey = "instance_setting_page/mod")
	{
		Version = version;
		IconSource = project?.IconSource;
		IconKey = (string.IsNullOrWhiteSpace(IconSource) ? fallbackIconKey : string.Empty);
	}

	private string FormatSubtitle(string value)
	{
		if (!string.IsNullOrWhiteSpace(VersionTypeText))
		{
			return value + "  " + VersionTypeText;
		}
		return value;
	}
}
