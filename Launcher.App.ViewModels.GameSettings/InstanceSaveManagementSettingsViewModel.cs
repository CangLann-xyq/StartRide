using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.ViewModels.Shared;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.GameSettings;

public sealed class InstanceSaveManagementSettingsViewModel : GameSettingsDetailsSectionViewModelBase
{
	private readonly LocalSavesViewModel localSavesViewModel;

	private readonly IStatusService statusService;

	private readonly IInstanceFolderService instanceFolderService;

	private readonly IFilePickerService filePickerService;

	private readonly IInstanceContentImportPathValidator importPathValidator;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger<InstanceSaveManagementSettingsViewModel> logger;

	private readonly LocalContentSelectionState<SaveManagementSaveItemViewModel> selectionState;

	private Task? loadTask;

	private GameInstance? selectedInstance;

	private bool hasPendingVisualRefresh;

	private bool isVisibleRefreshQueued;

	private bool isSectionActive;

	private bool isInitialProjectionReady;

	private bool suppressLocalCollectionEvents;

	private long lifecycleGeneration;

	[ObservableProperty]
	private int installedSaveCount;

	[ObservableProperty]
	private SaveManagementSaveItemViewModel? selectedSave;

	[ObservableProperty]
	private string saveSearchQuery = string.Empty;

	[ObservableProperty]
	private bool isMultiSelectMode;

	[ObservableProperty]
	private int selectedSaveCount;

	[ObservableProperty]
	private bool isLoadingSaves;

	[ObservableProperty]
	private bool hasLoadedSaves;

	[ObservableProperty]
	private IReadOnlyList<SaveManagementSaveItemViewModel> visibleSaves = Array.Empty<SaveManagementSaveItemViewModel>();

	[ObservableProperty]
	private IReadOnlyList<object> visibleSaveListItems = Array.Empty<object>();

	[ObservableProperty]
	private int listEntranceAnimationToken;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openSaveFolderCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? importLocalSaveCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? toggleMultiSelectModeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? selectAllSavesCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestDeleteSelectedSavesCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<SaveManagementSaveItemViewModel?>? openSaveLocationCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<SaveManagementSaveItemViewModel?>? requestDeleteSaveCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<SaveManagementSaveItemViewModel?>? selectSaveCommand;

	public override bool UsesFullViewportLayout => true;

	public IReadOnlyList<SaveManagementSaveItemViewModel> Saves => VisibleSaves;

	public bool CanShowSaveInfoSection => selectedInstance != null;

	public bool HasSaves => Saves.Count > 0;

	public bool CanShowSaveScrollableContent
	{
		get
		{
			if (selectedInstance != null)
			{
				return isInitialProjectionReady;
			}
			return false;
		}
	}

	public bool HasInstalledSaves => InstalledSaveCount > 0;

	public bool CanShowSaveListSection
	{
		get
		{
			if (selectedInstance != null)
			{
				if (!IsLoadingSaves)
				{
					return HasInstalledSaves;
				}
				return true;
			}
			return false;
		}
	}

	public bool CanShowNoSavesEmptyState
	{
		get
		{
			if (selectedInstance != null && HasLoadedSaves && !IsLoadingSaves)
			{
				return !HasInstalledSaves;
			}
			return false;
		}
	}

	public bool CanShowSaveEmptyState
	{
		get
		{
			if (selectedInstance != null && HasLoadedSaves && !IsLoadingSaves && HasInstalledSaves)
			{
				return !HasSaves;
			}
			return false;
		}
	}

	public bool CanShowSaveLoadingState
	{
		get
		{
			if (selectedInstance != null && IsLoadingSaves)
			{
				return !HasLoadedSaves;
			}
			return false;
		}
	}

	public bool HasSelectedSaves => SelectedSaveCount > 0;

	public bool AreAllVisibleSavesSelected
	{
		get
		{
			if (HasSaves)
			{
				return SelectedSaveCount == Saves.Count;
			}
			return false;
		}
	}

	public bool CanImportLocalSave => selectedInstance != null;

	public string SelectAllButtonText
	{
		get
		{
			if (!AreAllVisibleSavesSelected)
			{
				return Strings.GameSettings_SaveManagementSelectAllButton;
			}
			return Strings.GameSettings_SaveManagementCancelSelectAllButton;
		}
	}

	public string InstalledSummaryText
	{
		get
		{
			if (!IsLoadingSaves || HasLoadedSaves)
			{
				return string.Format(Strings.GameSettings_SaveManagementInstalledSummaryFormat, InstalledSaveCount);
			}
			return Strings.GameSettings_SaveManagementLoading;
		}
	}

	public string SaveEmptyMessage
	{
		get
		{
			if (HasInstalledSaves && !string.IsNullOrWhiteSpace(SaveSearchQuery))
			{
				return Strings.GameSettings_SaveManagementSearchEmptyMessage;
			}
			return Strings.GameSettings_SaveManagementEmptyMessage;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int InstalledSaveCount
	{
		get
		{
			return installedSaveCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(installedSaveCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InstalledSaveCount);
				installedSaveCount = value;
				OnInstalledSaveCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InstalledSaveCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SaveManagementSaveItemViewModel? SelectedSave
	{
		get
		{
			return selectedSave;
		}
		set
		{
			if (!EqualityComparer<SaveManagementSaveItemViewModel>.Default.Equals(selectedSave, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedSave);
				selectedSave = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedSave);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string SaveSearchQuery
	{
		get
		{
			return saveSearchQuery;
		}
		[MemberNotNull("saveSearchQuery")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(saveSearchQuery, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SaveSearchQuery);
				saveSearchQuery = value;
				OnSaveSearchQueryChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SaveSearchQuery);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsMultiSelectMode
	{
		get
		{
			return isMultiSelectMode;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isMultiSelectMode, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsMultiSelectMode);
				isMultiSelectMode = value;
				OnIsMultiSelectModeChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsMultiSelectMode);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int SelectedSaveCount
	{
		get
		{
			return selectedSaveCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(selectedSaveCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedSaveCount);
				selectedSaveCount = value;
				OnSelectedSaveCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedSaveCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLoadingSaves
	{
		get
		{
			return isLoadingSaves;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLoadingSaves, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLoadingSaves);
				isLoadingSaves = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLoadingSaves);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasLoadedSaves
	{
		get
		{
			return hasLoadedSaves;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(hasLoadedSaves, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasLoadedSaves);
				hasLoadedSaves = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasLoadedSaves);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<SaveManagementSaveItemViewModel> VisibleSaves
	{
		get
		{
			return visibleSaves;
		}
		[MemberNotNull("visibleSaves")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<SaveManagementSaveItemViewModel>>.Default.Equals(visibleSaves, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VisibleSaves);
				visibleSaves = value;
				OnVisibleSavesChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VisibleSaves);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<object> VisibleSaveListItems
	{
		get
		{
			return visibleSaveListItems;
		}
		[MemberNotNull("visibleSaveListItems")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<object>>.Default.Equals(visibleSaveListItems, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VisibleSaveListItems);
				visibleSaveListItems = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VisibleSaveListItems);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int ListEntranceAnimationToken
	{
		get
		{
			return listEntranceAnimationToken;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(listEntranceAnimationToken, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ListEntranceAnimationToken);
				listEntranceAnimationToken = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ListEntranceAnimationToken);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenSaveFolderCommand => openSaveFolderCommand ?? (openSaveFolderCommand = new RelayCommand(OpenSaveFolder));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ImportLocalSaveCommand => importLocalSaveCommand ?? (importLocalSaveCommand = new AsyncRelayCommand(ImportLocalSaveAsync, () => CanImportLocalSave));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ToggleMultiSelectModeCommand => toggleMultiSelectModeCommand ?? (toggleMultiSelectModeCommand = new RelayCommand(ToggleMultiSelectMode));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand SelectAllSavesCommand => selectAllSavesCommand ?? (selectAllSavesCommand = new RelayCommand(SelectAllSaves, CanToggleSelectAllSaves));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestDeleteSelectedSavesCommand => requestDeleteSelectedSavesCommand ?? (requestDeleteSelectedSavesCommand = new RelayCommand(RequestDeleteSelectedSaves, () => HasSelectedSaves));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<SaveManagementSaveItemViewModel?> OpenSaveLocationCommand => openSaveLocationCommand ?? (openSaveLocationCommand = new RelayCommand<SaveManagementSaveItemViewModel>(OpenSaveLocation));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<SaveManagementSaveItemViewModel?> RequestDeleteSaveCommand => requestDeleteSaveCommand ?? (requestDeleteSaveCommand = new RelayCommand<SaveManagementSaveItemViewModel>(RequestDeleteSave));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<SaveManagementSaveItemViewModel?> SelectSaveCommand => selectSaveCommand ?? (selectSaveCommand = new RelayCommand<SaveManagementSaveItemViewModel>(SelectSave));

	public event Action<SaveDeleteRequest>? DeleteSavesRequested;

	public event Action<SaveImportFailureRequest>? SaveImportFailedRequested;

	[RelayCommand]
	private void OpenSaveFolder()
	{
		if (selectedInstance == null)
		{
			return;
		}
		try
		{
			string text = instanceFolderService.EnsureDirectoryExists(Path.Combine(selectedInstance.InstanceDirectory, "saves"));
			logger.LogDebug("Opening save folder. InstanceId={InstanceId} SavesDirectory={SavesDirectory}", selectedInstance.Id, text);
			if (!instanceFolderService.TryOpen(text))
			{
				logger.LogWarning("Failed to open save folder. InstanceId={InstanceId} SavesDirectory={SavesDirectory}", selectedInstance.Id, text);
				statusService.Report(Strings.Status_OpenLocalSaveFolderFailed);
			}
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to prepare save folder for opening. InstanceId={InstanceId}", selectedInstance.Id);
			statusService.Report(Strings.Status_OpenLocalSaveFolderFailed);
		}
	}

	[RelayCommand(CanExecute = "CanImportLocalSave")]
	private async Task ImportLocalSaveAsync()
	{
		if (selectedInstance != null)
		{
			string text = filePickerService.PickSaveArchive();
			if (!string.IsNullOrWhiteSpace(text))
			{
				await ImportSaveArchivesAsync(new global::_003C_003Ez__ReadOnlySingleElementList<string>(text), ImportTriggerSource.FilePicker);
			}
		}
	}

	public GameSettingsFileDropEvaluation EvaluateDroppedFiles(IReadOnlyList<string> paths)
	{
		if (!TryValidateImportPaths(paths, Strings.GameSettings_DropSaveArchivesOnlyMessage, out string failureMessage))
		{
			return GameSettingsFileDropEvaluation.Reject(failureMessage);
		}
		return GameSettingsFileDropEvaluation.Accept(Strings.GameSettings_DropImportSavesMessage);
	}

	public Task ImportDroppedSaveArchivesAsync(IReadOnlyList<string> paths)
	{
		return ImportSaveArchivesAsync(paths, ImportTriggerSource.DragDrop);
	}

	[RelayCommand]
	private void ToggleMultiSelectMode()
	{
		if (IsMultiSelectMode)
		{
			ExitMultiSelectMode();
		}
		else
		{
			EnterMultiSelectMode();
		}
	}

	[RelayCommand(CanExecute = "CanToggleSelectAllSaves")]
	private void SelectAllSaves()
	{
		if (AreAllVisibleSavesSelected)
		{
			selectionState.ClearVisibleSelections(Saves);
			selectionState.ClearSelectedPaths();
			SelectedSave = null;
			UpdateSelectedSaveState();
		}
		else
		{
			selectionState.SelectAll(Saves);
			SelectedSave = null;
			UpdateSelectedSaveState();
		}
	}

	[RelayCommand(CanExecute = "HasSelectedSaves")]
	private void RequestDeleteSelectedSaves()
	{
		IReadOnlyList<SaveManagementSaveItemViewModel> selectedVisibleSaves = GetSelectedVisibleSaves();
		if (selectedVisibleSaves.Count != 0)
		{
			DeleteSavesRequested?.Invoke(new SaveDeleteRequest(selectedVisibleSaves.Select((SaveManagementSaveItemViewModel save) => save.FullPath).ToArray(), selectedVisibleSaves.Select((SaveManagementSaveItemViewModel save) => save.Title).ToArray()));
		}
	}

	[RelayCommand]
	private void OpenSaveLocation(SaveManagementSaveItemViewModel? save)
	{
		if (save == null)
		{
			return;
		}
		try
		{
			if (!instanceFolderService.TryOpen(save.FullPath))
			{
				logger.LogWarning("Failed to open local save directory. InstanceId={InstanceId} Path={Path}", selectedInstance?.Id ?? "<none>", save.FullPath);
				statusService.Report(Strings.Status_OpenLocalSaveFolderFailed);
			}
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to open local save directory. InstanceId={InstanceId} Path={Path}", selectedInstance?.Id ?? "<none>", save.FullPath);
			statusService.Report(Strings.Status_OpenLocalSaveFolderFailed);
		}
	}

	[RelayCommand]
	private void RequestDeleteSave(SaveManagementSaveItemViewModel? save)
	{
		if (save != null)
		{
			DeleteSavesRequested?.Invoke(new SaveDeleteRequest(new global::_003C_003Ez__ReadOnlySingleElementList<string>(save.FullPath), new global::_003C_003Ez__ReadOnlySingleElementList<string>(save.Title)));
		}
	}

	[RelayCommand]
	private void SelectSave(SaveManagementSaveItemViewModel? save)
	{
		if (save == null)
		{
			SelectedSave = null;
			if (IsMultiSelectMode)
			{
				selectionState.ClearSelectedPaths();
			}
			selectionState.ClearVisibleSelections(Saves);
			UpdateSelectedSaveState();
		}
		else if (IsMultiSelectMode)
		{
			selectionState.ToggleSelection(save);
			SelectedSave = null;
			UpdateSelectedSaveState();
		}
		else
		{
			SelectedSave = save;
			selectionState.SelectSingle(save, Saves);
		}
	}

	public async Task DeleteSavesAsync(IReadOnlyList<string> fullPaths)
	{
		ArgumentNullException.ThrowIfNull(fullPaths, "fullPaths");
		IReadOnlyList<LocalSave> savesToDelete = ResolveLocalSaves(fullPaths);
		if (savesToDelete.Count == 0)
		{
			ExitMultiSelectMode();
			return;
		}
		logger.LogInformation("Deleting selected saves. InstanceId={InstanceId} Count={Count}", selectedInstance?.Id ?? "<none>", savesToDelete.Count);
		try
		{
			int failedCount = await localSavesViewModel.DeleteSavesAsync(savesToDelete);
			ExitMultiSelectMode();
			ReportBatchOperationResult(savesToDelete.Count, failedCount, Strings.Status_SelectedSavesDeletedFormat, Strings.Status_SelectedSavesDeletePartialFailedFormat);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to delete selected saves. InstanceId={InstanceId}", selectedInstance?.Id ?? "<none>");
			statusService.Report(Strings.Status_SelectedSavesDeleteFailed);
		}
	}

	public InstanceSaveManagementSettingsViewModel(GameSettingsDetailsViewModel parent, LocalSavesViewModel localSavesViewModel, IStatusService statusService, IInstanceFolderService instanceFolderService, IFilePickerService filePickerService, IInstanceContentImportPathValidator importPathValidator, IUiDispatcher? uiDispatcher = null, ILogger<InstanceSaveManagementSettingsViewModel>? logger = null)
		: base(parent)
	{
		this.localSavesViewModel = localSavesViewModel;
		this.statusService = statusService;
		this.instanceFolderService = instanceFolderService;
		this.filePickerService = filePickerService;
		this.importPathValidator = importPathValidator;
		this.uiDispatcher = uiDispatcher ?? ImmediateUiDispatcher.Instance;
		this.logger = logger ?? NullLogger<InstanceSaveManagementSettingsViewModel>.Instance;
		selectionState = new LocalContentSelectionState<SaveManagementSaveItemViewModel>((SaveManagementSaveItemViewModel save) => save.FullPath, (SaveManagementSaveItemViewModel save) => save.IsSelected, delegate(SaveManagementSaveItemViewModel save, bool isSelected)
		{
			save.IsSelected = isSelected;
		});
		this.localSavesViewModel.SavesChanged += LocalSavesViewModel_SavesChanged;
	}

	public Task SetSelectedInstanceAsync(GameInstance? instance)
	{
		OnSelectedInstanceChanged(instance);
		return EnsureLoadedForSelectedInstanceAsync();
	}

	public override void OnSelectedInstanceChanged(GameInstance? instance)
	{
		Interlocked.Increment(ref lifecycleGeneration);
		selectedInstance = instance;
		loadTask = null;
		hasPendingVisualRefresh = false;
		isVisibleRefreshQueued = false;
		suppressLocalCollectionEvents = true;
		try
		{
			localSavesViewModel.SetSelectedInstance(instance);
			localSavesViewModel.SetSectionActive(isSectionActive && selectedInstance != null);
		}
		finally
		{
			suppressLocalCollectionEvents = false;
		}
		IsLoadingSaves = false;
		HasLoadedSaves = false;
		selectionState.ClearCache();
		SetInitialProjectionReady(value: false);
		ResetSelectionState();
		ClearDisplayedSaves();
		ImportLocalSaveCommand.NotifyCanExecuteChanged();
	}

	public bool RefreshSelectedInstanceReference(GameInstance? instance)
	{
		if (ShouldResetForInstanceReference(instance))
		{
			OnSelectedInstanceChanged(instance);
			return true;
		}
		selectedInstance = instance;
		ImportLocalSaveCommand.NotifyCanExecuteChanged();
		return false;
	}

	public override void OnSectionDeactivated()
	{
		if (isSectionActive)
		{
			isSectionActive = false;
			Interlocked.Increment(ref lifecycleGeneration);
			loadTask = null;
			IsLoadingSaves = false;
			localSavesViewModel.SetSectionActive(active: false);
		}
	}

	public void ReleaseLocalObservation()
	{
		isSectionActive = false;
		Interlocked.Increment(ref lifecycleGeneration);
		loadTask = null;
		IsLoadingSaves = false;
		localSavesViewModel.ReleaseObservation();
	}

	public void SuspendLocalWatchersForInstanceRename()
	{
		localSavesViewModel.SuspendWatcherForInstanceRename();
	}

	public void ResumeLocalWatchersAfterInstanceRename(bool restart = true)
	{
		if (restart && isSectionActive)
		{
			Interlocked.Increment(ref lifecycleGeneration);
			loadTask = null;
			localSavesViewModel.InvalidateSnapshot();
		}
		localSavesViewModel.ResumeWatcherAfterInstanceRename(restart);
	}

	public override Task OnSectionActivatedAsync()
	{
		if (!isSectionActive)
		{
			isSectionActive = true;
			Interlocked.Increment(ref lifecycleGeneration);
			loadTask = null;
			localSavesViewModel.SetSectionActive(selectedInstance != null);
		}
		if (selectedInstance == null)
		{
			return Task.CompletedTask;
		}
		if (HasLoadedSaves)
		{
			if (hasPendingVisualRefresh)
			{
				hasPendingVisualRefresh = false;
				RefreshFromLocalSaves();
			}
			loadTask = RefreshCachedSavesAsync(Volatile.Read(in lifecycleGeneration));
			return loadTask;
		}
		return EnsureLoadedForSelectedInstanceAsync();
	}

	public Task EnsureLoadedForSelectedInstanceAsync()
	{
		if (!isSectionActive || selectedInstance == null)
		{
			return Task.CompletedTask;
		}
		Task task = loadTask;
		if (task != null && !task.IsCompleted)
		{
			return loadTask;
		}
		if (HasLoadedSaves)
		{
			return Task.CompletedTask;
		}
		loadTask = LoadSavesAsync(Volatile.Read(in lifecycleGeneration));
		return loadTask;
	}

	private bool IsCurrentLifecycle(long generation, GameInstance expectedInstance)
	{
		if (isSectionActive && generation == Volatile.Read(in lifecycleGeneration) && string.Equals(expectedInstance.Id, selectedInstance?.Id, StringComparison.Ordinal))
		{
			return string.Equals(expectedInstance.InstanceDirectory, selectedInstance?.InstanceDirectory, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private async Task ImportSaveArchivesAsync(IReadOnlyList<string> archivePaths, ImportTriggerSource source)
	{
		if (selectedInstance == null)
		{
			return;
		}
		if (!TryValidateImportPaths(archivePaths, Strings.GameSettings_DropSaveArchivesOnlyMessage, out string failureMessage))
		{
			if (source == ImportTriggerSource.DragDrop)
			{
				statusService.Report(failureMessage);
			}
			else
			{
				SaveImportFailedRequested?.Invoke(new SaveImportFailureRequest(Strings.Dialog_UnsupportedSaveArchiveMessage));
			}
			return;
		}
		logger.LogInformation("Starting local save import batch. InstanceId={InstanceId} Source={Source} FileCount={FileCount}", selectedInstance.Id, source, archivePaths.Count);
		LocalContentImportBatchResult<LocalSaveImportResult> localContentImportBatchResult = await LocalContentImportBatchCoordinator.ExecuteAsync(archivePaths, async delegate(string archivePath)
		{
			logger.LogDebug("Importing local save archive. InstanceId={InstanceId} ArchivePath={ArchivePath}", selectedInstance.Id, archivePath);
			return await localSavesViewModel.ImportSaveFromArchiveAsync(archivePath, reportStatus: false);
		}, (LocalSaveImportResult result) => result.IsSuccess);
		if (localContentImportBatchResult.Failure != null)
		{
			switch (localContentImportBatchResult.Failure.FailureReason)
			{
			case LocalSaveImportFailureReason.InvalidMinecraftSaveArchive:
				SaveImportFailedRequested?.Invoke(new SaveImportFailureRequest(Strings.Dialog_InvalidSaveArchiveMessage));
				break;
			case LocalSaveImportFailureReason.UnsupportedArchive:
				SaveImportFailedRequested?.Invoke(new SaveImportFailureRequest(Strings.Dialog_UnsupportedSaveArchiveMessage));
				break;
			case LocalSaveImportFailureReason.FileNotFound:
				statusService.Report(Strings.Status_LocalSaveImportFileNotFound);
				break;
			case LocalSaveImportFailureReason.UnexpectedError:
				logger.LogWarning("Local save import failed unexpectedly after service call. InstanceId={InstanceId} ArchivePath={ArchivePath}", selectedInstance.Id, localContentImportBatchResult.FailedPath);
				statusService.Report(Strings.Status_LocalSaveImportFailed);
				break;
			}
		}
		else
		{
			if (localContentImportBatchResult.SuccessCount > 0)
			{
				statusService.Report((localContentImportBatchResult.SuccessCount == 1) ? Strings.Status_LocalSaveImported : string.Format(Strings.Status_LocalSavesImportedFormat, localContentImportBatchResult.SuccessCount));
			}
			logger.LogInformation("Local save import batch completed. InstanceId={InstanceId} RequestedCount={RequestedCount} ImportedCount={ImportedCount}", selectedInstance.Id, archivePaths.Count, localContentImportBatchResult.SuccessCount);
		}
	}

	private bool TryValidateImportPaths(IReadOnlyList<string> paths, string invalidTypeMessage, out string failureMessage)
	{
		return LocalContentImportPathEvaluator.TryValidate(importPathValidator, paths, InstanceContentImportKind.SaveArchive, invalidTypeMessage, out failureMessage);
	}

	private async Task LoadSavesAsync(long generation)
	{
		if (selectedInstance == null)
		{
			return;
		}
		GameInstance expectedInstance = selectedInstance;
		SetInitialProjectionReady(value: false);
		IsLoadingSaves = true;
		OnPropertyChanged("InstalledSummaryText");
		RaiseAvailabilityPropertyChanges();
		try
		{
			if (await localSavesViewModel.RefreshSavesAsync() && IsCurrentLifecycle(generation, expectedInstance))
			{
				HasLoadedSaves = true;
				if (isSectionActive)
				{
					PublishReadyProjection();
				}
				else
				{
					hasPendingVisualRefresh = true;
				}
			}
		}
		catch (Exception exception)
		{
			if (IsCurrentLifecycle(generation, expectedInstance))
			{
				logger.LogError(exception, "Failed to load saves for section activation. InstanceId={InstanceId}", expectedInstance.Id);
				HasLoadedSaves = false;
				ClearDisplayedSaves();
				hasPendingVisualRefresh = false;
				SetInitialProjectionReady(value: true);
				statusService.Report(Strings.Status_LoadLocalSavesFailed);
			}
		}
		finally
		{
			if (IsCurrentLifecycle(generation, expectedInstance))
			{
				IsLoadingSaves = false;
				loadTask = null;
				OnPropertyChanged("InstalledSummaryText");
				RaiseAvailabilityPropertyChanges();
				OnPropertyChanged("SaveEmptyMessage");
			}
		}
	}

	private async Task RefreshCachedSavesAsync(long generation)
	{
		if (selectedInstance == null)
		{
			return;
		}
		GameInstance expectedInstance = selectedInstance;
		long previousRevision = localSavesViewModel.Revision;
		try
		{
			if (await localSavesViewModel.RefreshIfInvalidatedAsync() && IsCurrentLifecycle(generation, expectedInstance) && localSavesViewModel.Revision != previousRevision)
			{
				hasPendingVisualRefresh = false;
				RefreshFromLocalSaves();
			}
		}
		catch (Exception exception)
		{
			if (IsCurrentLifecycle(generation, expectedInstance))
			{
				logger.LogError(exception, "Failed to silently refresh cached saves. InstanceId={InstanceId}", expectedInstance.Id);
				statusService.Report(Strings.Status_LoadLocalSavesFailed);
			}
		}
		finally
		{
			if (IsCurrentLifecycle(generation, expectedInstance))
			{
				loadTask = null;
			}
		}
	}

	private void RefreshSummary()
	{
		InstalledSaveCount = localSavesViewModel.CurrentSaves.Count;
	}

	private void LocalSavesViewModel_SavesChanged(object? sender, EventArgs e)
	{
		if (!suppressLocalCollectionEvents)
		{
			if (!HasLoadedSaves)
			{
				hasPendingVisualRefresh = true;
			}
			else if (!isSectionActive)
			{
				hasPendingVisualRefresh = true;
			}
			else
			{
				QueueVisibleRefresh();
			}
		}
	}

	private void RefreshFromLocalSaves()
	{
		string selectedFullPath = selectionState.LastSingleSelectedPath ?? SelectedSave?.FullPath;
		IReadOnlyList<SaveManagementSaveItemViewModel> visibleItems = StableFilteredItemProjection.Synchronize(localSavesViewModel.CurrentSaves, selectionState.ItemsByPath, (LocalSave save) => save.FullPath, (LocalSave save) => new SaveManagementSaveItemViewModel(save), delegate(SaveManagementSaveItemViewModel item, LocalSave save)
		{
			item.SyncFrom(save);
		}, MatchesSearch);
		selectionState.SyncSelectionToItems(visibleItems, IsMultiSelectMode);
		SetVisibleSaves(visibleItems);
		RefreshSummary();
		OnPropertyChanged("HasSaves");
		RaiseAvailabilityPropertyChanges();
		OnPropertyChanged("SaveEmptyMessage");
		OnPropertyChanged("AreAllVisibleSavesSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllSavesCommand.NotifyCanExecuteChanged();
		if (IsMultiSelectMode)
		{
			SelectedSave = null;
			UpdateSelectedSaveState();
			return;
		}
		SaveManagementSaveItemViewModel saveManagementSaveItemViewModel = Saves.FirstOrDefault((SaveManagementSaveItemViewModel save) => string.Equals(save.FullPath, selectedFullPath, StringComparison.OrdinalIgnoreCase));
		SelectSave(saveManagementSaveItemViewModel ?? Saves.FirstOrDefault());
	}

	private void QueueVisibleRefresh()
	{
		if (isVisibleRefreshQueued)
		{
			return;
		}
		isVisibleRefreshQueued = true;
		uiDispatcher.Post(delegate
		{
			isVisibleRefreshQueued = false;
			if (!isSectionActive)
			{
				hasPendingVisualRefresh = true;
			}
			else
			{
				hasPendingVisualRefresh = false;
				RefreshFromLocalSaves();
			}
		});
	}

	private void PublishReadyProjection()
	{
		hasPendingVisualRefresh = false;
		RefreshFromLocalSaves();
		SetInitialProjectionReady(value: true);
		ListEntranceAnimationToken++;
	}

	private void SetInitialProjectionReady(bool value)
	{
		if (isInitialProjectionReady != value)
		{
			isInitialProjectionReady = value;
			OnPropertyChanged("CanShowSaveScrollableContent");
		}
	}

	private bool MatchesSearch(LocalSave save)
	{
		if (string.IsNullOrWhiteSpace(SaveSearchQuery))
		{
			return true;
		}
		string query = SaveSearchQuery.Trim();
		if (!Contains(save.Name, query))
		{
			return Contains(save.DirectoryName, query);
		}
		return true;
	}

	private static bool Contains(string? source, string query)
	{
		if (!string.IsNullOrWhiteSpace(source))
		{
			return source.Contains(query, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private bool ShouldResetForInstanceReference(GameInstance? instance)
	{
		if (selectedInstance == null || instance == null)
		{
			if (selectedInstance == null)
			{
				return instance != null;
			}
			return true;
		}
		if (string.Equals(selectedInstance.Id, instance.Id, StringComparison.OrdinalIgnoreCase))
		{
			return !string.Equals(selectedInstance.InstanceDirectory, instance.InstanceDirectory, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private bool CanToggleSelectAllSaves()
	{
		if (IsMultiSelectMode)
		{
			return HasSaves;
		}
		return false;
	}

	private void RaiseAvailabilityPropertyChanges()
	{
		OnPropertyChanged("CanShowSaveInfoSection");
		OnPropertyChanged("CanShowSaveScrollableContent");
		OnPropertyChanged("HasInstalledSaves");
		OnPropertyChanged("CanShowSaveListSection");
		OnPropertyChanged("CanShowNoSavesEmptyState");
		OnPropertyChanged("CanShowSaveEmptyState");
		OnPropertyChanged("CanShowSaveLoadingState");
	}

	private void EnterMultiSelectMode()
	{
		SaveManagementSaveItemViewModel selectedItem = SelectedSave;
		IsMultiSelectMode = true;
		SelectedSave = null;
		selectionState.BeginMultiSelect(selectedItem, Saves);
		UpdateSelectedSaveState();
	}

	private void ExitMultiSelectMode()
	{
		IsMultiSelectMode = false;
		selectionState.ClearVisibleSelections(Saves);
		selectionState.ClearSelectedPaths();
		UpdateSelectedSaveState();
		SaveManagementSaveItemViewModel saveManagementSaveItemViewModel = Saves.FirstOrDefault((SaveManagementSaveItemViewModel save) => string.Equals(save.FullPath, selectionState.LastSingleSelectedPath, StringComparison.OrdinalIgnoreCase));
		SelectSave(saveManagementSaveItemViewModel ?? Saves.FirstOrDefault());
	}

	private void ResetSelectionState()
	{
		selectionState.Reset();
		IsMultiSelectMode = false;
		SelectedSave = null;
		SelectedSaveCount = 0;
	}

	private void ClearDisplayedSaves()
	{
		selectionState.ClearCache();
		SetVisibleSaves(Array.Empty<SaveManagementSaveItemViewModel>());
		RefreshVisibleSaveListItems();
		SelectedSave = null;
		RefreshSummary();
		OnPropertyChanged("HasSaves");
		RaiseAvailabilityPropertyChanges();
		OnPropertyChanged("SaveEmptyMessage");
		OnPropertyChanged("AreAllVisibleSavesSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllSavesCommand.NotifyCanExecuteChanged();
		UpdateSelectedSaveState();
	}

	private void ClearVisibleSelections()
	{
		selectionState.ClearVisibleSelections(Saves);
	}

	private IReadOnlyList<SaveManagementSaveItemViewModel> GetSelectedVisibleSaves()
	{
		return selectionState.GetSelectedVisibleItems(Saves);
	}

	private IReadOnlyList<LocalSave> ResolveLocalSaves(IEnumerable<string> fullPaths)
	{
		HashSet<string> pathSet = new HashSet<string>(fullPaths, StringComparer.OrdinalIgnoreCase);
		return localSavesViewModel.CurrentSaves.Where((LocalSave save) => pathSet.Contains(save.FullPath)).ToArray();
	}

	private void ReportBatchOperationResult(int totalCount, int failedCount, string successFormat, string partialFailureFormat)
	{
		if (failedCount <= 0)
		{
			statusService.Report(string.Format(successFormat, totalCount));
		}
		else
		{
			statusService.Report(string.Format(partialFailureFormat, totalCount - failedCount, failedCount));
		}
	}

	private void UpdateSelectedSaveState()
	{
		SelectedSaveCount = selectionState.CountSelectedVisibleItems(Saves);
	}

	private void SetVisibleSaves(IReadOnlyList<SaveManagementSaveItemViewModel> saves)
	{
		if (!LocalContentListPresentation.HasSameReferences(VisibleSaves, saves))
		{
			VisibleSaves = saves;
		}
	}

	private void RefreshVisibleSaveListItems()
	{
		IReadOnlyList<object> next = LocalContentListPresentation.CreateSectionedItems(VisibleSaves, SaveManagementInfoPanelItem.Instance, SaveManagementListSectionItem.Instance, CanShowSaveInfoSection);
		if (!LocalContentListPresentation.HasSameReferences(VisibleSaveListItems, next))
		{
			VisibleSaveListItems = next;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnInstalledSaveCountChanged(int value)
	{
		OnPropertyChanged("InstalledSummaryText");
		RaiseAvailabilityPropertyChanges();
		OnPropertyChanged("SaveEmptyMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSaveSearchQueryChanged(string value)
	{
		RefreshFromLocalSaves();
		OnPropertyChanged("SaveEmptyMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsMultiSelectModeChanged(bool value)
	{
		OnPropertyChanged("AreAllVisibleSavesSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllSavesCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedSaveCountChanged(int value)
	{
		OnPropertyChanged("HasSelectedSaves");
		OnPropertyChanged("AreAllVisibleSavesSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllSavesCommand.NotifyCanExecuteChanged();
		RequestDeleteSelectedSavesCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnVisibleSavesChanged(IReadOnlyList<SaveManagementSaveItemViewModel> value)
	{
		OnPropertyChanged("Saves");
		RefreshVisibleSaveListItems();
	}
}
