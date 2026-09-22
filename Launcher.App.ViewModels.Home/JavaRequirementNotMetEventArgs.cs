using System;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Home;

public sealed class JavaRequirementNotMetEventArgs : EventArgs
{
	public int? RequiredMajorVersion { get; }

	public JavaRuntimeSelectionFailureReason Reason { get; }

	public GameInstance Instance { get; }

	public int? CurrentMajorVersion { get; }

	public string? CurrentVersion { get; }

	public int? RecommendedMajorVersion { get; }

	public JavaRequirementNotMetEventArgs(int? requiredMajorVersion, JavaRuntimeSelectionFailureReason reason, GameInstance instance, int? currentMajorVersion = null, string? currentVersion = null, int? recommendedMajorVersion = null)
	{
		RequiredMajorVersion = requiredMajorVersion;
		Reason = reason;
		Instance = instance;
		CurrentMajorVersion = currentMajorVersion;
		CurrentVersion = currentVersion;
		RecommendedMajorVersion = recommendedMajorVersion ?? requiredMajorVersion;
	}
}
