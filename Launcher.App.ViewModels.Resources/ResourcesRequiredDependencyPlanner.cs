using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Launcher.App.Resources;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Launcher.App.ViewModels.Resources;

internal sealed class ResourcesRequiredDependencyPlanner
{
	private readonly IResourceDependencyPlanningService? planningService;

	private readonly ResourcesOnlineProjectPageOptions options;

	private readonly ILogger? logger;

	private readonly Action<string> reportStatus;

	public ResourcesRequiredDependencyPlanner(IResourceDependencyPlanningService? planningService, ResourcesOnlineProjectPageOptions options, ILogger? logger, Action<string> reportStatus)
	{
		this.planningService = planningService;
		this.options = options;
		this.logger = logger;
		this.reportStatus = reportStatus;
	}

	public async Task<RequiredDependencyInstallPlan> ResolveInstallPlanAsync(ResourcesModVersionItemViewModel item, GameInstance instance, string? projectId, Func<IReadOnlyList<ResourcesModDependencyRequirementItemViewModel>, Task<RequiredDependenciesDialogChoice>> requestDialogAsync, CancellationToken cancellationToken)
	{
		if (options.Kind != ResourceProjectKind.Mod || item.Version.RequiredDependencies.Count == 0 || planningService == null)
		{
			return RequiredDependencyInstallPlan.Continue;
		}
		ResourceDependencyInstallPlan plan = await planningService.CreatePlanAsync(item.Version, instance, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		ResourcesModDependencyRequirementItemViewModel[] array = plan.Requirements.Select((ResourceDependencyInstallCandidate candidate) => new ResourcesModDependencyRequirementItemViewModel(candidate.Dependency, candidate.MinimumVersion, candidate.InstallVersion, candidate.State, options.FallbackIconKey)).ToArray();
		if (plan.MissingDependencies.Count == 0)
		{
			logger?.LogInformation("Resource project required dependencies are already installed. ProjectId={ProjectId} VersionId={VersionId} RequiredCount={RequiredCount} InstanceId={InstanceId}", projectId, item.Version.VersionId, array.Length, instance.Id);
			return RequiredDependencyInstallPlan.Continue;
		}
		return new RequiredDependencyInstallPlan(await requestDialogAsync(array).ConfigureAwait(continueOnCapturedContext: false), plan.MissingDependencies);
	}

	public Task InstallRequiredDependenciesAsync(IReadOnlyList<ResourceDependencyInstallCandidate> missingDependencies, GameInstance instance, string? projectId, IProgress<LauncherProgress>? taskProgress, Action<LauncherProgress>? reportProgress, CancellationToken cancellationToken)
	{
		if (planningService == null || missingDependencies.Count == 0)
		{
			return Task.CompletedTask;
		}
		IProgress<ResourceDependencyInstallProgress> progress = new Progress<ResourceDependencyInstallProgress>(delegate(ResourceDependencyInstallProgress value)
		{
			string text = string.Format(Strings.Status_ModRequiredDependencyInstallingFormat, value.DependencyTitle);
			reportStatus(text);
			reportProgress?.Invoke(new LauncherProgress("Mod.DownloadingFile", text));
		});
		if (taskProgress != null)
		{
			progress = DownloadSpeedTaskProgress.Carry(taskProgress, progress);
		}
		logger?.LogInformation("Installing required resource dependencies. ProjectId={ProjectId} MissingCount={MissingCount} InstanceId={InstanceId}", projectId, missingDependencies.Count, instance.Id);
		return planningService.InstallRequiredDependenciesAsync(missingDependencies, instance, progress, cancellationToken);
	}
}
