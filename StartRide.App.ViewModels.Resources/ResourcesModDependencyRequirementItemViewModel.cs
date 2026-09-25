using System;
using StartRide.App.Resources;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Resources;

public sealed class ResourcesModDependencyRequirementItemViewModel
{
	public ResourceProject Project { get; }

	public ResourceProjectDependency Dependency { get; }

	public ResourceProjectVersion? MinimumVersion { get; }

	public ResourceProjectVersion? InstallVersion { get; }

	public ResourceDependencyRequirementState State { get; }

	public bool IsInstalled => State == ResourceDependencyRequirementState.Installed;

	public string? IconSource { get; }

	public string IconKey { get; }

	public string Title => Project.Title;

	public string VersionText => string.Format(Strings.Resources_ModRequiredDependencyVersionFormat, ResolveVersionText(InstallVersion));

	public string MinimumVersionText => string.Format(Strings.Resources_ModRequiredDependencyMinimumVersionFormat, ResolveVersionText(MinimumVersion));

	public string InstallVersionText => string.Format(Strings.Resources_ModRequiredDependencyInstallVersionFormat, ResolveVersionText(InstallVersion));

	public string StateText => State switch
	{
		ResourceDependencyRequirementState.Installed => Strings.Resources_ModRequiredDependencyInstalled, 
		ResourceDependencyRequirementState.UpdateRequired => Strings.Resources_ModRequiredDependencyUpdateRequired, 
		_ => Strings.Resources_ModRequiredDependencyMissing, 
	};

	public ResourcesModDependencyRequirementItemViewModel(ResourceProjectDependency dependency, ResourceProjectVersion? minimumVersion, ResourceProjectVersion? installVersion, ResourceDependencyRequirementState state, string fallbackIconKey = "instance_setting_page/mod")
	{
		Dependency = dependency;
		Project = dependency.Project;
		MinimumVersion = minimumVersion;
		InstallVersion = installVersion;
		State = state;
		IconSource = (string.IsNullOrWhiteSpace(Project.IconUrl) ? null : Project.IconUrl);
		IconKey = (string.IsNullOrWhiteSpace(IconSource) ? fallbackIconKey : string.Empty);
	}

	private static string ResolveVersionText(ResourceProjectVersion? version)
	{
		if (version == null)
		{
			return Strings.Resources_ModRequiredDependencyVersionUnresolved;
		}
		if (!string.IsNullOrWhiteSpace(version.Name) && !string.IsNullOrWhiteSpace(version.VersionNumber) && !string.Equals(version.Name, version.VersionNumber, StringComparison.OrdinalIgnoreCase))
		{
			return version.Name + " " + version.VersionNumber;
		}
		if (!string.IsNullOrWhiteSpace(version.Name))
		{
			return version.Name;
		}
		if (!string.IsNullOrWhiteSpace(version.VersionNumber))
		{
			return version.VersionNumber;
		}
		if (!string.IsNullOrWhiteSpace(version.VersionId))
		{
			return version.VersionId;
		}
		return Strings.Resources_ModRequiredDependencyVersionUnresolved;
	}
}
