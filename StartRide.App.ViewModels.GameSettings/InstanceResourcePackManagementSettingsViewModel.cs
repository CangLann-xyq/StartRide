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

public sealed class InstanceResourcePackManagementSettingsViewModel : GameSettingsDetailsSectionViewModelBase
{
	private readonly LocalResourcePacksViewModel localResourcePacksViewModel;

	private readonly IStatusService statusService;

	private readonly IInstanceFolderService instanceFolderService;

	private readonly IFilePickerService filePickerService;

	private readonly IInstanceContentImportPathValidator importPathValidator;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger<InstanceResourcePackManagementSettingsViewModel> logger;

	private readonly LocalContentSelectionState<ResourcePackManagementItemViewModel> selectionState;

	private Task? loadTask;

	private GameInstance? selectedInstance;

	private bool hasPendingVisualRefresh;

	private bool isVisibleRefreshQueued;

	private bool isSectionActive;

	private bool isInitialProjectionReady;

	private bool suppressLocalCollectionEvents;

	private long lifecycleGeneration;

	[ObservableProperty]
	private int installedResourcePackCount;

	[ObservableProperty]
	private ResourcePackManagementItemViewModel? selectedResourcePack;

	[ObservableProperty]
	private string resourcePackSearchQuery = string.Empty;

	[ObservableProperty]
	private bool isMultiSelectMode;

	[ObservableProperty]
	private int selectedResourcePackCount;

	[ObservableProperty]
	private bool isLoadingResourcePacks;

	[ObservableProperty]
	private bool hasLoadedResourcePacks;

	[ObservableProperty]
	private IReadOnlyList<ResourcePackManagementItemViewModel> visibleResourcePacks = Array.Empty<ResourcePackManagementItemViewModel>();

	[ObservableProperty]
	private IReadOnlyList<object> visibleResourcePackListItems = Array.Empty<object>();

	[ObservableProperty]
	private int listEntranceAnimationToken;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ResourcePackManagementItemViewModel?>? openResourceDetailsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openResourcePackFolderCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? importLocalResourcePackCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? toggleMultiSelectModeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? selectAllResourcePacksCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestDeleteSelectedResourcePacksCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ResourcePackManagementItemViewModel?>? openResourcePackLocationCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ResourcePackManagementItemViewModel?>? requestDeleteResourcePackCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ResourcePackManagementItemViewModel?>? selectResourcePackCommand;

	public override bool UsesFullViewportLayout => true;

	public IReadOnlyList<ResourcePackManagementItemViewModel> ResourcePacks => VisibleResourcePacks;

	public bool CanShowResourcePackInfoSection => selectedInstance != null;

	public bool HasResourcePacks => ResourcePacks.Count > 0;

	public bool CanShowResourcePackScrollableContent
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

	public bool HasInstalledResourcePacks => InstalledResourcePackCount > 0;

	public bool CanShowResourcePackListSection
	{
		get
		{
			if (selectedInstance != null)
			{
				if (!IsLoadingResourcePacks)
				{
					return HasInstalledResourcePacks;
				}
				return true;
			}
			return false;
		}
	}

	public bool CanShowNoResourcePacksEmptyState
	{
		get
		{
			if (selectedInstance != null && HasLoadedResourcePacks && !IsLoadingResourcePacks)
			{
				return !HasInstalledResourcePacks;
			}
			return false;
		}
	}

	public bool CanShowResourcePackEmptyState
	{
		get
		{
			if (selectedInstance != null && HasLoadedResourcePacks && !IsLoadingResourcePacks && HasInstalledResourcePacks)
			{
				return !HasResourcePacks;
			}
			return false;
		}
	}

	public bool CanShowResourcePackLoadingState
	{
		get
		{
			if (selectedInstance != null && IsLoadingResourcePacks)
			{
				return !HasLoadedResourcePacks;
			}
			return false;
		}
	}

	public bool HasSelectedResourcePacks => SelectedResourcePackCount > 0;

	public bool AreAllVisibleResourcePacksSelected
	{
		get
		{
			if (HasResourcePacks)
			{
				return SelectedResourcePackCount == ResourcePacks.Count;
			}
			return false;
		}
	}

	public bool CanImportLocalResourcePack => selectedInstance != null;

	public string SelectAllButtonText
	{
		get
		{
			if (!AreAllVisibleResourcePacksSelected)
			{
				return Strings.GameSettings_ResourcePackManagementSelectAllButton;
			}
			return Strings.GameSettings_ResourcePackManagementCancelSelectAllButton;
		}
	}

	public string InstalledSummaryText
	{
		get
		{
			if (!IsLoadingResourcePacks || HasLoadedResourcePacks)
			{
				return string.Format(Strings.GameSettings_ResourcePackManagementInstalledSummaryFormat, InstalledResourcePackCount);
			}
			return Strings.GameSettings_ResourcePackManagementLoading;
		}
	}

	public string ResourcePackEmptyMessage
	{
		get
		{
			if (HasInstalledResourcePacks && !string.IsNullOrWhiteSpace(ResourcePackSearchQuery))
			{
				return Strings.GameSettings_ResourcePackManagementSearchEmptyMessage;
			}
			return Strings.GameSettings_ResourcePackManagementEmptyMessage;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int InstalledResourcePackCount
	{
		get
		{
			return installedResourcePackCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(installedResourcePackCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InstalledResourcePackCount);
				installedResourcePackCount = value;
				OnInstalledResourcePackCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InstalledResourcePackCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcePackManagementItemViewModel? SelectedResourcePack
	{
		get
		{
			return selectedResourcePack;
		}
		set
		{
			if (!EqualityComparer<ResourcePackManagementItemViewModel>.Default.Equals(selectedResourcePack, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedResourcePack);
				selectedResourcePack = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedResourcePack);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ResourcePackSearchQuery
	{
		get
		{
			return resourcePackSearchQuery;
		}
		[MemberNotNull("resourcePackSearchQuery")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(resourcePackSearchQuery, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ResourcePackSearchQuery);
				resourcePackSearchQuery = value;
				OnResourcePackSearchQueryChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ResourcePackSearchQuery);
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
	public int SelectedResourcePackCount
	{
		get
		{
			return selectedResourcePackCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(selectedResourcePackCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedResourcePackCount);
				selectedResourcePackCount = value;
				OnSelectedResourcePackCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedResourcePackCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLoadingResourcePacks
	{
		get
		{
			return isLoadingResourcePacks;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLoadingResourcePacks, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLoadingResourcePacks);
				isLoadingResourcePacks = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLoadingResourcePacks);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasLoadedResourcePacks
	{
		get
		{
			return hasLoadedResourcePacks;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(hasLoadedResourcePacks, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasLoadedResourcePacks);
				hasLoadedResourcePacks = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasLoadedResourcePacks);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<ResourcePackManagementItemViewModel> VisibleResourcePacks
	{
		get
		{
			return visibleResourcePacks;
		}
		[MemberNotNull("visibleResourcePacks")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<ResourcePackManagementItemViewModel>>.Default.Equals(visibleResourcePacks, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VisibleResourcePacks);
				visibleResourcePacks = value;
				OnVisibleResourcePacksChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VisibleResourcePacks);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<object> VisibleResourcePackListItems
	{
		get
		{
			return visibleResourcePackListItems;
		}
		[MemberNotNull("visibleResourcePackListItems")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<object>>.Default.Equals(visibleResourcePackListItems, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VisibleResourcePackListItems);
				visibleResourcePackListItems = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VisibleResourcePackListItems);
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
	public IRelayCommand<ResourcePackManagementItemViewModel?> OpenResourceDetailsCommand => openResourceDetailsCommand ?? (openResourceDetailsCommand = new RelayCommand<ResourcePackManagementItemViewModel>(OpenResourceDetails));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenResourcePackFolderCommand => openResourcePackFolderCommand ?? (openResourcePackFolderCommand = new RelayCommand(OpenResourcePackFolder));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ImportLocalResourcePackCommand => importLocalResourcePackCommand ?? (importLocalResourcePackCommand = new AsyncRelayCommand(ImportLocalResourcePackAsync, () => CanImportLocalResourcePack));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ToggleMultiSelectModeCommand => toggleMultiSelectModeCommand ?? (toggleMultiSelectModeCommand = new RelayCommand(ToggleMultiSelectMode));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand SelectAllResourcePacksCommand => selectAllResourcePacksCommand ?? (selectAllResourcePacksCommand = new RelayCommand(SelectAllResourcePacks, CanToggleSelectAllResourcePacks));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestDeleteSelectedResourcePacksCommand => requestDeleteSelectedResourcePacksCommand ?? (requestDeleteSelectedResourcePacksCommand = new RelayCommand(RequestDeleteSelectedResourcePacks, () => HasSelectedResourcePacks));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ResourcePackManagementItemViewModel?> OpenResourcePackLocationCommand => openResourcePackLocationCommand ?? (openResourcePackLocationCommand = new RelayCommand<ResourcePackManagementItemViewModel>(OpenResourcePackLocation));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ResourcePackManagementItemViewModel?> RequestDeleteResourcePackCommand => requestDeleteResourcePackCommand ?? (requestDeleteResourcePackCommand = new RelayCommand<ResourcePackManagementItemViewModel>(RequestDeleteResourcePack));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ResourcePackManagementItemViewModel?> SelectResourcePackCommand => selectResourcePackCommand ?? (selectResourcePackCommand = new RelayCommand<ResourcePackManagementItemViewModel>(SelectResourcePack));

	public event Action<ResourcePackDeleteRequest>? DeleteResourcePacksRequested;

	public event Action<ResourcePackImportFailureRequest>? ResourcePackImportFailedRequested;

	public event Action<ResourceProjectReference>? ResourceDetailsRequested;

	[RelayCommand]
	private void OpenResourceDetails(ResourcePackManagementItemViewModel? resourcePack)
	{
		ResourceProjectReference resourceProjectReference = resourcePack?.ProjectReference;
		if ((object)resourceProjectReference != null)
		{
			ResourceDetailsRequested?.Invoke(resourceProjectReference);
		}
	}

	[RelayCommand]
	private void OpenResourcePackFolder()
	{
		if (selectedInstance == null)
		{
			return;
		}
		try
		{
			string text = instanceFolderService.EnsureDirectoryExists(Path.Combine(selectedInstance.InstanceDirectory, "resourcepacks"));
			logger.LogDebug("Opening resource pack folder. InstanceId={InstanceId} ResourcePacksDirectory={ResourcePacksDirectory}", selectedInstance.Id, text);
			if (!instanceFolderService.TryOpen(text))
			{
				logger.LogWarning("Failed to open resource pack folder. InstanceId={InstanceId} ResourcePacksDirectory={ResourcePacksDirectory}", selectedInstance.Id, text);
				statusService.Report(Strings.Status_OpenLocalResourcePackFolderFailed);
			}
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to prepare resource pack folder for opening. InstanceId={InstanceId}", selectedInstance.Id);
			statusService.Report(Strings.Status_OpenLocalResourcePackFolderFailed);
		}
	}

	[RelayCommand(CanExecute = "CanImportLocalResourcePack")]
	private async Task ImportLocalResourcePackAsync()
	{
		if (selectedInstance != null)
		{
			string text = filePickerService.PickResourcePackArchive();
			if (!string.IsNullOrWhiteSpace(text))
			{
				await ImportResourcePackArchivesAsync(new global::_003C_003Ez__ReadOnlySingleElementList<string>(text), ImportTriggerSource.FilePicker);
			}
		}
	}

	public GameSettingsFileDropEvaluation EvaluateDroppedFiles(IReadOnlyList<string> paths)
	{
		if (!TryValidateImportPaths(paths, Strings.GameSettings_DropResourcePackArchivesOnlyMessage, out string failureMessage))
		{
			return GameSettingsFileDropEvaluation.Reject(failureMessage);
		}
		return GameSettingsFileDropEvaluation.Accept(Strings.GameSettings_DropImportResourcePacksMessage);
	}

	public Task ImportDroppedResourcePackArchivesAsync(IReadOnlyList<string> paths)
	{
		return ImportResourcePackArchivesAsync(paths, ImportTriggerSource.DragDrop);
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

	[RelayCommand(CanExecute = "CanToggleSelectAllResourcePacks")]
	private void SelectAllResourcePacks()
	{
		if (AreAllVisibleResourcePacksSelected)
		{
			selectionState.ClearVisibleSelections(ResourcePacks);
			selectionState.ClearSelectedPaths();
			SelectedResourcePack = null;
			UpdateSelectedResourcePackState();
		}
		else
		{
			selectionState.SelectAll(ResourcePacks);
			SelectedResourcePack = null;
			UpdateSelectedResourcePackState();
		}
	}

	[RelayCommand(CanExecute = "HasSelectedResourcePacks")]
	private void RequestDeleteSelectedResourcePacks()
	{
		IReadOnlyList<ResourcePackManagementItemViewModel> selectedVisibleResourcePacks = GetSelectedVisibleResourcePacks();
		if (selectedVisibleResourcePacks.Count != 0)
		{
			DeleteResourcePacksRequested?.Invoke(new ResourcePackDeleteRequest(selectedVisibleResourcePacks.Select((ResourcePackManagementItemViewModel resourcePack) => resourcePack.FullPath).ToArray(), selectedVisibleResourcePacks.Select((ResourcePackManagementItemViewModel resourcePack) => resourcePack.Title).ToArray()));
		}
	}

	[RelayCommand]
	private void OpenResourcePackLocation(ResourcePackManagementItemViewModel? resourcePack)
	{
		if (resourcePack == null)
		{
			return;
		}
		try
		{
			if (!instanceFolderService.TryRevealFile(resourcePack.FullPath))
			{
				logger.LogWarning("Failed to reveal local resource pack file. InstanceId={InstanceId} Path={Path}", selectedInstance?.Id ?? "<none>", resourcePack.FullPath);
				statusService.Report(Strings.Status_OpenLocalResourcePackLocationFailed);
			}
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to reveal local resource pack file. InstanceId={InstanceId} Path={Path}", selectedInstance?.Id ?? "<none>", resourcePack.FullPath);
			statusService.Report(Strings.Status_OpenLocalResourcePackLocationFailed);
		}
	}

	[RelayCommand]
	private void RequestDeleteResourcePack(ResourcePackManagementItemViewModel? resourcePack)
	{
		if (resourcePack != null)
		{
			DeleteResourcePacksRequested?.Invoke(new ResourcePackDeleteRequest(new global::_003C_003Ez__ReadOnlySingleElementList<string>(resourcePack.FullPath), new global::_003C_003Ez__ReadOnlySingleElementList<string>(resourcePack.Title)));
		}
	}

	[RelayCommand]
	private void SelectResourcePack(ResourcePackManagementItemViewModel? resourcePack)
	{
		if (resourcePack == null)
		{
			SelectedResourcePack = null;
			if (IsMultiSelectMode)
			{
				selectionState.ClearSelectedPaths();
			}
			selectionState.ClearVisibleSelections(ResourcePacks);
			UpdateSelectedResourcePackState();
		}
		else if (IsMultiSelectMode)
		{
			selectionState.ToggleSelection(resourcePack);
			SelectedResourcePack = null;
			UpdateSelectedResourcePackState();
		}
		else
		{
			SelectedResourcePack = resourcePack;
			selectionState.SelectSingle(resourcePack, ResourcePacks);
		}
	}

	public async Task DeleteResourcePacksAsync(IReadOnlyList<string> fullPaths)
	{
		ArgumentNullException.ThrowIfNull(fullPaths, "fullPaths");
		IReadOnlyList<LocalResourcePack> resourcePacksToDelete = ResolveLocalResourcePacks(fullPaths);
		if (resourcePacksToDelete.Count == 0)
		{
			ExitMultiSelectMode();
			return;
		}
		logger.LogInformation("Deleting selected resource packs. InstanceId={InstanceId} Count={Count}", selectedInstance?.Id ?? "<none>", resourcePacksToDelete.Count);
		try
		{
			int failedCount = await localResourcePacksViewModel.DeleteResourcePacksAsync(resourcePacksToDelete);
			ExitMultiSelectMode();
			ReportBatchOperationResult(resourcePacksToDelete.Count, failedCount, Strings.Status_SelectedResourcePacksDeletedFormat, Strings.Status_SelectedResourcePacksDeletePartialFailedFormat);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to delete selected resource packs. InstanceId={InstanceId}", selectedInstance?.Id ?? "<none>");
			statusService.Report(Strings.Status_SelectedResourcePacksDeleteFailed);
		}
	}

	public InstanceResourcePackManagementSettingsViewModel(GameSettingsDetailsViewModel parent, LocalResourcePacksViewModel localResourcePacksViewModel, IStatusService statusService, IInstanceFolderService instanceFolderService, IFilePickerService filePickerService, IInstanceContentImportPathValidator importPathValidator, IUiDispatcher? uiDispatcher = null, ILogger<InstanceResourcePackManagementSettingsViewModel>? logger = null)
		: base(parent)
	{
		this.localResourcePacksViewModel = localResourcePacksViewModel;
		this.statusService = statusService;
		this.instanceFolderService = instanceFolderService;
		this.filePickerService = filePickerService;
		this.importPathValidator = importPathValidator;
		this.uiDispatcher = uiDispatcher ?? ImmediateUiDispatcher.Instance;
		this.logger = logger ?? NullLogger<InstanceResourcePackManagementSettingsViewModel>.Instance;
		selectionState = new LocalContentSelectionState<ResourcePackManagementItemViewModel>((ResourcePackManagementItemViewModel resourcePack) => resourcePack.FullPath, (ResourcePackManagementItemViewModel resourcePack) => resourcePack.IsSelected, delegate(ResourcePackManagementItemViewModel resourcePack, bool isSelected)
		{
			resourcePack.IsSelected = isSelected;
		});
		this.localResourcePacksViewModel.ResourcePacksChanged += LocalResourcePacksViewModel_ResourcePacksChanged;
		this.localResourcePacksViewModel.IconChanged += LocalResourcePacksViewModel_IconChanged;
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
			localResourcePacksViewModel.SetSelectedInstance(instance);
			localResourcePacksViewModel.SetSectionActive(isSectionActive && selectedInstance != null);
		}
		finally
		{
			suppressLocalCollectionEvents = false;
		}
		IsLoadingResourcePacks = false;
		HasLoadedResourcePacks = false;
		selectionState.ClearCache();
		SetInitialProjectionReady(value: false);
		ResetSelectionState();
		ClearDisplayedResourcePacks();
		ImportLocalResourcePackCommand.NotifyCanExecuteChanged();
	}

	public bool RefreshSelectedInstanceReference(GameInstance? instance)
	{
		if (ShouldResetForInstanceReference(instance))
		{
			OnSelectedInstanceChanged(instance);
			return true;
		}
		selectedInstance = instance;
		ImportLocalResourcePackCommand.NotifyCanExecuteChanged();
		return false;
	}

	public override void OnSectionDeactivated()
	{
		if (isSectionActive)
		{
			isSectionActive = false;
			Interlocked.Increment(ref lifecycleGeneration);
			loadTask = null;
			IsLoadingResourcePacks = false;
			localResourcePacksViewModel.SetSectionActive(active: false);
		}
	}

	public void ReleaseLocalObservation()
	{
		isSectionActive = false;
		Interlocked.Increment(ref lifecycleGeneration);
		loadTask = null;
		IsLoadingResourcePacks = false;
		localResourcePacksViewModel.ReleaseObservation();
	}

	public void SuspendLocalWatchersForInstanceRename()
	{
		localResourcePacksViewModel.SuspendWatcherForInstanceRename();
	}

	public void ResumeLocalWatchersAfterInstanceRename(bool restart = true)
	{
		if (restart && isSectionActive)
		{
			Interlocked.Increment(ref lifecycleGeneration);
			loadTask = null;
			localResourcePacksViewModel.InvalidateSnapshot();
		}
		localResourcePacksViewModel.ResumeWatcherAfterInstanceRename(restart);
	}

	public override Task OnSectionActivatedAsync()
	{
		if (!isSectionActive)
		{
			isSectionActive = true;
			Interlocked.Increment(ref lifecycleGeneration);
			loadTask = null;
			localResourcePacksViewModel.SetSectionActive(selectedInstance != null);
		}
		if (selectedInstance == null)
		{
			return Task.CompletedTask;
		}
		if (HasLoadedResourcePacks)
		{
			if (hasPendingVisualRefresh)
			{
				hasPendingVisualRefresh = false;
				RefreshFromLocalResourcePacks();
			}
			loadTask = RefreshCachedResourcePacksAsync(Volatile.Read(in lifecycleGeneration));
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
		if (HasLoadedResourcePacks)
		{
			return Task.CompletedTask;
		}
		loadTask = LoadResourcePacksAsync(Volatile.Read(in lifecycleGeneration));
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

	private async Task ImportResourcePackArchivesAsync(IReadOnlyList<string> archivePaths, ImportTriggerSource source)
	{
		if (selectedInstance == null)
		{
			return;
		}
		if (!TryValidateImportPaths(archivePaths, Strings.GameSettings_DropResourcePackArchivesOnlyMessage, out string failureMessage))
		{
			if (source == ImportTriggerSource.DragDrop)
			{
				statusService.Report(failureMessage);
			}
			else
			{
				ResourcePackImportFailedRequested?.Invoke(new ResourcePackImportFailureRequest(Strings.Dialog_UnsupportedResourcePackArchiveMessage));
			}
			return;
		}
		logger.LogInformation("Starting local resource pack import batch. InstanceId={InstanceId} Source={Source} FileCount={FileCount}", selectedInstance.Id, source, archivePaths.Count);
		LocalContentImportBatchResult<LocalResourcePackImportResult> localContentImportBatchResult = await LocalContentImportBatchCoordinator.ExecuteAsync(archivePaths, async delegate(string archivePath)
		{
			logger.LogDebug("Importing local resource pack archive. InstanceId={InstanceId} ArchivePath={ArchivePath}", selectedInstance.Id, archivePath);
			return await localResourcePacksViewModel.ImportResourcePackAsync(archivePath, reportStatus: false);
		}, (LocalResourcePackImportResult result) => result.IsSuccess);
		if (localContentImportBatchResult.Failure != null)
		{
			switch (localContentImportBatchResult.Failure.FailureReason)
			{
			case LocalResourcePackImportFailureReason.UnsupportedArchive:
				ResourcePackImportFailedRequested?.Invoke(new ResourcePackImportFailureRequest(Strings.Dialog_UnsupportedResourcePackArchiveMessage));
				break;
			case LocalResourcePackImportFailureReason.FileNotFound:
				statusService.Report(Strings.Status_LocalResourcePackImportFileNotFound);
				break;
			case LocalResourcePackImportFailureReason.UnexpectedError:
				logger.LogWarning("Local resource pack import failed unexpectedly after service call. InstanceId={InstanceId} ArchivePath={ArchivePath}", selectedInstance.Id, localContentImportBatchResult.FailedPath);
				statusService.Report(Strings.Status_LocalResourcePackImportFailed);
				break;
			}
		}
		else
		{
			if (localContentImportBatchResult.SuccessCount > 0)
			{
				statusService.Report((localContentImportBatchResult.SuccessCount == 1) ? Strings.Status_LocalResourcePackImported : string.Format(Strings.Status_LocalResourcePacksImportedFormat, localContentImportBatchResult.SuccessCount));
			}
			logger.LogInformation("Local resource pack import batch completed. InstanceId={InstanceId} RequestedCount={RequestedCount} ImportedCount={ImportedCount}", selectedInstance.Id, archivePaths.Count, localContentImportBatchResult.SuccessCount);
		}
	}

	private bool TryValidateImportPaths(IReadOnlyList<string> paths, string invalidTypeMessage, out string failureMessage)
	{
		return LocalContentImportPathEvaluator.TryValidate(importPathValidator, paths, InstanceContentImportKind.ResourcePack, invalidTypeMessage, out failureMessage);
	}

	private async Task LoadResourcePacksAsync(long generation)
	{
		if (selectedInstance == null)
		{
			return;
		}
		GameInstance expectedInstance = selectedInstance;
		SetInitialProjectionReady(value: false);
		IsLoadingResourcePacks = true;
		OnPropertyChanged("InstalledSummaryText");
		RaiseAvailabilityPropertyChanges();
		try
		{
			if (await localResourcePacksViewModel.RefreshResourcePacksAsync() && IsCurrentLifecycle(generation, expectedInstance))
			{
				HasLoadedResourcePacks = true;
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
				logger.LogError(exception, "Failed to load resource packs for section activation. InstanceId={InstanceId}", expectedInstance.Id);
				HasLoadedResourcePacks = false;
				ClearDisplayedResourcePacks();
				hasPendingVisualRefresh = false;
				SetInitialProjectionReady(value: true);
				statusService.Report(Strings.Status_LoadLocalResourcePacksFailed);
			}
		}
		finally
		{
			if (IsCurrentLifecycle(generation, expectedInstance))
			{
				IsLoadingResourcePacks = false;
				loadTask = null;
				OnPropertyChanged("InstalledSummaryText");
				RaiseAvailabilityPropertyChanges();
				OnPropertyChanged("ResourcePackEmptyMessage");
			}
		}
	}

	private async Task RefreshCachedResourcePacksAsync(long generation)
	{
		if (selectedInstance == null)
		{
			return;
		}
		GameInstance expectedInstance = selectedInstance;
		long previousRevision = localResourcePacksViewModel.Revision;
		try
		{
			if (await localResourcePacksViewModel.RefreshIfInvalidatedAsync() && IsCurrentLifecycle(generation, expectedInstance) && localResourcePacksViewModel.Revision != previousRevision)
			{
				hasPendingVisualRefresh = false;
				RefreshFromLocalResourcePacks();
			}
		}
		catch (Exception exception)
		{
			if (IsCurrentLifecycle(generation, expectedInstance))
			{
				logger.LogError(exception, "Failed to silently refresh cached resource packs. InstanceId={InstanceId}", expectedInstance.Id);
				statusService.Report(Strings.Status_LoadLocalResourcePacksFailed);
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
		InstalledResourcePackCount = localResourcePacksViewModel.CurrentResourcePacks.Count;
	}

	private void LocalResourcePacksViewModel_ResourcePacksChanged(object? sender, EventArgs e)
	{
		if (!suppressLocalCollectionEvents)
		{
			if (!HasLoadedResourcePacks)
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

	private void LocalResourcePacksViewModel_IconChanged(object? sender, LocalContentIconChangedEventArgs e)
	{
		if (HasLoadedResourcePacks && isSectionActive && selectionState.ItemsByPath.TryGetValue(e.FullPath, out ResourcePackManagementItemViewModel value) && !string.Equals(value.IconSource, e.IconSource, StringComparison.Ordinal))
		{
			value.IconSource = e.IconSource;
		}
	}

	private void RefreshFromLocalResourcePacks()
	{
		string selectedFullPath = selectionState.LastSingleSelectedPath ?? SelectedResourcePack?.FullPath;
		IReadOnlyList<ResourcePackManagementItemViewModel> visibleItems = StableFilteredItemProjection.Synchronize(localResourcePacksViewModel.CurrentResourcePacks, selectionState.ItemsByPath, (LocalResourcePack resourcePack) => resourcePack.FullPath, (LocalResourcePack resourcePack) => new ResourcePackManagementItemViewModel(resourcePack), delegate(ResourcePackManagementItemViewModel item, LocalResourcePack resourcePack)
		{
			item.SyncFrom(resourcePack);
		}, MatchesSearch);
		selectionState.SyncSelectionToItems(visibleItems, IsMultiSelectMode);
		SetVisibleResourcePacks(visibleItems);
		RefreshSummary();
		OnPropertyChanged("HasResourcePacks");
		RaiseAvailabilityPropertyChanges();
		OnPropertyChanged("ResourcePackEmptyMessage");
		OnPropertyChanged("AreAllVisibleResourcePacksSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllResourcePacksCommand.NotifyCanExecuteChanged();
		if (IsMultiSelectMode)
		{
			SelectedResourcePack = null;
			UpdateSelectedResourcePackState();
			return;
		}
		ResourcePackManagementItemViewModel resourcePackManagementItemViewModel = ResourcePacks.FirstOrDefault((ResourcePackManagementItemViewModel resourcePack) => string.Equals(resourcePack.FullPath, selectedFullPath, StringComparison.OrdinalIgnoreCase));
		SelectResourcePack(resourcePackManagementItemViewModel ?? ResourcePacks.FirstOrDefault());
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
				RefreshFromLocalResourcePacks();
			}
		});
	}

	private void PublishReadyProjection()
	{
		hasPendingVisualRefresh = false;
		RefreshFromLocalResourcePacks();
		SetInitialProjectionReady(value: true);
		ListEntranceAnimationToken++;
	}

	private void SetInitialProjectionReady(bool value)
	{
		if (isInitialProjectionReady != value)
		{
			isInitialProjectionReady = value;
			OnPropertyChanged("CanShowResourcePackScrollableContent");
		}
	}

	private bool MatchesSearch(LocalResourcePack resourcePack)
	{
		if (string.IsNullOrWhiteSpace(ResourcePackSearchQuery))
		{
			return true;
		}
		string query = ResourcePackSearchQuery.Trim();
		if (!Contains(resourcePack.Name, query))
		{
			return Contains(resourcePack.FileName, query);
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

	private bool CanToggleSelectAllResourcePacks()
	{
		if (IsMultiSelectMode)
		{
			return HasResourcePacks;
		}
		return false;
	}

	private void RaiseAvailabilityPropertyChanges()
	{
		OnPropertyChanged("CanShowResourcePackInfoSection");
		OnPropertyChanged("CanShowResourcePackScrollableContent");
		OnPropertyChanged("HasInstalledResourcePacks");
		OnPropertyChanged("CanShowResourcePackListSection");
		OnPropertyChanged("CanShowNoResourcePacksEmptyState");
		OnPropertyChanged("CanShowResourcePackEmptyState");
		OnPropertyChanged("CanShowResourcePackLoadingState");
	}

	private void EnterMultiSelectMode()
	{
		ResourcePackManagementItemViewModel selectedItem = SelectedResourcePack;
		IsMultiSelectMode = true;
		SelectedResourcePack = null;
		selectionState.BeginMultiSelect(selectedItem, ResourcePacks);
		UpdateSelectedResourcePackState();
	}

	private void ExitMultiSelectMode()
	{
		IsMultiSelectMode = false;
		selectionState.ClearVisibleSelections(ResourcePacks);
		selectionState.ClearSelectedPaths();
		UpdateSelectedResourcePackState();
		ResourcePackManagementItemViewModel resourcePackManagementItemViewModel = ResourcePacks.FirstOrDefault((ResourcePackManagementItemViewModel resourcePack) => string.Equals(resourcePack.FullPath, selectionState.LastSingleSelectedPath, StringComparison.OrdinalIgnoreCase));
		SelectResourcePack(resourcePackManagementItemViewModel ?? ResourcePacks.FirstOrDefault());
	}

	private void ResetSelectionState()
	{
		selectionState.Reset();
		IsMultiSelectMode = false;
		SelectedResourcePack = null;
		SelectedResourcePackCount = 0;
	}

	private void ClearDisplayedResourcePacks()
	{
		selectionState.ClearCache();
		SetVisibleResourcePacks(Array.Empty<ResourcePackManagementItemViewModel>());
		RefreshVisibleResourcePackListItems();
		SelectedResourcePack = null;
		RefreshSummary();
		OnPropertyChanged("HasResourcePacks");
		RaiseAvailabilityPropertyChanges();
		OnPropertyChanged("ResourcePackEmptyMessage");
		OnPropertyChanged("AreAllVisibleResourcePacksSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllResourcePacksCommand.NotifyCanExecuteChanged();
		UpdateSelectedResourcePackState();
	}

	private void ClearVisibleSelections()
	{
		selectionState.ClearVisibleSelections(ResourcePacks);
	}

	private IReadOnlyList<ResourcePackManagementItemViewModel> GetSelectedVisibleResourcePacks()
	{
		return selectionState.GetSelectedVisibleItems(ResourcePacks);
	}

	private IReadOnlyList<LocalResourcePack> ResolveLocalResourcePacks(IEnumerable<string> fullPaths)
	{
		HashSet<string> pathSet = new HashSet<string>(fullPaths, StringComparer.OrdinalIgnoreCase);
		return localResourcePacksViewModel.CurrentResourcePacks.Where((LocalResourcePack resourcePack) => pathSet.Contains(resourcePack.FullPath)).ToArray();
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

	private void UpdateSelectedResourcePackState()
	{
		SelectedResourcePackCount = selectionState.CountSelectedVisibleItems(ResourcePacks);
	}

	private void SetVisibleResourcePacks(IReadOnlyList<ResourcePackManagementItemViewModel> resourcePacks)
	{
		if (!LocalContentListPresentation.HasSameReferences(VisibleResourcePacks, resourcePacks))
		{
			VisibleResourcePacks = resourcePacks;
		}
	}

	private void RefreshVisibleResourcePackListItems()
	{
		IReadOnlyList<object> next = LocalContentListPresentation.CreateSectionedItems(VisibleResourcePacks, ResourcePackManagementInfoPanelItem.Instance, ResourcePackManagementListSectionItem.Instance, CanShowResourcePackInfoSection);
		if (!LocalContentListPresentation.HasSameReferences(VisibleResourcePackListItems, next))
		{
			VisibleResourcePackListItems = next;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnInstalledResourcePackCountChanged(int value)
	{
		OnPropertyChanged("InstalledSummaryText");
		RaiseAvailabilityPropertyChanges();
		OnPropertyChanged("ResourcePackEmptyMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnResourcePackSearchQueryChanged(string value)
	{
		RefreshFromLocalResourcePacks();
		OnPropertyChanged("ResourcePackEmptyMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsMultiSelectModeChanged(bool value)
	{
		OnPropertyChanged("AreAllVisibleResourcePacksSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllResourcePacksCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedResourcePackCountChanged(int value)
	{
		OnPropertyChanged("HasSelectedResourcePacks");
		OnPropertyChanged("AreAllVisibleResourcePacksSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllResourcePacksCommand.NotifyCanExecuteChanged();
		RequestDeleteSelectedResourcePacksCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnVisibleResourcePacksChanged(IReadOnlyList<ResourcePackManagementItemViewModel> value)
	{
		OnPropertyChanged("ResourcePacks");
		RefreshVisibleResourcePackListItems();
	}
}
