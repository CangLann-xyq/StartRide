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
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Shared;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class InstanceShaderPackManagementSettingsViewModel : GameSettingsDetailsSectionViewModelBase
{
	private readonly LocalShaderPacksViewModel localShaderPacksViewModel;

	private readonly IStatusService statusService;

	private readonly IInstanceFolderService instanceFolderService;

	private readonly IFilePickerService filePickerService;

	private readonly IInstanceContentImportPathValidator importPathValidator;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger<InstanceShaderPackManagementSettingsViewModel> logger;

	private readonly LocalContentSelectionState<ShaderPackManagementItemViewModel> selectionState;

	private Task? loadTask;

	private GameInstance? selectedInstance;

	private bool hasPendingVisualRefresh;

	private bool isVisibleRefreshQueued;

	private bool isSectionActive;

	private bool isInitialProjectionReady;

	private bool suppressLocalCollectionEvents;

	private long lifecycleGeneration;

	[ObservableProperty]
	private int installedShaderPackCount;

	[ObservableProperty]
	private ShaderPackManagementItemViewModel? selectedShaderPack;

	[ObservableProperty]
	private string shaderPackSearchQuery = string.Empty;

	[ObservableProperty]
	private bool isMultiSelectMode;

	[ObservableProperty]
	private int selectedShaderPackCount;

	[ObservableProperty]
	private bool isLoadingShaderPacks;

	[ObservableProperty]
	private bool hasLoadedShaderPacks;

	[ObservableProperty]
	private IReadOnlyList<ShaderPackManagementItemViewModel> visibleShaderPacks = Array.Empty<ShaderPackManagementItemViewModel>();

	[ObservableProperty]
	private IReadOnlyList<object> visibleShaderPackListItems = Array.Empty<object>();

	[ObservableProperty]
	private int listEntranceAnimationToken;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ShaderPackManagementItemViewModel?>? openResourceDetailsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openShaderPackFolderCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? importLocalShaderPackCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? toggleMultiSelectModeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? selectAllShaderPacksCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestDeleteSelectedShaderPacksCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ShaderPackManagementItemViewModel?>? openShaderPackLocationCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ShaderPackManagementItemViewModel?>? requestDeleteShaderPackCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ShaderPackManagementItemViewModel?>? selectShaderPackCommand;

	public override bool UsesFullViewportLayout => true;

	public IReadOnlyList<ShaderPackManagementItemViewModel> ShaderPacks => VisibleShaderPacks;

	public bool CanShowShaderPackInfoSection => selectedInstance != null;

	public bool HasShaderPacks => ShaderPacks.Count > 0;

	public bool CanShowShaderPackScrollableContent
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

	public bool HasInstalledShaderPacks => InstalledShaderPackCount > 0;

	public bool CanShowShaderPackListSection
	{
		get
		{
			if (selectedInstance != null)
			{
				if (!IsLoadingShaderPacks)
				{
					return HasInstalledShaderPacks;
				}
				return true;
			}
			return false;
		}
	}

	public bool CanShowNoShaderPacksEmptyState
	{
		get
		{
			if (selectedInstance != null && HasLoadedShaderPacks && !IsLoadingShaderPacks)
			{
				return !HasInstalledShaderPacks;
			}
			return false;
		}
	}

	public bool CanShowShaderPackEmptyState
	{
		get
		{
			if (selectedInstance != null && HasLoadedShaderPacks && !IsLoadingShaderPacks && HasInstalledShaderPacks)
			{
				return !HasShaderPacks;
			}
			return false;
		}
	}

	public bool CanShowShaderPackLoadingState
	{
		get
		{
			if (selectedInstance != null && IsLoadingShaderPacks)
			{
				return !HasLoadedShaderPacks;
			}
			return false;
		}
	}

	public bool HasSelectedShaderPacks => SelectedShaderPackCount > 0;

	public bool AreAllVisibleShaderPacksSelected
	{
		get
		{
			if (HasShaderPacks)
			{
				return SelectedShaderPackCount == ShaderPacks.Count;
			}
			return false;
		}
	}

	public bool CanImportLocalShaderPack => selectedInstance != null;

	public string SelectAllButtonText
	{
		get
		{
			if (!AreAllVisibleShaderPacksSelected)
			{
				return Strings.GameSettings_ShaderPackManagementSelectAllButton;
			}
			return Strings.GameSettings_ShaderPackManagementCancelSelectAllButton;
		}
	}

	public string InstalledSummaryText
	{
		get
		{
			if (!IsLoadingShaderPacks || HasLoadedShaderPacks)
			{
				return string.Format(Strings.GameSettings_ShaderPackManagementInstalledSummaryFormat, InstalledShaderPackCount);
			}
			return Strings.GameSettings_ShaderPackManagementLoading;
		}
	}

	public string ShaderPackEmptyMessage
	{
		get
		{
			if (HasInstalledShaderPacks && !string.IsNullOrWhiteSpace(ShaderPackSearchQuery))
			{
				return Strings.GameSettings_ShaderPackManagementSearchEmptyMessage;
			}
			return Strings.GameSettings_ShaderPackManagementEmptyMessage;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int InstalledShaderPackCount
	{
		get
		{
			return installedShaderPackCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(installedShaderPackCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InstalledShaderPackCount);
				installedShaderPackCount = value;
				OnInstalledShaderPackCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InstalledShaderPackCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ShaderPackManagementItemViewModel? SelectedShaderPack
	{
		get
		{
			return selectedShaderPack;
		}
		set
		{
			if (!EqualityComparer<ShaderPackManagementItemViewModel>.Default.Equals(selectedShaderPack, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedShaderPack);
				selectedShaderPack = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedShaderPack);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ShaderPackSearchQuery
	{
		get
		{
			return shaderPackSearchQuery;
		}
		[MemberNotNull("shaderPackSearchQuery")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(shaderPackSearchQuery, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ShaderPackSearchQuery);
				shaderPackSearchQuery = value;
				OnShaderPackSearchQueryChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ShaderPackSearchQuery);
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
	public int SelectedShaderPackCount
	{
		get
		{
			return selectedShaderPackCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(selectedShaderPackCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedShaderPackCount);
				selectedShaderPackCount = value;
				OnSelectedShaderPackCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedShaderPackCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLoadingShaderPacks
	{
		get
		{
			return isLoadingShaderPacks;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLoadingShaderPacks, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLoadingShaderPacks);
				isLoadingShaderPacks = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLoadingShaderPacks);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasLoadedShaderPacks
	{
		get
		{
			return hasLoadedShaderPacks;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(hasLoadedShaderPacks, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasLoadedShaderPacks);
				hasLoadedShaderPacks = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasLoadedShaderPacks);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<ShaderPackManagementItemViewModel> VisibleShaderPacks
	{
		get
		{
			return visibleShaderPacks;
		}
		[MemberNotNull("visibleShaderPacks")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<ShaderPackManagementItemViewModel>>.Default.Equals(visibleShaderPacks, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VisibleShaderPacks);
				visibleShaderPacks = value;
				OnVisibleShaderPacksChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VisibleShaderPacks);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<object> VisibleShaderPackListItems
	{
		get
		{
			return visibleShaderPackListItems;
		}
		[MemberNotNull("visibleShaderPackListItems")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<object>>.Default.Equals(visibleShaderPackListItems, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VisibleShaderPackListItems);
				visibleShaderPackListItems = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VisibleShaderPackListItems);
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
	public IRelayCommand<ShaderPackManagementItemViewModel?> OpenResourceDetailsCommand => openResourceDetailsCommand ?? (openResourceDetailsCommand = new RelayCommand<ShaderPackManagementItemViewModel>(OpenResourceDetails));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenShaderPackFolderCommand => openShaderPackFolderCommand ?? (openShaderPackFolderCommand = new RelayCommand(OpenShaderPackFolder));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ImportLocalShaderPackCommand => importLocalShaderPackCommand ?? (importLocalShaderPackCommand = new AsyncRelayCommand(ImportLocalShaderPackAsync, () => CanImportLocalShaderPack));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ToggleMultiSelectModeCommand => toggleMultiSelectModeCommand ?? (toggleMultiSelectModeCommand = new RelayCommand(ToggleMultiSelectMode));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand SelectAllShaderPacksCommand => selectAllShaderPacksCommand ?? (selectAllShaderPacksCommand = new RelayCommand(SelectAllShaderPacks, CanToggleSelectAllShaderPacks));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestDeleteSelectedShaderPacksCommand => requestDeleteSelectedShaderPacksCommand ?? (requestDeleteSelectedShaderPacksCommand = new RelayCommand(RequestDeleteSelectedShaderPacks, () => HasSelectedShaderPacks));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ShaderPackManagementItemViewModel?> OpenShaderPackLocationCommand => openShaderPackLocationCommand ?? (openShaderPackLocationCommand = new RelayCommand<ShaderPackManagementItemViewModel>(OpenShaderPackLocation));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ShaderPackManagementItemViewModel?> RequestDeleteShaderPackCommand => requestDeleteShaderPackCommand ?? (requestDeleteShaderPackCommand = new RelayCommand<ShaderPackManagementItemViewModel>(RequestDeleteShaderPack));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ShaderPackManagementItemViewModel?> SelectShaderPackCommand => selectShaderPackCommand ?? (selectShaderPackCommand = new RelayCommand<ShaderPackManagementItemViewModel>(SelectShaderPack));

	public event Action<ShaderPackDeleteRequest>? DeleteShaderPacksRequested;

	public event Action<ShaderPackImportFailureRequest>? ShaderPackImportFailedRequested;

	public event Action<ResourceProjectReference>? ResourceDetailsRequested;

	[RelayCommand]
	private void OpenResourceDetails(ShaderPackManagementItemViewModel? shaderPack)
	{
		ResourceProjectReference resourceProjectReference = shaderPack?.ProjectReference;
		if ((object)resourceProjectReference != null)
		{
			ResourceDetailsRequested?.Invoke(resourceProjectReference);
		}
	}

	[RelayCommand]
	private void OpenShaderPackFolder()
	{
		if (selectedInstance == null)
		{
			return;
		}
		try
		{
			string text = instanceFolderService.EnsureDirectoryExists(Path.Combine(selectedInstance.InstanceDirectory, "shaderpacks"));
			logger.LogDebug("Opening shader pack folder. InstanceId={InstanceId} ShaderPacksDirectory={ShaderPacksDirectory}", selectedInstance.Id, text);
			if (!instanceFolderService.TryOpen(text))
			{
				logger.LogWarning("Failed to open shader pack folder. InstanceId={InstanceId} ShaderPacksDirectory={ShaderPacksDirectory}", selectedInstance.Id, text);
				statusService.Report(Strings.Status_OpenLocalShaderPackFolderFailed);
			}
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to prepare shader pack folder for opening. InstanceId={InstanceId}", selectedInstance.Id);
			statusService.Report(Strings.Status_OpenLocalShaderPackFolderFailed);
		}
	}

	[RelayCommand(CanExecute = "CanImportLocalShaderPack")]
	private async Task ImportLocalShaderPackAsync()
	{
		if (selectedInstance != null)
		{
			string text = filePickerService.PickShaderPackArchive();
			if (!string.IsNullOrWhiteSpace(text))
			{
				await ImportShaderPackArchivesAsync(new global::_003C_003Ez__ReadOnlySingleElementList<string>(text), ImportTriggerSource.FilePicker);
			}
		}
	}

	public GameSettingsFileDropEvaluation EvaluateDroppedFiles(IReadOnlyList<string> paths)
	{
		if (!TryValidateImportPaths(paths, Strings.GameSettings_DropShaderPackArchivesOnlyMessage, out string failureMessage))
		{
			return GameSettingsFileDropEvaluation.Reject(failureMessage);
		}
		return GameSettingsFileDropEvaluation.Accept(Strings.GameSettings_DropImportShaderPacksMessage);
	}

	public Task ImportDroppedShaderPackArchivesAsync(IReadOnlyList<string> paths)
	{
		return ImportShaderPackArchivesAsync(paths, ImportTriggerSource.DragDrop);
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

	[RelayCommand(CanExecute = "CanToggleSelectAllShaderPacks")]
	private void SelectAllShaderPacks()
	{
		if (AreAllVisibleShaderPacksSelected)
		{
			selectionState.ClearVisibleSelections(ShaderPacks);
			selectionState.ClearSelectedPaths();
			SelectedShaderPack = null;
			UpdateSelectedShaderPackState();
		}
		else
		{
			selectionState.SelectAll(ShaderPacks);
			SelectedShaderPack = null;
			UpdateSelectedShaderPackState();
		}
	}

	[RelayCommand(CanExecute = "HasSelectedShaderPacks")]
	private void RequestDeleteSelectedShaderPacks()
	{
		IReadOnlyList<ShaderPackManagementItemViewModel> selectedVisibleShaderPacks = GetSelectedVisibleShaderPacks();
		if (selectedVisibleShaderPacks.Count != 0)
		{
			DeleteShaderPacksRequested?.Invoke(new ShaderPackDeleteRequest(selectedVisibleShaderPacks.Select((ShaderPackManagementItemViewModel shaderPack) => shaderPack.FullPath).ToArray(), selectedVisibleShaderPacks.Select((ShaderPackManagementItemViewModel shaderPack) => shaderPack.Title).ToArray()));
		}
	}

	[RelayCommand]
	private void OpenShaderPackLocation(ShaderPackManagementItemViewModel? shaderPack)
	{
		if (shaderPack == null)
		{
			return;
		}
		try
		{
			if (!instanceFolderService.TryRevealFile(shaderPack.FullPath))
			{
				logger.LogWarning("Failed to reveal local shader pack file. InstanceId={InstanceId} Path={Path}", selectedInstance?.Id ?? "<none>", shaderPack.FullPath);
				statusService.Report(Strings.Status_OpenLocalShaderPackLocationFailed);
			}
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to reveal local shader pack file. InstanceId={InstanceId} Path={Path}", selectedInstance?.Id ?? "<none>", shaderPack.FullPath);
			statusService.Report(Strings.Status_OpenLocalShaderPackLocationFailed);
		}
	}

	[RelayCommand]
	private void RequestDeleteShaderPack(ShaderPackManagementItemViewModel? shaderPack)
	{
		if (shaderPack != null)
		{
			DeleteShaderPacksRequested?.Invoke(new ShaderPackDeleteRequest(new global::_003C_003Ez__ReadOnlySingleElementList<string>(shaderPack.FullPath), new global::_003C_003Ez__ReadOnlySingleElementList<string>(shaderPack.Title)));
		}
	}

	[RelayCommand]
	private void SelectShaderPack(ShaderPackManagementItemViewModel? shaderPack)
	{
		if (shaderPack == null)
		{
			SelectedShaderPack = null;
			if (IsMultiSelectMode)
			{
				selectionState.ClearSelectedPaths();
			}
			selectionState.ClearVisibleSelections(ShaderPacks);
			UpdateSelectedShaderPackState();
		}
		else if (IsMultiSelectMode)
		{
			selectionState.ToggleSelection(shaderPack);
			SelectedShaderPack = null;
			UpdateSelectedShaderPackState();
		}
		else
		{
			SelectedShaderPack = shaderPack;
			selectionState.SelectSingle(shaderPack, ShaderPacks);
		}
	}

	public async Task DeleteShaderPacksAsync(IReadOnlyList<string> fullPaths)
	{
		ArgumentNullException.ThrowIfNull(fullPaths, "fullPaths");
		IReadOnlyList<LocalShaderPack> shaderPacksToDelete = ResolveLocalShaderPacks(fullPaths);
		if (shaderPacksToDelete.Count == 0)
		{
			ExitMultiSelectMode();
			return;
		}
		logger.LogInformation("Deleting selected shader packs. InstanceId={InstanceId} Count={Count}", selectedInstance?.Id ?? "<none>", shaderPacksToDelete.Count);
		try
		{
			int failedCount = await localShaderPacksViewModel.DeleteShaderPacksAsync(shaderPacksToDelete);
			ExitMultiSelectMode();
			ReportBatchOperationResult(shaderPacksToDelete.Count, failedCount, Strings.Status_SelectedShaderPacksDeletedFormat, Strings.Status_SelectedShaderPacksDeletePartialFailedFormat);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to delete selected shader packs. InstanceId={InstanceId}", selectedInstance?.Id ?? "<none>");
			statusService.Report(Strings.Status_SelectedShaderPacksDeleteFailed);
		}
	}

	public InstanceShaderPackManagementSettingsViewModel(GameSettingsDetailsViewModel parent, LocalShaderPacksViewModel localShaderPacksViewModel, IStatusService statusService, IInstanceFolderService instanceFolderService, IFilePickerService filePickerService, IInstanceContentImportPathValidator importPathValidator, IUiDispatcher? uiDispatcher = null, ILogger<InstanceShaderPackManagementSettingsViewModel>? logger = null)
		: base(parent)
	{
		this.localShaderPacksViewModel = localShaderPacksViewModel;
		this.statusService = statusService;
		this.instanceFolderService = instanceFolderService;
		this.filePickerService = filePickerService;
		this.importPathValidator = importPathValidator;
		this.uiDispatcher = uiDispatcher ?? ImmediateUiDispatcher.Instance;
		this.logger = logger ?? NullLogger<InstanceShaderPackManagementSettingsViewModel>.Instance;
		selectionState = new LocalContentSelectionState<ShaderPackManagementItemViewModel>((ShaderPackManagementItemViewModel shaderPack) => shaderPack.FullPath, (ShaderPackManagementItemViewModel shaderPack) => shaderPack.IsSelected, delegate(ShaderPackManagementItemViewModel shaderPack, bool isSelected)
		{
			shaderPack.IsSelected = isSelected;
		});
		this.localShaderPacksViewModel.ShaderPacksChanged += LocalShaderPacksViewModel_ShaderPacksChanged;
		this.localShaderPacksViewModel.IconChanged += LocalShaderPacksViewModel_IconChanged;
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
			localShaderPacksViewModel.SetSelectedInstance(instance);
			localShaderPacksViewModel.SetSectionActive(isSectionActive && selectedInstance != null);
		}
		finally
		{
			suppressLocalCollectionEvents = false;
		}
		IsLoadingShaderPacks = false;
		HasLoadedShaderPacks = false;
		selectionState.ClearCache();
		SetInitialProjectionReady(value: false);
		ResetSelectionState();
		ClearDisplayedShaderPacks();
		ImportLocalShaderPackCommand.NotifyCanExecuteChanged();
	}

	public bool RefreshSelectedInstanceReference(GameInstance? instance)
	{
		if (ShouldResetForInstanceReference(instance))
		{
			OnSelectedInstanceChanged(instance);
			return true;
		}
		selectedInstance = instance;
		ImportLocalShaderPackCommand.NotifyCanExecuteChanged();
		return false;
	}

	public override void OnSectionDeactivated()
	{
		if (isSectionActive)
		{
			isSectionActive = false;
			Interlocked.Increment(ref lifecycleGeneration);
			loadTask = null;
			IsLoadingShaderPacks = false;
			localShaderPacksViewModel.SetSectionActive(active: false);
		}
	}

	public void ReleaseLocalObservation()
	{
		isSectionActive = false;
		Interlocked.Increment(ref lifecycleGeneration);
		loadTask = null;
		IsLoadingShaderPacks = false;
		localShaderPacksViewModel.ReleaseObservation();
	}

	public void SuspendLocalWatchersForInstanceRename()
	{
		localShaderPacksViewModel.SuspendWatcherForInstanceRename();
	}

	public void ResumeLocalWatchersAfterInstanceRename(bool restart = true)
	{
		if (restart && isSectionActive)
		{
			Interlocked.Increment(ref lifecycleGeneration);
			loadTask = null;
			localShaderPacksViewModel.InvalidateSnapshot();
		}
		localShaderPacksViewModel.ResumeWatcherAfterInstanceRename(restart);
	}

	public override Task OnSectionActivatedAsync()
	{
		if (!isSectionActive)
		{
			isSectionActive = true;
			Interlocked.Increment(ref lifecycleGeneration);
			loadTask = null;
			localShaderPacksViewModel.SetSectionActive(selectedInstance != null);
		}
		if (selectedInstance == null)
		{
			return Task.CompletedTask;
		}
		if (HasLoadedShaderPacks)
		{
			if (hasPendingVisualRefresh)
			{
				hasPendingVisualRefresh = false;
				RefreshFromLocalShaderPacks();
			}
			loadTask = RefreshCachedShaderPacksAsync(Volatile.Read(in lifecycleGeneration));
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
		if (HasLoadedShaderPacks)
		{
			return Task.CompletedTask;
		}
		loadTask = LoadShaderPacksAsync(Volatile.Read(in lifecycleGeneration));
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

	private async Task ImportShaderPackArchivesAsync(IReadOnlyList<string> archivePaths, ImportTriggerSource source)
	{
		if (selectedInstance == null)
		{
			return;
		}
		if (!TryValidateImportPaths(archivePaths, Strings.GameSettings_DropShaderPackArchivesOnlyMessage, out string failureMessage))
		{
			if (source == ImportTriggerSource.DragDrop)
			{
				statusService.Report(failureMessage);
			}
			else
			{
				ShaderPackImportFailedRequested?.Invoke(new ShaderPackImportFailureRequest(Strings.Dialog_UnsupportedShaderPackArchiveMessage));
			}
			return;
		}
		logger.LogInformation("Starting local shader pack import batch. InstanceId={InstanceId} Source={Source} FileCount={FileCount}", selectedInstance.Id, source, archivePaths.Count);
		LocalContentImportBatchResult<LocalShaderPackImportResult> localContentImportBatchResult = await LocalContentImportBatchCoordinator.ExecuteAsync(archivePaths, async delegate(string archivePath)
		{
			logger.LogDebug("Importing local shader pack archive. InstanceId={InstanceId} ArchivePath={ArchivePath}", selectedInstance.Id, archivePath);
			return await localShaderPacksViewModel.ImportShaderPackAsync(archivePath, reportStatus: false);
		}, (LocalShaderPackImportResult result) => result.IsSuccess);
		if (localContentImportBatchResult.Failure != null)
		{
			switch (localContentImportBatchResult.Failure.FailureReason)
			{
			case LocalShaderPackImportFailureReason.UnsupportedArchive:
				ShaderPackImportFailedRequested?.Invoke(new ShaderPackImportFailureRequest(Strings.Dialog_UnsupportedShaderPackArchiveMessage));
				break;
			case LocalShaderPackImportFailureReason.FileNotFound:
				statusService.Report(Strings.Status_LocalShaderPackImportFileNotFound);
				break;
			case LocalShaderPackImportFailureReason.UnexpectedError:
				logger.LogWarning("Local shader pack import failed unexpectedly after service call. InstanceId={InstanceId} ArchivePath={ArchivePath}", selectedInstance.Id, localContentImportBatchResult.FailedPath);
				statusService.Report(Strings.Status_LocalShaderPackImportFailed);
				break;
			}
		}
		else
		{
			if (localContentImportBatchResult.SuccessCount > 0)
			{
				statusService.Report((localContentImportBatchResult.SuccessCount == 1) ? Strings.Status_LocalShaderPackImported : string.Format(Strings.Status_LocalShaderPacksImportedFormat, localContentImportBatchResult.SuccessCount));
			}
			logger.LogInformation("Local shader pack import batch completed. InstanceId={InstanceId} RequestedCount={RequestedCount} ImportedCount={ImportedCount}", selectedInstance.Id, archivePaths.Count, localContentImportBatchResult.SuccessCount);
		}
	}

	private bool TryValidateImportPaths(IReadOnlyList<string> paths, string invalidTypeMessage, out string failureMessage)
	{
		return LocalContentImportPathEvaluator.TryValidate(importPathValidator, paths, InstanceContentImportKind.ShaderPack, invalidTypeMessage, out failureMessage);
	}

	private async Task LoadShaderPacksAsync(long generation)
	{
		if (selectedInstance == null)
		{
			return;
		}
		GameInstance expectedInstance = selectedInstance;
		SetInitialProjectionReady(value: false);
		IsLoadingShaderPacks = true;
		OnPropertyChanged("InstalledSummaryText");
		RaiseAvailabilityPropertyChanges();
		try
		{
			if (await localShaderPacksViewModel.RefreshShaderPacksAsync() && IsCurrentLifecycle(generation, expectedInstance))
			{
				HasLoadedShaderPacks = true;
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
				logger.LogError(exception, "Failed to load shader packs for section activation. InstanceId={InstanceId}", expectedInstance.Id);
				HasLoadedShaderPacks = false;
				ClearDisplayedShaderPacks();
				hasPendingVisualRefresh = false;
				SetInitialProjectionReady(value: true);
				statusService.Report(Strings.Status_LoadLocalShaderPacksFailed);
			}
		}
		finally
		{
			if (IsCurrentLifecycle(generation, expectedInstance))
			{
				IsLoadingShaderPacks = false;
				loadTask = null;
				OnPropertyChanged("InstalledSummaryText");
				RaiseAvailabilityPropertyChanges();
				OnPropertyChanged("ShaderPackEmptyMessage");
			}
		}
	}

	private async Task RefreshCachedShaderPacksAsync(long generation)
	{
		if (selectedInstance == null)
		{
			return;
		}
		GameInstance expectedInstance = selectedInstance;
		long previousRevision = localShaderPacksViewModel.Revision;
		try
		{
			if (await localShaderPacksViewModel.RefreshIfInvalidatedAsync() && IsCurrentLifecycle(generation, expectedInstance) && localShaderPacksViewModel.Revision != previousRevision)
			{
				hasPendingVisualRefresh = false;
				RefreshFromLocalShaderPacks();
			}
		}
		catch (Exception exception)
		{
			if (IsCurrentLifecycle(generation, expectedInstance))
			{
				logger.LogError(exception, "Failed to silently refresh cached shader packs. InstanceId={InstanceId}", expectedInstance.Id);
				statusService.Report(Strings.Status_LoadLocalShaderPacksFailed);
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
		InstalledShaderPackCount = localShaderPacksViewModel.CurrentShaderPacks.Count;
	}

	private void LocalShaderPacksViewModel_ShaderPacksChanged(object? sender, EventArgs e)
	{
		if (!suppressLocalCollectionEvents)
		{
			if (!HasLoadedShaderPacks)
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

	private void LocalShaderPacksViewModel_IconChanged(object? sender, LocalContentIconChangedEventArgs e)
	{
		if (HasLoadedShaderPacks && isSectionActive && selectionState.ItemsByPath.TryGetValue(e.FullPath, out ShaderPackManagementItemViewModel value) && !string.Equals(value.IconSource, e.IconSource, StringComparison.Ordinal))
		{
			value.IconSource = e.IconSource;
		}
	}

	private void RefreshFromLocalShaderPacks()
	{
		string selectedFullPath = selectionState.LastSingleSelectedPath ?? SelectedShaderPack?.FullPath;
		IReadOnlyList<ShaderPackManagementItemViewModel> visibleItems = StableFilteredItemProjection.Synchronize(localShaderPacksViewModel.CurrentShaderPacks, selectionState.ItemsByPath, (LocalShaderPack shaderPack) => shaderPack.FullPath, (LocalShaderPack shaderPack) => new ShaderPackManagementItemViewModel(shaderPack), delegate(ShaderPackManagementItemViewModel item, LocalShaderPack shaderPack)
		{
			item.SyncFrom(shaderPack);
		}, MatchesSearch);
		selectionState.SyncSelectionToItems(visibleItems, IsMultiSelectMode);
		SetVisibleShaderPacks(visibleItems);
		RefreshSummary();
		OnPropertyChanged("HasShaderPacks");
		RaiseAvailabilityPropertyChanges();
		OnPropertyChanged("ShaderPackEmptyMessage");
		OnPropertyChanged("AreAllVisibleShaderPacksSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllShaderPacksCommand.NotifyCanExecuteChanged();
		if (IsMultiSelectMode)
		{
			SelectedShaderPack = null;
			UpdateSelectedShaderPackState();
			return;
		}
		ShaderPackManagementItemViewModel shaderPackManagementItemViewModel = ShaderPacks.FirstOrDefault((ShaderPackManagementItemViewModel shaderPack) => string.Equals(shaderPack.FullPath, selectedFullPath, StringComparison.OrdinalIgnoreCase));
		SelectShaderPack(shaderPackManagementItemViewModel ?? ShaderPacks.FirstOrDefault());
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
				RefreshFromLocalShaderPacks();
			}
		});
	}

	private void PublishReadyProjection()
	{
		hasPendingVisualRefresh = false;
		RefreshFromLocalShaderPacks();
		SetInitialProjectionReady(value: true);
		ListEntranceAnimationToken++;
	}

	private void SetInitialProjectionReady(bool value)
	{
		if (isInitialProjectionReady != value)
		{
			isInitialProjectionReady = value;
			OnPropertyChanged("CanShowShaderPackScrollableContent");
		}
	}

	private bool MatchesSearch(LocalShaderPack shaderPack)
	{
		if (string.IsNullOrWhiteSpace(ShaderPackSearchQuery))
		{
			return true;
		}
		string query = ShaderPackSearchQuery.Trim();
		if (!Contains(shaderPack.Name, query))
		{
			return Contains(shaderPack.FileName, query);
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

	private bool CanToggleSelectAllShaderPacks()
	{
		if (IsMultiSelectMode)
		{
			return HasShaderPacks;
		}
		return false;
	}

	private void RaiseAvailabilityPropertyChanges()
	{
		OnPropertyChanged("CanShowShaderPackInfoSection");
		OnPropertyChanged("CanShowShaderPackScrollableContent");
		OnPropertyChanged("HasInstalledShaderPacks");
		OnPropertyChanged("CanShowShaderPackListSection");
		OnPropertyChanged("CanShowNoShaderPacksEmptyState");
		OnPropertyChanged("CanShowShaderPackEmptyState");
		OnPropertyChanged("CanShowShaderPackLoadingState");
	}

	private void EnterMultiSelectMode()
	{
		ShaderPackManagementItemViewModel selectedItem = SelectedShaderPack;
		IsMultiSelectMode = true;
		SelectedShaderPack = null;
		selectionState.BeginMultiSelect(selectedItem, ShaderPacks);
		UpdateSelectedShaderPackState();
	}

	private void ExitMultiSelectMode()
	{
		IsMultiSelectMode = false;
		selectionState.ClearVisibleSelections(ShaderPacks);
		selectionState.ClearSelectedPaths();
		UpdateSelectedShaderPackState();
		ShaderPackManagementItemViewModel shaderPackManagementItemViewModel = ShaderPacks.FirstOrDefault((ShaderPackManagementItemViewModel shaderPack) => string.Equals(shaderPack.FullPath, selectionState.LastSingleSelectedPath, StringComparison.OrdinalIgnoreCase));
		SelectShaderPack(shaderPackManagementItemViewModel ?? ShaderPacks.FirstOrDefault());
	}

	private void ResetSelectionState()
	{
		selectionState.Reset();
		IsMultiSelectMode = false;
		SelectedShaderPack = null;
		SelectedShaderPackCount = 0;
	}

	private void ClearDisplayedShaderPacks()
	{
		selectionState.ClearCache();
		SetVisibleShaderPacks(Array.Empty<ShaderPackManagementItemViewModel>());
		RefreshVisibleShaderPackListItems();
		SelectedShaderPack = null;
		RefreshSummary();
		OnPropertyChanged("HasShaderPacks");
		RaiseAvailabilityPropertyChanges();
		OnPropertyChanged("ShaderPackEmptyMessage");
		OnPropertyChanged("AreAllVisibleShaderPacksSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllShaderPacksCommand.NotifyCanExecuteChanged();
		UpdateSelectedShaderPackState();
	}

	private void ClearVisibleSelections()
	{
		selectionState.ClearVisibleSelections(ShaderPacks);
	}

	private IReadOnlyList<ShaderPackManagementItemViewModel> GetSelectedVisibleShaderPacks()
	{
		return selectionState.GetSelectedVisibleItems(ShaderPacks);
	}

	private IReadOnlyList<LocalShaderPack> ResolveLocalShaderPacks(IEnumerable<string> fullPaths)
	{
		HashSet<string> pathSet = new HashSet<string>(fullPaths, StringComparer.OrdinalIgnoreCase);
		return localShaderPacksViewModel.CurrentShaderPacks.Where((LocalShaderPack shaderPack) => pathSet.Contains(shaderPack.FullPath)).ToArray();
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

	private void UpdateSelectedShaderPackState()
	{
		SelectedShaderPackCount = selectionState.CountSelectedVisibleItems(ShaderPacks);
	}

	private void SetVisibleShaderPacks(IReadOnlyList<ShaderPackManagementItemViewModel> shaderPacks)
	{
		if (!LocalContentListPresentation.HasSameReferences(VisibleShaderPacks, shaderPacks))
		{
			VisibleShaderPacks = shaderPacks;
		}
	}

	private void RefreshVisibleShaderPackListItems()
	{
		IReadOnlyList<object> next = LocalContentListPresentation.CreateSectionedItems(VisibleShaderPacks, ShaderPackManagementInfoPanelItem.Instance, ShaderPackManagementListSectionItem.Instance, CanShowShaderPackInfoSection);
		if (!LocalContentListPresentation.HasSameReferences(VisibleShaderPackListItems, next))
		{
			VisibleShaderPackListItems = next;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnInstalledShaderPackCountChanged(int value)
	{
		OnPropertyChanged("InstalledSummaryText");
		RaiseAvailabilityPropertyChanges();
		OnPropertyChanged("ShaderPackEmptyMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnShaderPackSearchQueryChanged(string value)
	{
		RefreshFromLocalShaderPacks();
		OnPropertyChanged("ShaderPackEmptyMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsMultiSelectModeChanged(bool value)
	{
		OnPropertyChanged("AreAllVisibleShaderPacksSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllShaderPacksCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedShaderPackCountChanged(int value)
	{
		OnPropertyChanged("HasSelectedShaderPacks");
		OnPropertyChanged("AreAllVisibleShaderPacksSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllShaderPacksCommand.NotifyCanExecuteChanged();
		RequestDeleteSelectedShaderPacksCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnVisibleShaderPacksChanged(IReadOnlyList<ShaderPackManagementItemViewModel> value)
	{
		OnPropertyChanged("ShaderPacks");
		RefreshVisibleShaderPackListItems();
	}
}
