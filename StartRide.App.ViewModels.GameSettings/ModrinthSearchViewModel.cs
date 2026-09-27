using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Shared;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class ModrinthSearchViewModel : ObservableObject
{
	private readonly IModrinthService modrinthService;

	private readonly IStatusService statusService;

	[ObservableProperty]
	private string modSearchQuery = string.Empty;

	[ObservableProperty]
	private ModrinthProject? selectedModrinthProject;

	public ObservableCollection<ModrinthProject> ModrinthProjects { get; } = new ObservableCollection<ModrinthProject>();

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ModSearchQuery
	{
		get
		{
			return modSearchQuery;
		}
		[MemberNotNull("modSearchQuery")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(modSearchQuery, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ModSearchQuery);
				modSearchQuery = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ModSearchQuery);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ModrinthProject? SelectedModrinthProject
	{
		get
		{
			return selectedModrinthProject;
		}
		set
		{
			if (!EqualityComparer<ModrinthProject>.Default.Equals(selectedModrinthProject, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedModrinthProject);
				selectedModrinthProject = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedModrinthProject);
			}
		}
	}

	public ModrinthSearchViewModel(IModrinthService modrinthService, IStatusService statusService)
	{
		this.modrinthService = modrinthService;
		this.statusService = statusService;
	}

	public async Task SearchModsAsync(GameInstance? selectedInstance)
	{
		if (selectedInstance == null)
		{
			ReportStatus(Strings.Status_SelectInstanceFirst);
			return;
		}
		ReportStatus(Strings.Status_SearchingModrinth);
		IReadOnlyList<ModrinthProject> items = await modrinthService.SearchModsAsync(ModSearchQuery, selectedInstance.MinecraftVersion, selectedInstance.Loader);
		ModrinthProjects.ReplaceWith(items);
		ReportStatus(string.Format(Strings.Status_ModrinthResultsFoundFormat, ModrinthProjects.Count));
	}

	public async Task<bool> InstallSelectedModAsync(GameInstance? selectedInstance, IProgress<LauncherProgress>? progress)
	{
		if (selectedInstance == null || SelectedModrinthProject == null)
		{
			return false;
		}
		try
		{
			await modrinthService.InstallLatestCompatibleAsync(SelectedModrinthProject, selectedInstance, progress);
		}
		catch (NoCompatibleModFileException)
		{
			ReportStatus(Strings.Status_ModCompatibleFileNotFound);
			return false;
		}
		ReportStatus(string.Format(Strings.Status_ModInstalledFormat, SelectedModrinthProject.Title));
		return true;
	}

	private void ReportStatus(string message)
	{
		statusService.Report(message);
	}
}
