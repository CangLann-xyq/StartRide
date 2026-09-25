using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Shared;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class LocalResourcePacksViewModel : IDisposable
{
	private readonly ILocalResourcePackService service;

	private readonly IStatusService statusService;

	private readonly ILogger<LocalResourcePacksViewModel> logger;

	private readonly LocalContentRefreshCoordinator<LocalResourcePack> refreshCoordinator;

	private readonly LocalResourceCategoryEnrichmentCoordinator<LocalResourcePack> categoryEnrichmentCoordinator;

	public ObservableCollection<LocalResourcePack> ResourcePacks { get; } = new ObservableCollection<LocalResourcePack>();

	public IReadOnlyList<LocalResourcePack> CurrentResourcePacks => refreshCoordinator.CurrentItems;

	public long Revision => refreshCoordinator.Revision;

	public event EventHandler? ResourcePacksChanged;

	public event EventHandler<LocalContentIconChangedEventArgs>? IconChanged;

	public LocalResourcePacksViewModel(ILocalResourcePackService localResourcePackService, IStatusService statusService, IInstanceDirectoryMonitor instanceDirectoryMonitor, IUiDispatcher? uiDispatcher = null, ILogger<LocalResourcePacksViewModel>? logger = null, ILocalResourceCategoryEnrichmentService? categoryEnrichmentService = null)
	{
		service = localResourcePackService;
		this.statusService = statusService;
		this.logger = logger ?? NullLogger<LocalResourcePacksViewModel>.Instance;
		IUiDispatcher uiDispatcher2 = uiDispatcher ?? ImmediateUiDispatcher.Instance;
		refreshCoordinator = new LocalContentRefreshCoordinator<LocalResourcePack>(instanceDirectoryMonitor, InstanceDirectoryKind.ResourcePacks, localResourcePackService.GetResourcePacksAsync, Apply, Clear, delegate
		{
			ReportStatus(Strings.Status_LoadLocalResourcePacksFailed);
		}, uiDispatcher2, this.logger);
		categoryEnrichmentCoordinator = new LocalResourceCategoryEnrichmentCoordinator<LocalResourcePack>(categoryEnrichmentService, ResourceProjectKind.ResourcePack, (LocalResourcePack resourcePack) => resourcePack.FullPath, (LocalResourcePack resourcePack) => resourcePack.Categories, delegate(LocalResourcePack resourcePack, IReadOnlyList<ResourceProjectCategory> categories)
		{
			resourcePack.Categories = categories;
		}, () => CurrentResourcePacks, delegate
		{
			ResourcePacksChanged?.Invoke(this, EventArgs.Empty);
		}, uiDispatcher2, this.logger, (LocalResourcePack resourcePack) => resourcePack.IconSource, delegate(LocalResourcePack resourcePack, string iconSource)
		{
			resourcePack.IconSource = iconSource;
		}, preferResolvedIconSource: true, (LocalResourcePack resourcePack) => resourcePack.ProjectReference, delegate(LocalResourcePack resourcePack, ResourceProjectReference reference)
		{
			resourcePack.ProjectReference = reference;
		}, delegate(LocalResourcePack resourcePack, string iconSource)
		{
			IconChanged?.Invoke(this, new LocalContentIconChangedEventArgs(resourcePack.FullPath, iconSource));
		});
	}

	public void SetSelectedInstance(GameInstance? instance)
	{
		categoryEnrichmentCoordinator.Cancel();
		refreshCoordinator.SetInstance(instance);
	}

	public void SetSectionActive(bool active)
	{
		refreshCoordinator.SetSectionActive(active);
	}

	public async Task<bool> RefreshIfInvalidatedAsync()
	{
		HashSet<object> previous = CurrentResourcePacks.ToHashSet(ReferenceEqualityComparer.Instance);
		bool num = await refreshCoordinator.RefreshIfInvalidatedAsync();
		if (num)
		{
			categoryEnrichmentCoordinator.Queue(CurrentResourcePacks.Where((LocalResourcePack item) => !previous.Contains(item)).ToArray());
		}
		return num;
	}

	public void InvalidateSnapshot()
	{
		refreshCoordinator.InvalidateSnapshot();
	}

	public void ReleaseObservation()
	{
		categoryEnrichmentCoordinator.Cancel();
		refreshCoordinator.ReleaseObservation();
	}

	public void SuspendWatcherForInstanceRename()
	{
		refreshCoordinator.SuspendForRename();
	}

	public void ResumeWatcherAfterInstanceRename(bool restart = true)
	{
		refreshCoordinator.ResumeAfterRename(restart);
	}

	public async Task<bool> RefreshResourcePacksAsync()
	{
		HashSet<object> previous = CurrentResourcePacks.ToHashSet(ReferenceEqualityComparer.Instance);
		bool num = await refreshCoordinator.RefreshAsync();
		if (num)
		{
			categoryEnrichmentCoordinator.Queue(CurrentResourcePacks.Where((LocalResourcePack item) => !previous.Contains(item)).ToArray());
		}
		return num;
	}

	public async Task<int> DeleteResourcePacksAsync(IEnumerable<LocalResourcePack> resourcePacks)
	{
		return await refreshCoordinator.ExecuteInternalOperationAsync(() => LocalContentBatchExecutor.ExecuteAsync(resourcePacks, (LocalResourcePack item) => item.FullPath, (LocalResourcePack item) => service.DeleteAsync(item), delegate(LocalResourcePack item, Exception exception)
		{
			logger.LogWarning(exception, "Failed to delete local resource pack. Path={Path}", item.FullPath);
		}));
	}

	public async Task<LocalResourcePackImportResult> ImportResourcePackAsync(string archivePath, bool reportStatus = true)
	{
		GameInstance instance = refreshCoordinator.SelectedInstance;
		if (instance == null || string.IsNullOrWhiteSpace(archivePath))
		{
			return LocalResourcePackImportResult.Failure(LocalResourcePackImportFailureReason.UnexpectedError);
		}
		HashSet<object> previous = CurrentResourcePacks.ToHashSet(ReferenceEqualityComparer.Instance);
		LocalResourcePackImportResult localResourcePackImportResult = await refreshCoordinator.ExecuteInternalOperationAsync(() => service.ImportAsync(instance, archivePath), (LocalResourcePackImportResult value) => value.IsSuccess);
		if (!localResourcePackImportResult.IsSuccess)
		{
			if (reportStatus)
			{
				ReportStatus((localResourcePackImportResult.FailureReason == LocalResourcePackImportFailureReason.FileNotFound) ? Strings.Status_LocalResourcePackImportFileNotFound : Strings.Status_LocalResourcePackImportFailed);
			}
			return localResourcePackImportResult;
		}
		categoryEnrichmentCoordinator.Queue(CurrentResourcePacks.Where((LocalResourcePack item) => !previous.Contains(item)).ToArray());
		if (reportStatus)
		{
			ReportStatus(Strings.Status_LocalResourcePackImported);
		}
		return localResourcePackImportResult;
	}

	public void Dispose()
	{
		categoryEnrichmentCoordinator.Dispose();
		refreshCoordinator.Dispose();
	}

	private void Apply(IReadOnlyList<LocalResourcePack> items)
	{
		if (ResourcePacks.SynchronizeByKey<LocalResourcePack, string>(items, (LocalResourcePack item) => item.FullPath, StringComparer.OrdinalIgnoreCase))
		{
			ResourcePacksChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void Clear()
	{
		if (ResourcePacks.Count != 0)
		{
			ResourcePacks.Clear();
			ResourcePacksChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void ReportStatus(string message)
	{
		statusService.Report(message);
	}
}
