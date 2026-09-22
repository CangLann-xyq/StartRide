using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.ViewModels.Download;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Launcher.App.ViewModels.Resources;

public class ResourcesModPageViewModel : ResourcesSectionViewModelBase, IDisposable
{
	private readonly ResourcesOnlineProjectPageOptions options;

	private readonly DownloadTasksPageViewModel? downloadTasksPage;

	private readonly ILogger? logger;

	[ObservableProperty]
	[NotifyPropertyChangedFor("IsProjectListStep")]
	[NotifyPropertyChangedFor("IsProjectDetailsStep")]
	[NotifyPropertyChangedFor("IsProjectVersionsStep")]
	[NotifyPropertyChangedFor("IsProjectContentStep")]
	[NotifyPropertyChangedFor("PageTitle")]
	[NotifyPropertyChangedFor("PageTitleIconSource")]
	private ResourcesModPageStep currentStep;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? backToProjectListCommand;

	public ResourcesProjectListViewModel ProjectList { get; }

	public ResourcesProjectDetailsViewModel Details { get; }

	public ResourcesProjectVersionsViewModel Versions { get; }

	public ResourcesProjectInstallViewModel Install { get; }

	public bool IsProjectListStep => CurrentStep == ResourcesModPageStep.ProjectList;

	public bool IsProjectDetailsStep => CurrentStep == ResourcesModPageStep.ProjectDetails;

	public bool IsProjectVersionsStep => CurrentStep == ResourcesModPageStep.ProjectVersions;

	public bool IsProjectContentStep => CurrentStep != ResourcesModPageStep.ProjectList;

	public string PageTitle
	{
		get
		{
			object obj;
			if (IsProjectVersionsStep)
			{
				ResourcesModInstallTargetItemViewModel? selectedTarget = Versions.SelectedTarget;
				if (selectedTarget != null && !selectedTarget.IsLocalDownload)
				{
					obj = Versions.SelectedTarget.Title;
					goto IL_0065;
				}
			}
			if (!IsProjectContentStep)
			{
				return base.Title;
			}
			obj = Details.CurrentProject?.Title;
			if (obj == null)
			{
				return base.Title;
			}
			goto IL_0065;
			IL_0065:
			return (string)obj;
		}
	}

	public string? PageTitleIconSource
	{
		get
		{
			if (IsProjectVersionsStep)
			{
				ResourcesModInstallTargetItemViewModel? selectedTarget = Versions.SelectedTarget;
				if (selectedTarget != null && !selectedTarget.IsLocalDownload)
				{
					return Versions.SelectedTarget.IconSource;
				}
			}
			if (!IsProjectContentStep)
			{
				return null;
			}
			return Details.CurrentProject?.IconSource;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesModPageStep CurrentStep
	{
		get
		{
			return currentStep;
		}
		set
		{
			if (!EqualityComparer<ResourcesModPageStep>.Default.Equals(currentStep, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentStep);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsProjectListStep);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsProjectDetailsStep);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsProjectVersionsStep);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsProjectContentStep);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PageTitle);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PageTitleIconSource);
				currentStep = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentStep);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsProjectListStep);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsProjectDetailsStep);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsProjectVersionsStep);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsProjectContentStep);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PageTitle);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PageTitleIconSource);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand BackToProjectListCommand => backToProjectListCommand ?? (backToProjectListCommand = new RelayCommand(BackToProjectList));

	public event EventHandler<GameInstance>? ModpackImported;

	public event EventHandler<ResourcesModpackManualDownloadsRequestedEventArgs>? ModpackManualDownloadsRequested;

	public ResourcesModPageViewModel(ResourcesPageViewModel parent, IResourceCatalogService? resourceCatalogService = null, ILogger? logger = null, IUiDispatcher? uiDispatcher = null, IGameVersionService? gameVersionService = null, Func<IReadOnlyList<GameInstance>>? getInstanceCatalogSnapshot = null, IStatusService? statusService = null, IFilePickerService? filePickerService = null, IFloatingMessageService? floatingMessageService = null, DownloadTasksPageViewModel? downloadTasksPage = null, IResourceProjectInstallationService? resourceProjectInstallationService = null, IResourceDependencyPlanningService? resourceDependencyPlanningService = null)
		: this(parent, CreateModOptions(), resourceCatalogService, logger, uiDispatcher, gameVersionService, getInstanceCatalogSnapshot, statusService, filePickerService, floatingMessageService, downloadTasksPage, resourceProjectInstallationService, resourceDependencyPlanningService)
	{
	}

	protected ResourcesModPageViewModel(ResourcesPageViewModel parent, ResourcesOnlineProjectPageOptions options, IResourceCatalogService? resourceCatalogService = null, ILogger? logger = null, IUiDispatcher? uiDispatcher = null, IGameVersionService? gameVersionService = null, Func<IReadOnlyList<GameInstance>>? getInstanceCatalogSnapshot = null, IStatusService? statusService = null, IFilePickerService? filePickerService = null, IFloatingMessageService? floatingMessageService = null, DownloadTasksPageViewModel? downloadTasksPage = null, IResourceProjectInstallationService? resourceProjectInstallationService = null, IResourceDependencyPlanningService? resourceDependencyPlanningService = null)
		: base(parent, options.Title)
	{
		ResourcesModPageViewModel resourcesModPageViewModel = this;
		this.options = options;
		this.downloadTasksPage = downloadTasksPage;
		this.logger = logger;
		IUiDispatcher dispatcher = uiDispatcher ?? ImmediateUiDispatcher.Instance;
		Action<string> reportStatus = delegate(string message)
		{
			if (!string.IsNullOrWhiteSpace(message))
			{
				if (dispatcher.HasAccess)
				{
					statusService?.Report(message);
				}
				else
				{
					dispatcher.Invoke(delegate
					{
						statusService?.Report(message);
					});
				}
			}
		};
		ProjectList = new ResourcesProjectListViewModel(options, resourceCatalogService, gameVersionService, dispatcher, logger);
		Details = new ResourcesProjectDetailsViewModel(options, resourceCatalogService, dispatcher, logger);
		Versions = new ResourcesProjectVersionsViewModel(options, resourceCatalogService, getInstanceCatalogSnapshot, dispatcher, logger);
		Install = new ResourcesProjectInstallViewModel(options, resourceProjectInstallationService, new ResourcesRequiredDependencyPlanner(resourceDependencyPlanningService, options, logger, reportStatus), filePickerService, floatingMessageService, downloadTasksPage, dispatcher, logger, reportStatus);
		ProjectList.ProjectSelected += Details.SelectRoot;
		ProjectList.NavigationResetRequested += ResetToProjectList;
		Details.ProjectChanged += OpenProjectDetails;
		Versions.TargetSelected += delegate
		{
			resourcesModPageViewModel.CurrentStep = ResourcesModPageStep.ProjectVersions;
		};
		Versions.InstallRequested += delegate(ResourcesModVersionItemViewModel item)
		{
			resourcesModPageViewModel.ObserveInstall(item);
		};
		Install.ModpackImported += delegate(object? _, GameInstance instance)
		{
			resourcesModPageViewModel.ModpackImported?.Invoke(resourcesModPageViewModel, instance);
		};
		Install.ModpackManualDownloadsRequested += delegate(object? _, ResourcesModpackManualDownloadsRequestedEventArgs args)
		{
			resourcesModPageViewModel.ModpackManualDownloadsRequested?.Invoke(resourcesModPageViewModel, args);
		};
	}

	[RelayCommand]
	public void BackToProjectList()
	{
		ResourcesModProjectItemViewModel project;
		if (CurrentStep == ResourcesModPageStep.ProjectVersions)
		{
			CurrentStep = ResourcesModPageStep.ProjectDetails;
		}
		else if (CurrentStep != ResourcesModPageStep.ProjectDetails || !Details.TryGoBack(out project))
		{
			ResetToProjectList();
		}
	}

	public void ResetToProjectList()
	{
		Details.Reset();
		Versions.Reset();
		CurrentStep = ResourcesModPageStep.ProjectList;
		RaisePageTitleChanged();
	}

	public void BeginEnsureProjectsLoaded()
	{
		ProjectList.BeginEnsureLoaded();
	}

	public Task ApplyInstanceFiltersAsync(GameInstance instance)
	{
		return ProjectList.ApplyInstanceFiltersAsync(instance);
	}

	public void BeginLoadMoreProjects()
	{
		ProjectList.BeginLoadMore();
	}

	public void BeginLoadMoreAvailableVersions()
	{
		Versions.BeginLoadMore();
	}

	public void ShowProjectDetails(ResourceProject project)
	{
		ArgumentNullException.ThrowIfNull(project, "project");
		Details.SelectRoot(new ResourcesModProjectItemViewModel(project, null, options.FallbackIconKey, options.TypeOptions));
	}

	public void Dispose()
	{
		ProjectList.ProjectSelected -= Details.SelectRoot;
		ProjectList.NavigationResetRequested -= ResetToProjectList;
		Details.ProjectChanged -= OpenProjectDetails;
		ProjectList.Dispose();
		Details.Dispose();
		Versions.Dispose();
	}

	private void OpenProjectDetails(ResourcesModProjectItemViewModel project)
	{
		CurrentStep = ResourcesModPageStep.ProjectDetails;
		Versions.SetProject(project);
		RaisePageTitleChanged();
		logger?.LogDebug("Resource project selected. Kind={Kind} Source={Source} ProjectId={ProjectId}", options.Kind, project.Project.Source, project.Project.ProjectId);
	}

	private void ObserveInstall(ResourcesModVersionItemViewModel item)
	{
		Task task = Install.InstallAsync(item, Versions.SelectedTarget, Details.CurrentProject);
		downloadTasksPage?.TrackBackgroundTask(task);
		ObserveInstallAsync(task);
	}

	private async Task ObserveInstallAsync(Task operation)
	{
		try
		{
			await operation.ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception exception)
		{
			logger?.LogError(exception, "Unhandled resource installation command failure. Kind={Kind}", options.Kind);
		}
	}

	private void RaisePageTitleChanged()
	{
		OnPropertyChanged("PageTitle");
		OnPropertyChanged("PageTitleIconSource");
	}

	protected static ResourcesOnlineProjectPageOptions CreateModOptions()
	{
		return new ResourcesOnlineProjectPageOptions(ResourceProjectKind.Mod, Strings.Resources_SectionMods, "instance_setting_page/mod", ShowsLoaderFilters: true, Strings.Resources_ModFilterAllVersions, Strings.Resources_ModFilterAllLoaders, Strings.Resources_ModProjectsLoading, Strings.Resources_ModProjectsEmpty, Strings.Resources_ModProjectsLoadError, Strings.Resources_ModProjectsLoadingMore, Strings.Resources_ModProjectsNoMore, Strings.Resources_ModProjectsLoadMoreError, Strings.Resources_ModCurseForgeMissingApiKey, Strings.Resources_ModDetailsInfoSection, Strings.Resources_ModInstallTargetSection, Strings.Resources_ModInstallTargetLocal, Strings.Resources_ModInstallTargetsLoading, Strings.Resources_ModInstallTargetsLoadError, Strings.Resources_ModVersionsLoading, Strings.Resources_ModVersionsEmpty, Strings.Resources_ModVersionsEmptyLocal, Strings.Resources_ModVersionsFilterEmpty, Strings.Resources_ModVersionsLoadError, Strings.Resources_ModVersionsLoadingMore, Strings.Resources_ModVersionsNoMore, Strings.Resources_ModVersionsLoadMoreError, Strings.Resources_ModVersionsAllTitle, Strings.FilePicker_ModDownloadDirectoryTitle, Strings.Status_ModDownloading, Strings.Status_ModDownloadingFormat, Strings.Status_ModDownloadedFormat, Strings.Status_ModDownloadFailed, Strings.Status_ModInstalledFormat, Strings.Status_ModInstallFailed, Strings.Resources_ModDownloadFileExistsMessageFormat, new global::_003C_003Ez__ReadOnlyArray<ResourcesOnlineProjectTypeOption>(new ResourcesOnlineProjectTypeOption[11]
		{
			new ResourcesOnlineProjectTypeOption("optimization", Strings.Resources_ModFilterTypeOptimization, ResourceProjectCategory.Optimization),
			new ResourcesOnlineProjectTypeOption("utility", Strings.Resources_ModFilterTypeUtility, ResourceProjectCategory.Utility),
			new ResourcesOnlineProjectTypeOption("adventure", Strings.Resources_ModFilterTypeAdventure, ResourceProjectCategory.Adventure),
			new ResourcesOnlineProjectTypeOption("decoration", Strings.Resources_ModFilterTypeDecoration, ResourceProjectCategory.Decoration),
			new ResourcesOnlineProjectTypeOption("equipment", Strings.Resources_ModFilterTypeEquipment, ResourceProjectCategory.Equipment),
			new ResourcesOnlineProjectTypeOption("technology", Strings.Resources_ModFilterTypeTechnology, ResourceProjectCategory.Technology),
			new ResourcesOnlineProjectTypeOption("magic", Strings.Resources_ModFilterTypeMagic, ResourceProjectCategory.Magic),
			new ResourcesOnlineProjectTypeOption("mobs", Strings.Resources_ModFilterTypeMobs, ResourceProjectCategory.Mobs),
			new ResourcesOnlineProjectTypeOption("worldgen", Strings.Resources_ModFilterTypeWorldGeneration, ResourceProjectCategory.WorldGeneration),
			new ResourcesOnlineProjectTypeOption("storage", Strings.Resources_ModFilterTypeStorage, ResourceProjectCategory.Storage),
			new ResourcesOnlineProjectTypeOption("library", Strings.Resources_ModFilterTypeLibrary, ResourceProjectCategory.Library)
		}));
	}
}
