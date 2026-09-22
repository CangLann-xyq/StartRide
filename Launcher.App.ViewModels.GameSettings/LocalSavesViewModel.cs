using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.ViewModels.Shared;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.GameSettings;

public sealed class LocalSavesViewModel : IDisposable
{
	private readonly ILocalSaveService localSaveService;

	private readonly IStatusService statusService;

	private readonly ILogger<LocalSavesViewModel> logger;

	private readonly LocalContentRefreshCoordinator<LocalSave> refreshCoordinator;

	public ObservableCollection<LocalSave> Saves { get; } = new ObservableCollection<LocalSave>();

	public IReadOnlyList<LocalSave> CurrentSaves => refreshCoordinator.CurrentItems;

	public long Revision => refreshCoordinator.Revision;

	public event EventHandler? SavesChanged;

	public LocalSavesViewModel(ILocalSaveService localSaveService, IStatusService statusService, IInstanceDirectoryMonitor instanceDirectoryMonitor, IUiDispatcher? uiDispatcher = null, ILogger<LocalSavesViewModel>? logger = null)
	{
		this.localSaveService = localSaveService;
		this.statusService = statusService;
		this.logger = logger ?? NullLogger<LocalSavesViewModel>.Instance;
		refreshCoordinator = new LocalContentRefreshCoordinator<LocalSave>(instanceDirectoryMonitor, InstanceDirectoryKind.Saves, localSaveService.GetSavesAsync, ApplySaves, ClearSaves, delegate
		{
			ReportStatus(Strings.Status_LoadLocalSavesFailed);
		}, uiDispatcher ?? ImmediateUiDispatcher.Instance, this.logger);
	}

	public void SetSelectedInstance(GameInstance? instance)
	{
		refreshCoordinator.SetInstance(instance);
		logger.LogInformation("Selected instance changed for local saves view. InstanceId={InstanceId}", instance?.Id ?? "<none>");
	}

	public void SetSectionActive(bool active)
	{
		refreshCoordinator.SetSectionActive(active);
	}

	public Task<bool> RefreshIfInvalidatedAsync()
	{
		return refreshCoordinator.RefreshIfInvalidatedAsync();
	}

	public void InvalidateSnapshot()
	{
		refreshCoordinator.InvalidateSnapshot();
	}

	public void ReleaseObservation()
	{
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

	public Task<bool> RefreshSavesAsync()
	{
		return refreshCoordinator.RefreshAsync();
	}

	public async Task DeleteSaveAsync(LocalSave save)
	{
		await refreshCoordinator.ExecuteInternalOperationAsync(() => localSaveService.DeleteAsync(save));
	}

	public async Task<int> DeleteSavesAsync(IEnumerable<LocalSave> saves)
	{
		return await refreshCoordinator.ExecuteInternalOperationAsync(() => LocalContentBatchExecutor.ExecuteAsync(saves, (LocalSave save) => save.FullPath, (LocalSave save) => localSaveService.DeleteAsync(save), delegate(LocalSave save, Exception exception)
		{
			logger.LogWarning(exception, "Failed to delete local save. Path={Path}", save.FullPath);
		}));
	}

	public async Task<LocalSaveImportResult> ImportSaveFromArchiveAsync(string archivePath, bool reportStatus = true)
	{
		GameInstance instance = refreshCoordinator.SelectedInstance;
		if (instance == null || string.IsNullOrWhiteSpace(archivePath))
		{
			return LocalSaveImportResult.Failure(LocalSaveImportFailureReason.UnexpectedError);
		}
		LocalSaveImportResult localSaveImportResult = await refreshCoordinator.ExecuteInternalOperationAsync(() => localSaveService.ImportFromArchiveAsync(instance, archivePath), (LocalSaveImportResult value) => value.IsSuccess);
		if (!localSaveImportResult.IsSuccess)
		{
			if (reportStatus)
			{
				ReportStatus((localSaveImportResult.FailureReason == LocalSaveImportFailureReason.FileNotFound) ? Strings.Status_LocalSaveImportFileNotFound : Strings.Status_LocalSaveImportFailed);
			}
			return localSaveImportResult;
		}
		if (reportStatus)
		{
			ReportStatus(Strings.Status_LocalSaveImported);
		}
		return localSaveImportResult;
	}

	public void Dispose()
	{
		refreshCoordinator.Dispose();
	}

	private void ApplySaves(IReadOnlyList<LocalSave> saves)
	{
		if (Saves.SynchronizeByKey<LocalSave, string>(saves, (LocalSave save) => save.FullPath, StringComparer.OrdinalIgnoreCase))
		{
			SavesChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void ClearSaves()
	{
		if (Saves.Count != 0)
		{
			Saves.Clear();
			SavesChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void ReportStatus(string message)
	{
		statusService.Report(message);
	}
}
