using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.ViewModels.Download;
using Launcher.App.ViewModels.GameSettings;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Launcher.App.ViewModels.Resources;

public sealed class ResourcesPageViewModel : ObservableObject
{
	private readonly ILogger<ResourcesPageViewModel>? logger;

	private readonly IStatusService? statusService;

	private readonly IExternalLinkService? externalLinkService;

	private readonly IResourceCatalogService? resourceCatalogService;

	[ObservableProperty]
	[NotifyPropertyChangedFor("PageTitle")]
	[NotifyPropertyChangedFor("IsModsSection")]
	[NotifyPropertyChangedFor("IsModSearchVisible")]
	[NotifyPropertyChangedFor("IsModProjectDetailsStep")]
	[NotifyPropertyChangedFor("CurrentOnlineProjectPage")]
	[NotifyPropertyChangedFor("PageTitleIconSource")]
	private ResourcesSectionItem? selectedSection;

	[ObservableProperty]
	private ResourcesSectionViewModelBase? currentSectionViewModel;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ResourcesModProjectItemViewModel?>? openProjectPageCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ResourceProjectRelatedWebsite?>? openRelatedWebsiteCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ResourcesSectionItem?>? selectSectionCommand;

	public ObservableCollection<ResourcesSectionItem> Sections { get; }

	public ResourcesModPageViewModel ModPage { get; }

	public ResourcesResourcePacksPageViewModel ResourcePacksPage { get; }

	public ResourcesShaderPacksPageViewModel ShaderPacksPage { get; }

	public ResourcesWorldsPageViewModel WorldsPage { get; }

	public ResourcesModpacksPageViewModel ModpacksPage { get; }

	public string PageTitle
	{
		get
		{
			ResourcesModPageViewModel currentOnlineProjectPage = CurrentOnlineProjectPage;
			object obj;
			if (currentOnlineProjectPage == null)
			{
				obj = SelectedSection?.Title;
				if (obj == null)
				{
					return Strings.Page_Resources;
				}
			}
			else
			{
				obj = currentOnlineProjectPage.PageTitle;
			}
			return (string)obj;
		}
	}

	public bool IsModsSection => SelectedSection?.Id == "mods";

	public bool IsModSearchVisible
	{
		get
		{
			ResourcesModPageViewModel currentOnlineProjectPage = CurrentOnlineProjectPage;
			if (currentOnlineProjectPage != null)
			{
				if (!currentOnlineProjectPage.IsProjectListStep)
				{
					return currentOnlineProjectPage.IsProjectVersionsStep;
				}
				return true;
			}
			return false;
		}
	}

	public bool IsModProjectDetailsStep => CurrentOnlineProjectPage?.IsProjectContentStep ?? false;

	public string? PageTitleIconSource => CurrentOnlineProjectPage?.PageTitleIconSource;

	public ResourcesModPageViewModel? CurrentOnlineProjectPage => CurrentSectionViewModel as ResourcesModPageViewModel;

	public string ActiveModSearchQuery
	{
		get
		{
			ResourcesModPageViewModel currentOnlineProjectPage = CurrentOnlineProjectPage;
			if (currentOnlineProjectPage == null)
			{
				return string.Empty;
			}
			if (!currentOnlineProjectPage.IsProjectVersionsStep)
			{
				return currentOnlineProjectPage.ProjectList.SearchQuery;
			}
			return currentOnlineProjectPage.Versions.SearchQuery;
		}
		set
		{
			ResourcesModPageViewModel currentOnlineProjectPage = CurrentOnlineProjectPage;
			if (currentOnlineProjectPage != null)
			{
				if (currentOnlineProjectPage.IsProjectVersionsStep)
				{
					currentOnlineProjectPage.Versions.SearchQuery = value;
				}
				else
				{
					currentOnlineProjectPage.ProjectList.SearchQuery = value;
				}
				OnPropertyChanged("ActiveModSearchQuery");
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesSectionItem? SelectedSection
	{
		get
		{
			return selectedSection;
		}
		set
		{
			if (!EqualityComparer<ResourcesSectionItem>.Default.Equals(selectedSection, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedSection);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PageTitle);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsModsSection);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsModSearchVisible);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsModProjectDetailsStep);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentOnlineProjectPage);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PageTitleIconSource);
				selectedSection = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedSection);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PageTitle);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsModsSection);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsModSearchVisible);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsModProjectDetailsStep);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentOnlineProjectPage);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PageTitleIconSource);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesSectionViewModelBase? CurrentSectionViewModel
	{
		get
		{
			return currentSectionViewModel;
		}
		set
		{
			if (!EqualityComparer<ResourcesSectionViewModelBase>.Default.Equals(currentSectionViewModel, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentSectionViewModel);
				currentSectionViewModel = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentSectionViewModel);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ResourcesModProjectItemViewModel?> OpenProjectPageCommand => openProjectPageCommand ?? (openProjectPageCommand = new RelayCommand<ResourcesModProjectItemViewModel>(OpenProjectPage, CanOpenProjectPage));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ResourceProjectRelatedWebsite?> OpenRelatedWebsiteCommand => openRelatedWebsiteCommand ?? (openRelatedWebsiteCommand = new RelayCommand<ResourceProjectRelatedWebsite>(OpenRelatedWebsite, CanOpenRelatedWebsite));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ResourcesSectionItem?> SelectSectionCommand => selectSectionCommand ?? (selectSectionCommand = new RelayCommand<ResourcesSectionItem>(SelectSection));

	public event EventHandler<GameInstance>? ModpackImported;

	public event EventHandler<ResourcesModpackManualDownloadsRequestedEventArgs>? ModpackManualDownloadsRequested;

	public ResourcesPageViewModel(IResourceCatalogService? resourceCatalogService = null, ILogger<ResourcesPageViewModel>? logger = null, IUiDispatcher? uiDispatcher = null, IGameVersionService? gameVersionService = null, GameManagementViewModel? gameManagement = null, IStatusService? statusService = null, IFilePickerService? filePickerService = null, IFloatingMessageService? floatingMessageService = null, DownloadTasksPageViewModel? downloadTasksPage = null, IResourceProjectInstallationService? resourceProjectInstallationService = null, IResourceDependencyPlanningService? resourceDependencyPlanningService = null, IExternalLinkService? externalLinkService = null)
	{
		this.logger = logger;
		this.statusService = statusService;
		this.externalLinkService = externalLinkService;
		this.resourceCatalogService = resourceCatalogService;
		Func<IReadOnlyList<GameInstance>> getInstanceCatalogSnapshot = ((gameManagement == null) ? null : new Func<IReadOnlyList<GameInstance>>(gameManagement.GetInstanceCatalogSnapshot));
		Sections = new ObservableCollection<ResourcesSectionItem>
		{
			new ResourcesSectionItem
			{
				Id = "mods",
				Title = Strings.Resources_SectionMods,
				IconKey = "instance_setting_page/mod"
			},
			new ResourcesSectionItem
			{
				Id = "resource_packs",
				Title = Strings.Resources_SectionResourcePacks,
				IconKey = "main_menu_library"
			},
			new ResourcesSectionItem
			{
				Id = "shader_packs",
				Title = Strings.Resources_SectionShaderPacks,
				IconKey = "instance_setting_page/shader"
			},
			new ResourcesSectionItem
			{
				Id = "worlds",
				Title = Strings.Resources_SectionWorlds,
				IconKey = "world"
			},
			new ResourcesSectionItem
			{
				Id = "modpacks",
				Title = Strings.Resources_SectionModpacks,
				IconKey = "modpack"
			}
		};
		ModPage = new ResourcesModPageViewModel(this, resourceCatalogService, logger, uiDispatcher, gameVersionService, getInstanceCatalogSnapshot, statusService, filePickerService, floatingMessageService, downloadTasksPage, resourceProjectInstallationService, resourceDependencyPlanningService);
		ModPage.PropertyChanged += ModPage_PropertyChanged;
		SubscribeOnlinePageChildren(ModPage);
		ResourcePacksPage = new ResourcesResourcePacksPageViewModel(this, resourceCatalogService, logger, uiDispatcher, gameVersionService, getInstanceCatalogSnapshot, statusService, filePickerService, floatingMessageService, downloadTasksPage, resourceProjectInstallationService, resourceDependencyPlanningService);
		ResourcePacksPage.PropertyChanged += OnlineProjectPage_PropertyChanged;
		SubscribeOnlinePageChildren(ResourcePacksPage);
		ShaderPacksPage = new ResourcesShaderPacksPageViewModel(this, resourceCatalogService, logger, uiDispatcher, gameVersionService, getInstanceCatalogSnapshot, statusService, filePickerService, floatingMessageService, downloadTasksPage, resourceProjectInstallationService, resourceDependencyPlanningService);
		ShaderPacksPage.PropertyChanged += OnlineProjectPage_PropertyChanged;
		SubscribeOnlinePageChildren(ShaderPacksPage);
		WorldsPage = new ResourcesWorldsPageViewModel(this, resourceCatalogService, logger, uiDispatcher, gameVersionService, getInstanceCatalogSnapshot, statusService, filePickerService, floatingMessageService, downloadTasksPage, resourceProjectInstallationService, resourceDependencyPlanningService);
		WorldsPage.PropertyChanged += OnlineProjectPage_PropertyChanged;
		SubscribeOnlinePageChildren(WorldsPage);
		ModpacksPage = new ResourcesModpacksPageViewModel(this, resourceCatalogService, logger, uiDispatcher, gameVersionService, getInstanceCatalogSnapshot, statusService, filePickerService, floatingMessageService, downloadTasksPage, resourceProjectInstallationService, resourceDependencyPlanningService);
		ModpacksPage.PropertyChanged += OnlineProjectPage_PropertyChanged;
		SubscribeOnlinePageChildren(ModpacksPage);
		ModpacksPage.ModpackImported += delegate(object? _, GameInstance instance)
		{
			ModpackImported?.Invoke(this, instance);
		};
		ModpacksPage.ModpackManualDownloadsRequested += delegate(object? _, ResourcesModpackManualDownloadsRequestedEventArgs args)
		{
			ModpackManualDownloadsRequested?.Invoke(this, args);
		};
		SelectSection(Sections[0], logSelection: false);
	}

	[RelayCommand(CanExecute = "CanOpenProjectPage")]
	private void OpenProjectPage(ResourcesModProjectItemViewModel? project)
	{
		if (project == null || externalLinkService == null)
		{
			return;
		}
		try
		{
			if (externalLinkService.TryOpen(project.Project.ProjectUrl))
			{
				return;
			}
		}
		catch (Exception exception)
		{
			logger?.LogWarning(exception, "Failed to open resource project page. Source={Source} ProjectId={ProjectId}", project.Project.Source, project.Project.ProjectId);
		}
		statusService?.Report(Strings.Status_OpenReferenceProjectFailed);
	}

	private bool CanOpenProjectPage(ResourcesModProjectItemViewModel? project)
	{
		if (externalLinkService == null || !Uri.TryCreate(project?.Project.ProjectUrl, UriKind.Absolute, out Uri result))
		{
			return false;
		}
		if (!result.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
		{
			return result.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	[RelayCommand(CanExecute = "CanOpenRelatedWebsite")]
	private void OpenRelatedWebsite(ResourceProjectRelatedWebsite? website)
	{
		if ((object)website == null || externalLinkService == null)
		{
			return;
		}
		try
		{
			if (externalLinkService.TryOpen(website.Url))
			{
				return;
			}
		}
		catch (Exception exception)
		{
			logger?.LogWarning(exception, "Failed to open resource project related website. Source={Source} ProjectId={ProjectId}", website.Source, website.ProjectId);
		}
		statusService?.Report(Strings.Status_OpenRelatedWebsiteFailed);
	}

	private bool CanOpenRelatedWebsite(ResourceProjectRelatedWebsite? website)
	{
		if (externalLinkService == null || !Uri.TryCreate(website?.Url, UriKind.Absolute, out Uri result))
		{
			return false;
		}
		if (result.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			return result.Host.Equals("www.mcresource.cn", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	public void BeginEnsureCurrentSectionLoaded()
	{
		CurrentOnlineProjectPage?.BeginEnsureProjectsLoaded();
	}

	internal async Task<ResourceProject?> LoadProjectDetailsAsync(ResourceProjectReference reference)
	{
		ArgumentNullException.ThrowIfNull(reference, "reference");
		if (resourceCatalogService == null)
		{
			return null;
		}
		try
		{
			ResourceProject resourceProject = await resourceCatalogService.GetProjectAsync(reference);
			if (resourceProject == null)
			{
				statusService?.Report(Strings.Status_OpenResourceDetailsFailed);
				return null;
			}
			return (ResolveProjectSection(reference.Kind) == null) ? null : resourceProject;
		}
		catch (Exception exception)
		{
			logger?.LogError(exception, "Failed to open recognized local resource project details. Kind={Kind} Source={Source} ProjectId={ProjectId}", reference.Kind, reference.Source, reference.ProjectId);
			statusService?.Report(Strings.Status_OpenResourceDetailsFailed);
			return null;
		}
	}

	internal void ShowProjectDetails(ResourceProjectReference reference, ResourceProject project)
	{
		ArgumentNullException.ThrowIfNull(reference, "reference");
		ArgumentNullException.ThrowIfNull(project, "project");
		ResourcesSectionItem resourcesSectionItem = ResolveProjectSection(reference.Kind);
		if (resourcesSectionItem != null)
		{
			SelectSection(resourcesSectionItem, logSelection: false);
			CurrentOnlineProjectPage?.ShowProjectDetails(project);
			logger?.LogInformation("Opened recognized local resource project details. Kind={Kind} Source={Source} ProjectId={ProjectId}", reference.Kind, reference.Source, reference.ProjectId);
		}
	}

	private ResourcesSectionItem? ResolveProjectSection(ResourceProjectKind kind)
	{
		string sectionId = kind switch
		{
			ResourceProjectKind.Mod => "mods", 
			ResourceProjectKind.ResourcePack => "resource_packs", 
			ResourceProjectKind.ShaderPack => "shader_packs", 
			_ => string.Empty, 
		};
		return Sections.FirstOrDefault((ResourcesSectionItem item) => item.Id == sectionId);
	}

	public async Task OpenModsForInstanceAsync(GameInstance instance)
	{
		ResourcesSectionItem section = Sections.FirstOrDefault((ResourcesSectionItem resourcesSectionItem) => resourcesSectionItem.Id == "mods") ?? Sections[0];
		SelectSection(section, logSelection: false);
		logger?.LogDebug("Opening resources mod section from instance. InstanceId={InstanceId}, MinecraftVersion={MinecraftVersion}, Loader={Loader}", instance.Id, instance.MinecraftVersion, instance.Loader);
		await ModPage.ApplyInstanceFiltersAsync(instance);
	}

	[RelayCommand]
	private void SelectSection(ResourcesSectionItem? section)
	{
		SelectSection(section, logSelection: true);
	}

	private void SelectSection(ResourcesSectionItem? section, bool logSelection)
	{
		if (section == null || SelectedSection == section)
		{
			return;
		}
		foreach (ResourcesSectionItem section2 in Sections)
		{
			section2.IsSelected = section2 == section;
		}
		CurrentSectionViewModel = section.Id switch
		{
			"mods" => ModPage, 
			"resource_packs" => ResourcePacksPage, 
			"shader_packs" => ShaderPacksPage, 
			"worlds" => WorldsPage, 
			"modpacks" => ModpacksPage, 
			_ => ModPage, 
		};
		SelectedSection = section;
		if (section.Id != "mods")
		{
			ModPage.ResetToProjectList();
		}
		if (section.Id != "resource_packs")
		{
			ResourcePacksPage.ResetToProjectList();
		}
		if (section.Id != "shader_packs")
		{
			ShaderPacksPage.ResetToProjectList();
		}
		if (section.Id != "worlds")
		{
			WorldsPage.ResetToProjectList();
		}
		if (section.Id != "modpacks")
		{
			ModpacksPage.ResetToProjectList();
		}
		if (logSelection)
		{
			logger?.LogDebug("Resources section selected. SectionId={SectionId}", section.Id);
		}
		if (logSelection)
		{
			CurrentOnlineProjectPage?.BeginEnsureProjectsLoaded();
		}
	}

	private void ModPage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		OnlineProjectPage_PropertyChanged(sender, e);
	}

	private void OnlineProjectPage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		bool flag;
		switch (e.PropertyName)
		{
		case "CurrentStep":
		case "PageTitle":
		case "PageTitleIconSource":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag && sender == CurrentOnlineProjectPage)
		{
			OnPropertyChanged("PageTitle");
			OnPropertyChanged("PageTitleIconSource");
			OnPropertyChanged("IsModSearchVisible");
			OnPropertyChanged("IsModProjectDetailsStep");
			OnPropertyChanged("ActiveModSearchQuery");
		}
	}

	private void SubscribeOnlinePageChildren(ResourcesModPageViewModel page)
	{
		page.ProjectList.PropertyChanged += OnlineProjectChild_PropertyChanged;
		page.Versions.PropertyChanged += OnlineProjectChild_PropertyChanged;
	}

	private void OnlineProjectChild_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		ResourcesModPageViewModel currentOnlineProjectPage = CurrentOnlineProjectPage;
		if (currentOnlineProjectPage != null && (sender == currentOnlineProjectPage.ProjectList || sender == currentOnlineProjectPage.Versions) && e.PropertyName == "SearchQuery")
		{
			OnPropertyChanged("ActiveModSearchQuery");
		}
	}
}
