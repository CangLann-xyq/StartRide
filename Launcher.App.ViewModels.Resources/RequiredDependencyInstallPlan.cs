using System;
using System.Collections.Generic;
using Launcher.Application.Services;

namespace Launcher.App.ViewModels.Resources;

internal sealed record RequiredDependencyInstallPlan(RequiredDependenciesDialogChoice Choice, IReadOnlyList<ResourceDependencyInstallCandidate> MissingDependencies)
{
	public static RequiredDependencyInstallPlan Continue { get; } = new RequiredDependencyInstallPlan(RequiredDependenciesDialogChoice.ContinueWithoutDependencies, Array.Empty<ResourceDependencyInstallCandidate>());
}
