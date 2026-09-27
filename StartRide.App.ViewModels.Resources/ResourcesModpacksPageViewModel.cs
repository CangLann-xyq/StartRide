using System;
using System.Collections.Generic;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Download;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace StartRide.App.ViewModels.Resources;

public sealed class ResourcesModpacksPageViewModel : ResourcesModPageViewModel
{
	public ResourcesModpacksPageViewModel(ResourcesPageViewModel parent, IResourceCatalogService? resourceCatalogService = null, ILogger? logger = null, IUiDispatcher? uiDispatcher = null, IGameVersionService? gameVersionService = null, Func<IReadOnlyList<GameInstance>>? getInstanceCatalogSnapshot = null, IStatusService? statusService = null, IFilePickerService? filePickerService = null, IFloatingMessageService? floatingMessageService = null, DownloadTasksPageViewModel? downloadTasksPage = null, IResourceProjectInstallationService? resourceProjectInstallationService = null, IResourceDependencyPlanningService? resourceDependencyPlanningService = null)
		: base(parent, CreateModpackOptions(), resourceCatalogService, logger, uiDispatcher, gameVersionService, getInstanceCatalogSnapshot, statusService, filePickerService, floatingMessageService, downloadTasksPage, resourceProjectInstallationService, resourceDependencyPlanningService)
	{
	}

	private static ResourcesOnlineProjectPageOptions CreateModpackOptions()
	{
		return new ResourcesOnlineProjectPageOptions(ResourceProjectKind.Modpack, Strings.Resources_SectionModpacks, "general/general_extention", ShowsLoaderFilters: true, Strings.Resources_ModFilterAllVersions, Strings.Resources_ModpackFilterAllLoaders, Strings.Resources_ModpackProjectsLoading, Strings.Resources_ModpackProjectsEmpty, Strings.Resources_ModpackProjectsLoadError, Strings.Resources_ModpackProjectsLoadingMore, Strings.Resources_ModpackProjectsNoMore, Strings.Resources_ModpackProjectsLoadMoreError, Strings.Resources_ModpackCurseForgeMissingApiKey, Strings.Resources_ModpackDetailsInfoSection, Strings.Resources_ModpackInstallTargetSection, Strings.Resources_ModpackInstallTargetLocal, Strings.Resources_ModpackInstallTargetsLoading, Strings.Resources_ModpackInstallTargetsLoadError, Strings.Resources_ModpackVersionsLoading, Strings.Resources_ModpackVersionsEmpty, Strings.Resources_ModpackVersionsEmptyLocal, Strings.Resources_ModpackVersionsFilterEmpty, Strings.Resources_ModpackVersionsLoadError, Strings.Resources_ModpackVersionsLoadingMore, Strings.Resources_ModpackVersionsNoMore, Strings.Resources_ModpackVersionsLoadMoreError, Strings.Resources_ModpackVersionsAllTitle, Strings.FilePicker_ModpackDownloadDirectoryTitle, Strings.Status_ModpackDownloading, Strings.Status_ModpackDownloadingFormat, Strings.Status_ModpackDownloadedFormat, Strings.Status_ModpackDownloadFailed, Strings.Status_ModpackImportedFormat, Strings.Status_ModpackImportFailed, Strings.Resources_ModpackDownloadFileExistsMessageFormat, new global::_003C_003Ez__ReadOnlyArray<ResourcesOnlineProjectTypeOption>(new ResourcesOnlineProjectTypeOption[9]
		{
			new ResourcesOnlineProjectTypeOption("adventure", Strings.Resources_ModpackFilterTypeAdventure, ResourceProjectCategory.Adventure),
			new ResourcesOnlineProjectTypeOption("technology", Strings.Resources_ModpackFilterTypeTechnology, ResourceProjectCategory.Technology),
			new ResourcesOnlineProjectTypeOption("magic", Strings.Resources_ModpackFilterTypeMagic, ResourceProjectCategory.Magic),
			new ResourcesOnlineProjectTypeOption("optimization", Strings.Resources_ModpackFilterTypeOptimization, ResourceProjectCategory.Optimization),
			new ResourcesOnlineProjectTypeOption("quests", Strings.Resources_ModpackFilterTypeQuests, ResourceProjectCategory.Quests),
			new ResourcesOnlineProjectTypeOption("kitchen-sink", Strings.Resources_ModpackFilterTypeKitchenSink, ResourceProjectCategory.KitchenSink),
			new ResourcesOnlineProjectTypeOption("lightweight", Strings.Resources_ModpackFilterTypeLightweight, ResourceProjectCategory.Lightweight),
			new ResourcesOnlineProjectTypeOption("multiplayer", Strings.Resources_ModpackFilterTypeMultiplayer, ResourceProjectCategory.Multiplayer),
			new ResourcesOnlineProjectTypeOption("exploration", Strings.Resources_ModpackFilterTypeExploration, ResourceProjectCategory.Exploration)
		}), null, ResourcesOnlineProjectInstallTargetMode.NewInstance, Strings.Resources_ModpackInstallTargetNewInstance, Strings.Resources_ModpackInstallTargetServer);
	}
}
