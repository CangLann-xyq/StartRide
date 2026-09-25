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

public sealed class LocalShaderPacksViewModel : IDisposable
{
	private readonly ILocalShaderPackService service;

	private readonly IStatusService statusService;

	private readonly ILogger<LocalShaderPacksViewModel> logger;

	private readonly LocalContentRefreshCoordinator<LocalShaderPack> refreshCoordinator;

	private readonly LocalResourceCategoryEnrichmentCoordinator<LocalShaderPack> categoryEnrichmentCoordinator;

	public ObservableCollection<LocalShaderPack> ShaderPacks { get; } = new ObservableCollection<LocalShaderPack>();

	public IReadOnlyList<LocalShaderPack> CurrentShaderPacks => refreshCoordinator.CurrentItems;

	public long Revision => refreshCoordinator.Revision;

	public event EventHandler? ShaderPacksChanged;

	public event EventHandler<LocalContentIconChangedEventArgs>? IconChanged;

	public LocalShaderPacksViewModel(ILocalShaderPackService localShaderPackService, IStatusService statusService, IInstanceDirectoryMonitor instanceDirectoryMonitor, IUiDispatcher? uiDispatcher = null, ILogger<LocalShaderPacksViewModel>? logger = null, ILocalResourceCategoryEnrichmentService? categoryEnrichmentService = null)
	{
		service = localShaderPackService;
		this.statusService = statusService;
		this.logger = logger ?? NullLogger<LocalShaderPacksViewModel>.Instance;
		IUiDispatcher uiDispatcher2 = uiDispatcher ?? ImmediateUiDispatcher.Instance;
		refreshCoordinator = new LocalContentRefreshCoordinator<LocalShaderPack>(instanceDirectoryMonitor, InstanceDirectoryKind.ShaderPacks, localShaderPackService.GetShaderPacksAsync, Apply, Clear, delegate
		{
			ReportStatus(Strings.Status_LoadLocalShaderPacksFailed);
		}, uiDispatcher2, this.logger);
		categoryEnrichmentCoordinator = new LocalResourceCategoryEnrichmentCoordinator<LocalShaderPack>(categoryEnrichmentService, ResourceProjectKind.ShaderPack, (LocalShaderPack shaderPack) => shaderPack.FullPath, (LocalShaderPack shaderPack) => shaderPack.Categories, delegate(LocalShaderPack shaderPack, IReadOnlyList<ResourceProjectCategory> categories)
		{
			shaderPack.Categories = categories;
		}, () => CurrentShaderPacks, delegate
		{
			ShaderPacksChanged?.Invoke(this, EventArgs.Empty);
		}, uiDispatcher2, this.logger, (LocalShaderPack shaderPack) => shaderPack.IconSource, delegate(LocalShaderPack shaderPack, string iconSource)
		{
			shaderPack.IconSource = iconSource;
		}, preferResolvedIconSource: false, (LocalShaderPack shaderPack) => shaderPack.ProjectReference, delegate(LocalShaderPack shaderPack, ResourceProjectReference reference)
		{
			shaderPack.ProjectReference = reference;
		}, delegate(LocalShaderPack shaderPack, string iconSource)
		{
			IconChanged?.Invoke(this, new LocalContentIconChangedEventArgs(shaderPack.FullPath, iconSource));
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
		HashSet<object> previous = CurrentShaderPacks.ToHashSet(ReferenceEqualityComparer.Instance);
		bool num = await refreshCoordinator.RefreshIfInvalidatedAsync();
		if (num)
		{
			categoryEnrichmentCoordinator.Queue(CurrentShaderPacks.Where((LocalShaderPack item) => !previous.Contains(item)).ToArray());
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

	public async Task<bool> RefreshShaderPacksAsync()
	{
		HashSet<object> previous = CurrentShaderPacks.ToHashSet(ReferenceEqualityComparer.Instance);
		bool num = await refreshCoordinator.RefreshAsync();
		if (num)
		{
			categoryEnrichmentCoordinator.Queue(CurrentShaderPacks.Where((LocalShaderPack item) => !previous.Contains(item)).ToArray());
		}
		return num;
	}

	public async Task<int> DeleteShaderPacksAsync(IEnumerable<LocalShaderPack> shaderPacks)
	{
		return await refreshCoordinator.ExecuteInternalOperationAsync(() => LocalContentBatchExecutor.ExecuteAsync(shaderPacks, (LocalShaderPack item) => item.FullPath, (LocalShaderPack item) => service.DeleteAsync(item), delegate(LocalShaderPack item, Exception exception)
		{
			logger.LogWarning(exception, "Failed to delete local shader pack. Path={Path}", item.FullPath);
		}));
	}

	public async Task<LocalShaderPackImportResult> ImportShaderPackAsync(string archivePath, bool reportStatus = true)
	{
		GameInstance instance = refreshCoordinator.SelectedInstance;
		if (instance == null || string.IsNullOrWhiteSpace(archivePath))
		{
			return LocalShaderPackImportResult.Failure(LocalShaderPackImportFailureReason.UnexpectedError);
		}
		HashSet<object> previous = CurrentShaderPacks.ToHashSet(ReferenceEqualityComparer.Instance);
		LocalShaderPackImportResult localShaderPackImportResult = await refreshCoordinator.ExecuteInternalOperationAsync(() => service.ImportAsync(instance, archivePath), (LocalShaderPackImportResult value) => value.IsSuccess);
		if (!localShaderPackImportResult.IsSuccess)
		{
			if (reportStatus)
			{
				ReportStatus((localShaderPackImportResult.FailureReason == LocalShaderPackImportFailureReason.FileNotFound) ? Strings.Status_LocalShaderPackImportFileNotFound : Strings.Status_LocalShaderPackImportFailed);
			}
			return localShaderPackImportResult;
		}
		categoryEnrichmentCoordinator.Queue(CurrentShaderPacks.Where((LocalShaderPack item) => !previous.Contains(item)).ToArray());
		if (reportStatus)
		{
			ReportStatus(Strings.Status_LocalShaderPackImported);
		}
		return localShaderPackImportResult;
	}

	public void Dispose()
	{
		categoryEnrichmentCoordinator.Dispose();
		refreshCoordinator.Dispose();
	}

	private void Apply(IReadOnlyList<LocalShaderPack> items)
	{
		if (ShaderPacks.SynchronizeByKey<LocalShaderPack, string>(items, (LocalShaderPack item) => item.FullPath, StringComparer.OrdinalIgnoreCase))
		{
			ShaderPacksChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void Clear()
	{
		if (ShaderPacks.Count != 0)
		{
			ShaderPacks.Clear();
			ShaderPacksChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void ReportStatus(string message)
	{
		statusService.Report(message);
	}
}
