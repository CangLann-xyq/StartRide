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

public sealed class InstanceModManagementSettingsViewModel : GameSettingsDetailsSectionViewModelBase
{
	private readonly LocalModsViewModel localModsViewModel;

	private readonly IStatusService statusService;

	private readonly IInstanceFolderService instanceFolderService;

	private readonly IFilePickerService filePickerService;

	private readonly IInstanceContentImportPathValidator importPathValidator;

	private readonly IUiDispatcher uiDispatcher;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly ILogger<InstanceModManagementSettingsViewModel> logger;

	private readonly Dictionary<string, ModManagementModItemViewModel> allModsByProjectionPath = new Dictionary<string, ModManagementModItemViewModel>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, ModManagementModItemViewModel> allModsByFullPath = new Dictionary<string, ModManagementModItemViewModel>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> selectedModPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private TaskCompletionSource<bool>? pendingImportConflictResolutionSource;

	private Task? loadTask;

	private GameInstance? selectedInstance;

	private string? lastSingleSelectedModPath;

	private bool hasPendingVisualRefresh;

	private bool isVisibleRefreshQueued;

	private bool isSectionActive;

	private bool isInitialProjectionReady;

	private bool suppressLocalCollectionEvents;

	private long lifecycleGeneration;

	[ObservableProperty]
	private int installedModCount;

	[ObservableProperty]
	private int enabledModCount;

	[ObservableProperty]
	private ModManagementModItemViewModel? selectedMod;

	[ObservableProperty]
	private string modSearchQuery = string.Empty;

	[ObservableProperty]
	private ModManagementFilter modFilter;

	[ObservableProperty]
	private bool isMultiSelectMode;

	[ObservableProperty]
	private int selectedModCount;

	[ObservableProperty]
	private bool isLoadingMods;

	[ObservableProperty]
	private bool hasLoadedMods;

	[ObservableProperty]
	private IReadOnlyList<ModManagementModItemViewModel> visibleMods = Array.Empty<ModManagementModItemViewModel>();

	[ObservableProperty]
	private IReadOnlyList<object> visibleModListItems = Array.Empty<object>();

	[ObservableProperty]
	private int listEntranceAnimationToken;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ModManagementModItemViewModel?>? openResourceDetailsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? installOnlineModCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ModManagementFilter>? setModFilterCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? toggleMultiSelectModeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? selectAllModsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? enableSelectedModsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? disableSelectedModsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestDeleteSelectedModsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<ModManagementModItemViewModel?>? toggleModEnabledCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ModManagementModItemViewModel?>? openModFileLocationCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ModManagementModItemViewModel?>? requestDeleteModCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ModManagementModItemViewModel?>? selectModCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openModFolderCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? importLocalModCommand;

	public override bool UsesFullViewportLayout => true;

	public IReadOnlyList<ModManagementModItemViewModel> Mods => VisibleMods;

	public bool IsModManagementSupported
	{
		get
		{
			LoaderKind? loaderKind = selectedInstance?.Loader;
			return !loaderKind.HasValue || loaderKind.GetValueOrDefault() != LoaderKind.Vanilla;
		}
	}

	public bool CanShowModInfoSection => IsModManagementSupported;

	public bool HasMods => Mods.Count > 0;

	public bool CanShowModScrollableContent
	{
		get
		{
			if (IsModManagementSupported)
			{
				return isInitialProjectionReady;
			}
			return false;
		}
	}

	public bool HasInstalledMods => InstalledModCount > 0;

	public bool CanShowModListSection
	{
		get
		{
			if (IsModManagementSupported)
			{
				if (!IsLoadingMods)
				{
					return HasInstalledMods;
				}
				return true;
			}
			return false;
		}
	}

	public bool CanShowNoModsEmptyState
	{
		get
		{
			if (IsModManagementSupported && HasLoadedMods && !IsLoadingMods)
			{
				return !HasInstalledMods;
			}
			return false;
		}
	}

	public bool CanShowModEmptyState
	{
		get
		{
			if (IsModManagementSupported && HasLoadedMods && !IsLoadingMods && HasInstalledMods)
			{
				return !HasMods;
			}
			return false;
		}
	}

	public bool CanShowModUnavailableState => !IsModManagementSupported;

	public bool CanShowModLoadingState
	{
		get
		{
			if (IsModManagementSupported && IsLoadingMods)
			{
				return !HasLoadedMods;
			}
			return false;
		}
	}

	public bool HasSelectedMods => SelectedModCount > 0;

	public bool AreAllVisibleModsSelected
	{
		get
		{
			if (HasMods)
			{
				return SelectedModCount == Mods.Count;
			}
			return false;
		}
	}

	public string SelectAllButtonText
	{
		get
		{
			if (!AreAllVisibleModsSelected)
			{
				return Strings.GameSettings_ModManagementSelectAllButton;
			}
			return Strings.GameSettings_ModManagementCancelSelectAllButton;
		}
	}

	public string InstalledSummaryText
	{
		get
		{
			if (!IsLoadingMods || HasLoadedMods)
			{
				return string.Format(Strings.GameSettings_ModManagementInstalledSummaryFormat, InstalledModCount, EnabledModCount);
			}
			return Strings.GameSettings_ModManagementLoading;
		}
	}

	public string ModEmptyMessage
	{
		get
		{
			if (HasInstalledMods && !string.IsNullOrWhiteSpace(ModSearchQuery))
			{
				return Strings.GameSettings_ModManagementSearchEmptyMessage;
			}
			return Strings.GameSettings_ModManagementEmptyMessage;
		}
	}

	public string ModUnavailableMessage => Strings.GameSettings_ModManagementUnavailableMessage;

	public bool IsAllModsFilterSelected => ModFilter == ModManagementFilter.All;

	public bool IsEnabledModsFilterSelected => ModFilter == ModManagementFilter.Enabled;

	public bool IsDisabledModsFilterSelected => ModFilter == ModManagementFilter.Disabled;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int InstalledModCount
	{
		get
		{
			return installedModCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(installedModCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InstalledModCount);
				installedModCount = value;
				OnInstalledModCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InstalledModCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int EnabledModCount
	{
		get
		{
			return enabledModCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(enabledModCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EnabledModCount);
				enabledModCount = value;
				OnEnabledModCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EnabledModCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ModManagementModItemViewModel? SelectedMod
	{
		get
		{
			return selectedMod;
		}
		set
		{
			if (!EqualityComparer<ModManagementModItemViewModel>.Default.Equals(selectedMod, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedMod);
				selectedMod = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedMod);
			}
		}
	}

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
				OnModSearchQueryChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ModSearchQuery);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ModManagementFilter ModFilter
	{
		get
		{
			return modFilter;
		}
		set
		{
			if (!EqualityComparer<ModManagementFilter>.Default.Equals(modFilter, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ModFilter);
				modFilter = value;
				OnModFilterChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ModFilter);
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
	public int SelectedModCount
	{
		get
		{
			return selectedModCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(selectedModCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedModCount);
				selectedModCount = value;
				OnSelectedModCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedModCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLoadingMods
	{
		get
		{
			return isLoadingMods;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLoadingMods, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLoadingMods);
				isLoadingMods = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLoadingMods);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasLoadedMods
	{
		get
		{
			return hasLoadedMods;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(hasLoadedMods, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasLoadedMods);
				hasLoadedMods = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasLoadedMods);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<ModManagementModItemViewModel> VisibleMods
	{
		get
		{
			return visibleMods;
		}
		[MemberNotNull("visibleMods")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<ModManagementModItemViewModel>>.Default.Equals(visibleMods, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VisibleMods);
				visibleMods = value;
				OnVisibleModsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VisibleMods);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<object> VisibleModListItems
	{
		get
		{
			return visibleModListItems;
		}
		[MemberNotNull("visibleModListItems")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<object>>.Default.Equals(visibleModListItems, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VisibleModListItems);
				visibleModListItems = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VisibleModListItems);
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
	public IRelayCommand<ModManagementModItemViewModel?> OpenResourceDetailsCommand => openResourceDetailsCommand ?? (openResourceDetailsCommand = new RelayCommand<ModManagementModItemViewModel>(OpenResourceDetails));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand InstallOnlineModCommand => installOnlineModCommand ?? (installOnlineModCommand = new RelayCommand(InstallOnlineMod));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ModManagementFilter> SetModFilterCommand => setModFilterCommand ?? (setModFilterCommand = new RelayCommand<ModManagementFilter>(SetModFilter));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ToggleMultiSelectModeCommand => toggleMultiSelectModeCommand ?? (toggleMultiSelectModeCommand = new RelayCommand(ToggleMultiSelectMode));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand SelectAllModsCommand => selectAllModsCommand ?? (selectAllModsCommand = new RelayCommand(SelectAllMods, CanToggleSelectAllMods));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand EnableSelectedModsCommand => enableSelectedModsCommand ?? (enableSelectedModsCommand = new AsyncRelayCommand(EnableSelectedModsAsync, () => HasSelectedMods));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand DisableSelectedModsCommand => disableSelectedModsCommand ?? (disableSelectedModsCommand = new AsyncRelayCommand(DisableSelectedModsAsync, () => HasSelectedMods));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestDeleteSelectedModsCommand => requestDeleteSelectedModsCommand ?? (requestDeleteSelectedModsCommand = new RelayCommand(RequestDeleteSelectedMods, () => HasSelectedMods));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<ModManagementModItemViewModel?> ToggleModEnabledCommand => toggleModEnabledCommand ?? (toggleModEnabledCommand = new AsyncRelayCommand<ModManagementModItemViewModel>(ToggleModEnabledAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ModManagementModItemViewModel?> OpenModFileLocationCommand => openModFileLocationCommand ?? (openModFileLocationCommand = new RelayCommand<ModManagementModItemViewModel>(OpenModFileLocation));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ModManagementModItemViewModel?> RequestDeleteModCommand => requestDeleteModCommand ?? (requestDeleteModCommand = new RelayCommand<ModManagementModItemViewModel>(RequestDeleteMod));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ModManagementModItemViewModel?> SelectModCommand => selectModCommand ?? (selectModCommand = new RelayCommand<ModManagementModItemViewModel>(SelectMod));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenModFolderCommand => openModFolderCommand ?? (openModFolderCommand = new RelayCommand(OpenModFolder));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ImportLocalModCommand => importLocalModCommand ?? (importLocalModCommand = new AsyncRelayCommand(ImportLocalModAsync));

	public event Action<ModDeleteRequest>? DeleteModsRequested;

	public event Action<ModImportConflictRequest>? ImportModConflictRequested;

	public event Action<GameInstance>? OnlineModInstallRequested;

	public event Action<ResourceProjectReference>? ResourceDetailsRequested;

	[RelayCommand]
	private void OpenResourceDetails(ModManagementModItemViewModel? mod)
	{
		ResourceProjectReference resourceProjectReference = mod?.ProjectReference;
		if ((object)resourceProjectReference != null)
		{
			ResourceDetailsRequested?.Invoke(resourceProjectReference);
		}
	}

	[RelayCommand]
	private void InstallOnlineMod()
	{
		if (selectedInstance != null && IsModManagementSupported)
		{
			logger.LogInformation("Online mod install requested from instance mod management. InstanceId={InstanceId}, MinecraftVersion={MinecraftVersion}, Loader={Loader}", selectedInstance.Id, selectedInstance.MinecraftVersion, selectedInstance.Loader);
			OnlineModInstallRequested?.Invoke(selectedInstance);
		}
	}

	[RelayCommand]
	private void SetModFilter(ModManagementFilter filter)
	{
		ModFilter = filter;
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

	[RelayCommand(CanExecute = "CanToggleSelectAllMods")]
	private void SelectAllMods()
	{
		if (AreAllVisibleModsSelected)
		{
			foreach (ModManagementModItemViewModel mod in Mods)
			{
				mod.IsSelected = false;
			}
			selectedModPaths.Clear();
			SelectedMod = null;
			UpdateSelectedModState();
			return;
		}
		foreach (ModManagementModItemViewModel mod2 in Mods)
		{
			mod2.IsSelected = true;
			selectedModPaths.Add(mod2.FullPath);
		}
		SelectedMod = null;
		UpdateSelectedModState();
	}

	[RelayCommand(CanExecute = "HasSelectedMods")]
	private async Task EnableSelectedModsAsync()
	{
		await SetSelectedModsEnabledAsync(enabled: true);
	}

	[RelayCommand(CanExecute = "HasSelectedMods")]
	private async Task DisableSelectedModsAsync()
	{
		await SetSelectedModsEnabledAsync(enabled: false);
	}

	[RelayCommand(CanExecute = "HasSelectedMods")]
	private void RequestDeleteSelectedMods()
	{
		IReadOnlyList<ModManagementModItemViewModel> selectedVisibleMods = GetSelectedVisibleMods();
		if (selectedVisibleMods.Count != 0)
		{
			DeleteModsRequested?.Invoke(new ModDeleteRequest(selectedVisibleMods.Select((ModManagementModItemViewModel mod) => mod.FullPath).ToArray(), selectedVisibleMods.Select((ModManagementModItemViewModel mod) => mod.Title).ToArray()));
		}
	}

	[RelayCommand]
	private async Task ToggleModEnabledAsync(ModManagementModItemViewModel? mod)
	{
		if (mod == null)
		{
			return;
		}
		LocalMod localMod = ResolveLocalMod(mod.FullPath);
		if (localMod == null)
		{
			return;
		}
		string nextPath = GetPathForEnabledState(localMod.FullPath, !localMod.IsEnabled);
		string previousSelectedPath = lastSingleSelectedModPath;
		bool wasSelectedInMultiSelect = selectedModPaths.Contains(localMod.FullPath);
		if (IsMultiSelectMode)
		{
			selectedModPaths.Remove(localMod.FullPath);
			if (wasSelectedInMultiSelect)
			{
				selectedModPaths.Add(nextPath);
			}
		}
		else
		{
			lastSingleSelectedModPath = nextPath;
		}
		logger.LogDebug("Toggling local mod enabled state. InstanceId={InstanceId} Path={Path} Enabled={Enabled}", selectedInstance?.Id ?? "<none>", localMod.FullPath, !localMod.IsEnabled);
		try
		{
			suppressLocalCollectionEvents = true;
			try
			{
				await localModsViewModel.ToggleModAsync(localMod);
			}
			finally
			{
				suppressLocalCollectionEvents = false;
			}
			RefreshFromLocalMods();
		}
		catch (ModEnabledStateConflictException ex)
		{
			suppressLocalCollectionEvents = false;
			logger.LogWarning(ex, "Local mod enabled state target already exists. InstanceId={InstanceId} Path={Path} TargetPath={TargetPath}", selectedInstance?.Id ?? "<none>", localMod.FullPath, ex.TargetPath);
			string message = FormatModEnabledStateTargetExists(ex.TargetPath);
			statusService.Report(message);
			floatingMessageService.Show(message);
			RestoreSelectionAfterFailure();
			RefreshFromLocalMods();
		}
		catch (Exception exception)
		{
			suppressLocalCollectionEvents = false;
			logger.LogError(exception, "Failed to toggle local mod enabled state. InstanceId={InstanceId} Path={Path}", selectedInstance?.Id ?? "<none>", localMod.FullPath);
			statusService.Report(localMod.IsEnabled ? Strings.Status_SelectedModsDisableFailed : Strings.Status_SelectedModsEnableFailed);
			RestoreSelectionAfterFailure();
			RefreshFromLocalMods();
		}
		void RestoreSelectionAfterFailure()
		{
			if (IsMultiSelectMode)
			{
				selectedModPaths.Remove(nextPath);
				if (wasSelectedInMultiSelect)
				{
					selectedModPaths.Add(localMod.FullPath);
				}
			}
			else
			{
				lastSingleSelectedModPath = previousSelectedPath;
			}
		}
	}

	[RelayCommand]
	private void OpenModFileLocation(ModManagementModItemViewModel? mod)
	{
		if (mod == null)
		{
			return;
		}
		try
		{
			if (!instanceFolderService.TryRevealFile(mod.FullPath))
			{
				logger.LogWarning("Failed to reveal local mod file. InstanceId={InstanceId} Path={Path}", selectedInstance?.Id ?? "<none>", mod.FullPath);
				statusService.Report(Strings.Status_OpenModFileLocationFailed);
			}
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to reveal local mod file. InstanceId={InstanceId} Path={Path}", selectedInstance?.Id ?? "<none>", mod.FullPath);
			statusService.Report(Strings.Status_OpenModFileLocationFailed);
		}
	}

	[RelayCommand]
	private void RequestDeleteMod(ModManagementModItemViewModel? mod)
	{
		if (mod != null)
		{
			DeleteModsRequested?.Invoke(new ModDeleteRequest(new global::_003C_003Ez__ReadOnlySingleElementList<string>(mod.FullPath), new global::_003C_003Ez__ReadOnlySingleElementList<string>(mod.Title)));
		}
	}

	[RelayCommand]
	private void SelectMod(ModManagementModItemViewModel? mod)
	{
		if (mod == null)
		{
			SelectedMod = null;
			if (IsMultiSelectMode)
			{
				selectedModPaths.Clear();
			}
			foreach (ModManagementModItemViewModel mod2 in Mods)
			{
				mod2.IsSelected = false;
			}
			UpdateSelectedModState();
			return;
		}
		if (IsMultiSelectMode)
		{
			if (mod.IsSelected = !mod.IsSelected)
			{
				selectedModPaths.Add(mod.FullPath);
			}
			else
			{
				selectedModPaths.Remove(mod.FullPath);
			}
			SelectedMod = null;
			UpdateSelectedModState();
			return;
		}
		SelectedMod = mod;
		lastSingleSelectedModPath = mod.FullPath;
		foreach (ModManagementModItemViewModel mod3 in Mods)
		{
			mod3.IsSelected = false;
		}
	}

	public async Task DeleteModsAsync(IReadOnlyList<string> fullPaths)
	{
		ArgumentNullException.ThrowIfNull(fullPaths, "fullPaths");
		IReadOnlyList<LocalMod> modsToDelete = ResolveLocalMods(fullPaths);
		if (modsToDelete.Count == 0)
		{
			ExitMultiSelectMode();
			return;
		}
		logger.LogInformation("Deleting selected mods. InstanceId={InstanceId} Count={Count}", selectedInstance?.Id ?? "<none>", modsToDelete.Count);
		try
		{
			int failedCount = await localModsViewModel.DeleteModsAsync(modsToDelete);
			ExitMultiSelectMode();
			ReportBatchOperationResult(modsToDelete.Count, failedCount, Strings.Status_SelectedModsDeletedFormat, Strings.Status_SelectedModsDeletePartialFailedFormat);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to delete selected mods. InstanceId={InstanceId}", selectedInstance?.Id ?? "<none>");
			statusService.Report(Strings.Status_SelectedModsDeleteFailed);
		}
	}

	public InstanceModManagementSettingsViewModel(GameSettingsDetailsViewModel parent, LocalModsViewModel localModsViewModel, IStatusService statusService, IInstanceFolderService instanceFolderService, IFilePickerService filePickerService, IInstanceContentImportPathValidator importPathValidator, IFloatingMessageService floatingMessageService, IUiDispatcher? uiDispatcher = null, ILogger<InstanceModManagementSettingsViewModel>? logger = null)
		: base(parent)
	{
		this.localModsViewModel = localModsViewModel;
		this.statusService = statusService;
		this.instanceFolderService = instanceFolderService;
		this.filePickerService = filePickerService;
		this.importPathValidator = importPathValidator;
		this.floatingMessageService = floatingMessageService;
		this.uiDispatcher = uiDispatcher ?? ImmediateUiDispatcher.Instance;
		this.logger = logger ?? NullLogger<InstanceModManagementSettingsViewModel>.Instance;
		this.localModsViewModel.ModsChanged += LocalModsViewModel_ModsChanged;
		this.localModsViewModel.IconChanged += LocalModsViewModel_IconChanged;
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
		ResolvePendingImportConflict(shouldReplace: false);
		loadTask = null;
		hasPendingVisualRefresh = false;
		isVisibleRefreshQueued = false;
		suppressLocalCollectionEvents = true;
		try
		{
			localModsViewModel.SetSelectedInstance(instance);
			localModsViewModel.SetSectionActive(isSectionActive && IsModManagementSupported);
		}
		finally
		{
			suppressLocalCollectionEvents = false;
		}
		IsLoadingMods = false;
		HasLoadedMods = false;
		allModsByProjectionPath.Clear();
		SetInitialProjectionReady(value: false);
		ResetSelectionState();
		ClearDisplayedMods();
	}

	public bool RefreshSelectedInstanceReference(GameInstance? instance)
	{
		if (ShouldResetForInstanceReference(instance))
		{
			OnSelectedInstanceChanged(instance);
			return true;
		}
		selectedInstance = instance;
		return false;
	}

	public override void OnSectionDeactivated()
	{
		if (isSectionActive)
		{
			isSectionActive = false;
			Interlocked.Increment(ref lifecycleGeneration);
			loadTask = null;
			IsLoadingMods = false;
			localModsViewModel.SetSectionActive(active: false);
		}
	}

	public void ReleaseLocalObservation()
	{
		isSectionActive = false;
		Interlocked.Increment(ref lifecycleGeneration);
		loadTask = null;
		IsLoadingMods = false;
		localModsViewModel.ReleaseObservation();
	}

	public void SuspendLocalWatchersForInstanceRename()
	{
		localModsViewModel.SuspendWatcherForInstanceRename();
	}

	public void ResumeLocalWatchersAfterInstanceRename(bool restart = true)
	{
		if (restart && isSectionActive)
		{
			Interlocked.Increment(ref lifecycleGeneration);
			loadTask = null;
			localModsViewModel.InvalidateSnapshot();
		}
		localModsViewModel.ResumeWatcherAfterInstanceRename(restart);
	}

	public override Task OnSectionActivatedAsync()
	{
		if (!isSectionActive)
		{
			isSectionActive = true;
			Interlocked.Increment(ref lifecycleGeneration);
			loadTask = null;
			localModsViewModel.SetSectionActive(IsModManagementSupported);
		}
		if (selectedInstance == null || !IsModManagementSupported)
		{
			return Task.CompletedTask;
		}
		if (HasLoadedMods)
		{
			if (hasPendingVisualRefresh)
			{
				hasPendingVisualRefresh = false;
				RefreshFromLocalMods();
			}
			loadTask = RefreshCachedModsAsync(Volatile.Read(in lifecycleGeneration));
			return loadTask;
		}
		return EnsureLoadedForSelectedInstanceAsync();
	}

	public Task EnsureLoadedForSelectedInstanceAsync()
	{
		if (!isSectionActive || selectedInstance == null || !IsModManagementSupported)
		{
			return Task.CompletedTask;
		}
		Task task = loadTask;
		if (task != null && !task.IsCompleted)
		{
			return loadTask;
		}
		if (HasLoadedMods)
		{
			return Task.CompletedTask;
		}
		loadTask = LoadModsAsync(Volatile.Read(in lifecycleGeneration));
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

	[RelayCommand]
	private void OpenModFolder()
	{
		if (selectedInstance == null)
		{
			return;
		}
		try
		{
			string text = instanceFolderService.EnsureDirectoryExists(Path.Combine(selectedInstance.InstanceDirectory, "mods"));
			logger.LogDebug("Opening mod folder. InstanceId={InstanceId} ModsDirectory={ModsDirectory}", selectedInstance.Id, text);
			if (!instanceFolderService.TryOpen(text))
			{
				logger.LogWarning("Failed to open mod folder. InstanceId={InstanceId} ModsDirectory={ModsDirectory}", selectedInstance.Id, text);
				statusService.Report(Strings.Status_OpenInstanceFolderFailed);
			}
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to prepare mod folder for opening. InstanceId={InstanceId}", selectedInstance.Id);
			statusService.Report(Strings.Status_OpenInstanceFolderFailed);
		}
	}

	[RelayCommand]
	private async Task ImportLocalModAsync()
	{
		if (selectedInstance != null)
		{
			string text = filePickerService.PickModFile();
			if (!string.IsNullOrWhiteSpace(text))
			{
				await ImportModFilesAsync(new global::_003C_003Ez__ReadOnlySingleElementList<string>(text), ImportTriggerSource.FilePicker);
			}
		}
	}

	public Task ReplaceImportedModAsync(string sourcePath)
	{
		ResolvePendingImportConflict(shouldReplace: true);
		return Task.CompletedTask;
	}

	public void SkipPendingImportedModReplacement()
	{
		ResolvePendingImportConflict(shouldReplace: false);
	}

	public GameSettingsFileDropEvaluation EvaluateDroppedFiles(IReadOnlyList<string> paths)
	{
		if (!IsModManagementSupported)
		{
			return GameSettingsFileDropEvaluation.Reject(ModUnavailableMessage);
		}
		if (!TryValidateImportPaths(paths, Strings.GameSettings_DropModsOnlyMessage, out string failureMessage))
		{
			return GameSettingsFileDropEvaluation.Reject(failureMessage);
		}
		return GameSettingsFileDropEvaluation.Accept(Strings.GameSettings_DropImportModsMessage);
	}

	public Task ImportDroppedModFilesAsync(IReadOnlyList<string> paths)
	{
		return ImportModFilesAsync(paths, ImportTriggerSource.DragDrop);
	}

	private async Task ImportLocalModCoreAsync(string modPath, bool overwriteExisting)
	{
		if (selectedInstance == null || string.IsNullOrWhiteSpace(modPath))
		{
			return;
		}
		logger.LogDebug("Importing local mod from file picker. InstanceId={InstanceId} SourcePath={SourcePath} OverwriteExisting={OverwriteExisting}", selectedInstance.Id, modPath, overwriteExisting);
		try
		{
			await localModsViewModel.ImportModFromPathAsync(modPath, overwriteExisting);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to import local mod. InstanceId={InstanceId} SourcePath={SourcePath} OverwriteExisting={OverwriteExisting}", selectedInstance.Id, modPath, overwriteExisting);
			statusService.Report(Strings.Status_LocalModImportFailed);
		}
	}

	private async Task ImportModFilesAsync(IReadOnlyList<string> paths, ImportTriggerSource source)
	{
		if (selectedInstance == null)
		{
			return;
		}
		if (!TryValidateImportPaths(paths, Strings.GameSettings_DropModsOnlyMessage, out string failureMessage))
		{
			statusService.Report((source == ImportTriggerSource.DragDrop) ? failureMessage : Strings.Status_LocalModImportFailed);
			return;
		}
		logger.LogInformation("Starting local mod import batch. InstanceId={InstanceId} Source={Source} FileCount={FileCount}", selectedInstance.Id, source, paths.Count);
		int successCount = 0;
		foreach (string modPath in paths)
		{
			string fileName = Path.GetFileName(modPath);
			bool overwriteExisting = false;
			if (localModsViewModel.Mods.Any((LocalMod mod) => string.Equals(mod.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
			{
				if (!(await RequestModImportConflictResolutionAsync(modPath, fileName)))
				{
					logger.LogDebug("Skipping local mod replacement after user canceled conflict dialog. InstanceId={InstanceId} SourcePath={SourcePath}", selectedInstance.Id, modPath);
					continue;
				}
				overwriteExisting = true;
			}
			try
			{
				if (!(await localModsViewModel.ImportModFromPathAsync(modPath, overwriteExisting, reportStatus: false)))
				{
					statusService.Report(Strings.Status_LocalModImportFailed);
					return;
				}
				successCount++;
			}
			catch (Exception exception)
			{
				logger.LogError(exception, "Failed to import local mod during batch import. InstanceId={InstanceId} SourcePath={SourcePath} OverwriteExisting={OverwriteExisting}", selectedInstance.Id, modPath, overwriteExisting);
				statusService.Report(Strings.Status_LocalModImportFailed);
				return;
			}
		}
		if (successCount > 0)
		{
			statusService.Report((successCount == 1) ? Strings.Status_LocalModImported : string.Format(Strings.Status_LocalModsImportedFormat, successCount));
		}
		logger.LogInformation("Local mod import batch completed. InstanceId={InstanceId} RequestedCount={RequestedCount} ImportedCount={ImportedCount} SkippedCount={SkippedCount}", selectedInstance.Id, paths.Count, successCount, paths.Count - successCount);
	}

	private async Task<bool> RequestModImportConflictResolutionAsync(string sourcePath, string fileName)
	{
		pendingImportConflictResolutionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		ImportModConflictRequested?.Invoke(new ModImportConflictRequest(sourcePath, fileName));
		return await pendingImportConflictResolutionSource.Task;
	}

	private void ResolvePendingImportConflict(bool shouldReplace)
	{
		pendingImportConflictResolutionSource?.TrySetResult(shouldReplace);
		pendingImportConflictResolutionSource = null;
	}

	private bool TryValidateImportPaths(IReadOnlyList<string> paths, string invalidTypeMessage, out string failureMessage)
	{
		InstanceContentImportPathValidation instanceContentImportPathValidation = importPathValidator.Validate(paths, InstanceContentImportKind.Mod);
		failureMessage = ((instanceContentImportPathValidation.Failure == InstanceContentImportPathFailure.DirectoryNotSupported) ? Strings.GameSettings_DropFoldersUnsupportedMessage : (instanceContentImportPathValidation.IsValid ? string.Empty : invalidTypeMessage));
		return instanceContentImportPathValidation.IsValid;
	}

	private static string GetPathForEnabledState(string path, bool enabled)
	{
		if (!enabled)
		{
			if (!path.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
			{
				return path + ".disabled";
			}
			return path;
		}
		if (!path.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
		{
			return path;
		}
		int length = ".disabled".Length;
		return path.Substring(0, path.Length - length);
	}

	private static string GetStableModPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		if (!path.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
		{
			return path;
		}
		int length = ".disabled".Length;
		return path.Substring(0, path.Length - length);
	}

	private async Task LoadModsAsync(long generation)
	{
		if (selectedInstance == null || !IsModManagementSupported)
		{
			return;
		}
		GameInstance expectedInstance = selectedInstance;
		SetInitialProjectionReady(value: false);
		IsLoadingMods = true;
		OnPropertyChanged("InstalledSummaryText");
		RaiseAvailabilityPropertyChanges();
		try
		{
			if (await localModsViewModel.RefreshModsAsync() && IsCurrentLifecycle(generation, expectedInstance))
			{
				HasLoadedMods = true;
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
				logger.LogError(exception, "Failed to load mods for section activation. InstanceId={InstanceId}", expectedInstance.Id);
				HasLoadedMods = false;
				ClearDisplayedMods();
				hasPendingVisualRefresh = false;
				SetInitialProjectionReady(value: true);
				statusService.Report(Strings.Status_LoadLocalModsFailed);
			}
		}
		finally
		{
			if (IsCurrentLifecycle(generation, expectedInstance))
			{
				IsLoadingMods = false;
				loadTask = null;
				OnPropertyChanged("InstalledSummaryText");
				RaiseAvailabilityPropertyChanges();
				OnPropertyChanged("ModEmptyMessage");
			}
		}
	}

	private async Task RefreshCachedModsAsync(long generation)
	{
		if (selectedInstance == null || !IsModManagementSupported)
		{
			return;
		}
		GameInstance expectedInstance = selectedInstance;
		long previousRevision = localModsViewModel.Revision;
		try
		{
			if (await localModsViewModel.RefreshIfInvalidatedAsync() && IsCurrentLifecycle(generation, expectedInstance) && localModsViewModel.Revision != previousRevision)
			{
				hasPendingVisualRefresh = false;
				RefreshFromLocalMods();
			}
		}
		catch (Exception exception)
		{
			if (IsCurrentLifecycle(generation, expectedInstance))
			{
				logger.LogError(exception, "Failed to silently refresh cached mods. InstanceId={InstanceId}", expectedInstance.Id);
				statusService.Report(Strings.Status_LoadLocalModsFailed);
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
		InstalledModCount = localModsViewModel.CurrentMods.Count;
		EnabledModCount = localModsViewModel.CurrentMods.Count((LocalMod mod) => mod.IsEnabled);
	}

	private void LocalModsViewModel_ModsChanged(object? sender, EventArgs e)
	{
		if (!suppressLocalCollectionEvents)
		{
			if (!HasLoadedMods)
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

	private void LocalModsViewModel_IconChanged(object? sender, LocalContentIconChangedEventArgs e)
	{
		if (HasLoadedMods && isSectionActive && allModsByFullPath.TryGetValue(e.FullPath, out ModManagementModItemViewModel value) && !string.Equals(value.IconSource, e.IconSource, StringComparison.Ordinal))
		{
			value.IconSource = e.IconSource;
		}
	}

	private void RefreshFromLocalMods()
	{
		string preferredPath = lastSingleSelectedModPath ?? SelectedMod?.FullPath;
		Dictionary<string, int> stablePathCounts = localModsViewModel.CurrentMods.GroupBy<LocalMod, string>((LocalMod mod) => GetStableModPath(mod.FullPath), StringComparer.OrdinalIgnoreCase).ToDictionary<IGrouping<string, LocalMod>, string, int>((IGrouping<string, LocalMod> group) => group.Key, (IGrouping<string, LocalMod> group) => group.Count(), StringComparer.OrdinalIgnoreCase);
		IReadOnlyList<ModManagementModItemViewModel> source = StableFilteredItemProjection.Synchronize(localModsViewModel.CurrentMods, allModsByProjectionPath, delegate(LocalMod mod)
		{
			string stableModPath = GetStableModPath(mod.FullPath);
			return (stablePathCounts[stableModPath] <= 1) ? stableModPath : mod.FullPath;
		}, (LocalMod mod) => new ModManagementModItemViewModel(mod), delegate(ModManagementModItemViewModel item, LocalMod mod)
		{
			item.SyncFrom(mod);
		}, MatchesSearch);
		allModsByFullPath.Clear();
		foreach (ModManagementModItemViewModel value in allModsByProjectionPath.Values)
		{
			allModsByFullPath[value.FullPath] = value;
		}
		if (IsMultiSelectMode)
		{
			selectedModPaths.IntersectWith(source.Select((ModManagementModItemViewModel mod) => mod.FullPath));
		}
		foreach (ModManagementModItemViewModel value2 in allModsByProjectionPath.Values)
		{
			value2.IsSelected = IsMultiSelectMode && selectedModPaths.Contains(value2.FullPath);
		}
		SetVisibleMods(source);
		RefreshSummary();
		OnPropertyChanged("HasMods");
		RaiseAvailabilityPropertyChanges();
		OnPropertyChanged("ModEmptyMessage");
		OnPropertyChanged("AreAllVisibleModsSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllModsCommand.NotifyCanExecuteChanged();
		if (IsMultiSelectMode)
		{
			SelectedMod = null;
			UpdateSelectedModState();
		}
		else
		{
			ModManagementModItemViewModel modManagementModItemViewModel = FindModByPreferredPath(preferredPath);
			SelectMod(modManagementModItemViewModel ?? Mods.FirstOrDefault());
		}
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
				RefreshFromLocalMods();
			}
		});
	}

	private void PublishReadyProjection()
	{
		hasPendingVisualRefresh = false;
		RefreshFromLocalMods();
		SetInitialProjectionReady(value: true);
		ListEntranceAnimationToken++;
	}

	private void SetInitialProjectionReady(bool value)
	{
		if (isInitialProjectionReady != value)
		{
			isInitialProjectionReady = value;
			OnPropertyChanged("CanShowModScrollableContent");
		}
	}

	private bool MatchesSearch(LocalMod mod)
	{
		if (ModFilter == ModManagementFilter.Enabled && !mod.IsEnabled)
		{
			return false;
		}
		if (ModFilter == ModManagementFilter.Disabled && mod.IsEnabled)
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(ModSearchQuery))
		{
			return true;
		}
		string query = ModSearchQuery.Trim();
		if (!Contains(mod.Name, query) && !Contains(mod.Loader, query) && !Contains(mod.ModId, query) && !Contains(mod.Version, query))
		{
			return Contains(mod.FileName, query);
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

	private bool CanToggleSelectAllMods()
	{
		if (IsMultiSelectMode)
		{
			return HasMods;
		}
		return false;
	}

	private void RaiseAvailabilityPropertyChanges()
	{
		OnPropertyChanged("IsModManagementSupported");
		OnPropertyChanged("CanShowModInfoSection");
		OnPropertyChanged("CanShowModScrollableContent");
		OnPropertyChanged("HasInstalledMods");
		OnPropertyChanged("CanShowModListSection");
		OnPropertyChanged("CanShowNoModsEmptyState");
		OnPropertyChanged("CanShowModEmptyState");
		OnPropertyChanged("CanShowModUnavailableState");
		OnPropertyChanged("CanShowModLoadingState");
		OnPropertyChanged("ModUnavailableMessage");
	}

	private void EnterMultiSelectMode()
	{
		lastSingleSelectedModPath = SelectedMod?.FullPath ?? lastSingleSelectedModPath;
		IsMultiSelectMode = true;
		SelectedMod = null;
		selectedModPaths.Clear();
		ClearVisibleSelections();
		UpdateSelectedModState();
	}

	private void ExitMultiSelectMode()
	{
		IsMultiSelectMode = false;
		ClearVisibleSelections();
		selectedModPaths.Clear();
		UpdateSelectedModState();
		ModManagementModItemViewModel modManagementModItemViewModel = FindModByPreferredPath(lastSingleSelectedModPath);
		SelectMod(modManagementModItemViewModel ?? Mods.FirstOrDefault());
	}

	private void ResetSelectionState()
	{
		lastSingleSelectedModPath = null;
		IsMultiSelectMode = false;
		SelectedMod = null;
		selectedModPaths.Clear();
		SelectedModCount = 0;
	}

	private void ClearDisplayedMods()
	{
		allModsByProjectionPath.Clear();
		allModsByFullPath.Clear();
		SetVisibleMods(Array.Empty<ModManagementModItemViewModel>());
		RefreshVisibleModListItems();
		SelectedMod = null;
		RefreshSummary();
		OnPropertyChanged("HasMods");
		RaiseAvailabilityPropertyChanges();
		OnPropertyChanged("ModEmptyMessage");
		OnPropertyChanged("AreAllVisibleModsSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllModsCommand.NotifyCanExecuteChanged();
		UpdateSelectedModState();
	}

	private void ClearVisibleSelections()
	{
		foreach (ModManagementModItemViewModel mod in Mods)
		{
			mod.IsSelected = false;
		}
	}

	private IReadOnlyList<ModManagementModItemViewModel> GetSelectedVisibleMods()
	{
		return Mods.Where((ModManagementModItemViewModel mod) => selectedModPaths.Contains(mod.FullPath)).ToArray();
	}

	private IReadOnlyList<LocalMod> ResolveLocalMods(IEnumerable<string> fullPaths)
	{
		HashSet<string> pathSet = new HashSet<string>(fullPaths, StringComparer.OrdinalIgnoreCase);
		return localModsViewModel.CurrentMods.Where((LocalMod mod) => pathSet.Contains(mod.FullPath)).ToArray();
	}

	private LocalMod? ResolveLocalMod(string fullPath)
	{
		string stablePath = GetStableModPath(fullPath);
		return localModsViewModel.CurrentMods.FirstOrDefault((LocalMod mod) => string.Equals(mod.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)) ?? localModsViewModel.CurrentMods.FirstOrDefault((LocalMod mod) => string.Equals(GetStableModPath(mod.FullPath), stablePath, StringComparison.OrdinalIgnoreCase));
	}

	private ModManagementModItemViewModel? FindModByPreferredPath(string? preferredPath)
	{
		if (string.IsNullOrWhiteSpace(preferredPath))
		{
			return null;
		}
		return Mods.FirstOrDefault((ModManagementModItemViewModel mod) => string.Equals(mod.FullPath, preferredPath, StringComparison.OrdinalIgnoreCase)) ?? Mods.FirstOrDefault((ModManagementModItemViewModel mod) => string.Equals(GetStableModPath(mod.FullPath), GetStableModPath(preferredPath), StringComparison.OrdinalIgnoreCase));
	}

	private async Task SetSelectedModsEnabledAsync(bool enabled)
	{
		IReadOnlyList<LocalMod> selectedMods = ResolveLocalMods(selectedModPaths);
		if (selectedMods.Count == 0)
		{
			UpdateSelectedModState();
			return;
		}
		logger.LogInformation("Changing selected mods enabled state. InstanceId={InstanceId} Count={Count} Enabled={Enabled}", selectedInstance?.Id ?? "<none>", selectedMods.Count, enabled);
		try
		{
			suppressLocalCollectionEvents = true;
			LocalModEnabledStateBatchResult localModEnabledStateBatchResult;
			try
			{
				localModEnabledStateBatchResult = await localModsViewModel.SetModsEnabledAsync(selectedMods, enabled);
			}
			finally
			{
				suppressLocalCollectionEvents = false;
			}
			selectedModPaths.Clear();
			selectedModPaths.UnionWith(selectedMods.Select((LocalMod mod) => mod.FullPath));
			RefreshFromLocalMods();
			ReportBatchOperationResult(selectedMods.Count, localModEnabledStateBatchResult.FailedCount, enabled ? Strings.Status_SelectedModsEnabledFormat : Strings.Status_SelectedModsDisabledFormat, enabled ? Strings.Status_SelectedModsEnablePartialFailedFormat : Strings.Status_SelectedModsDisablePartialFailedFormat);
			if (!string.IsNullOrWhiteSpace(localModEnabledStateBatchResult.FirstConflictTargetPath))
			{
				floatingMessageService.Show(FormatModEnabledStateTargetExists(localModEnabledStateBatchResult.FirstConflictTargetPath));
			}
		}
		catch (Exception exception)
		{
			suppressLocalCollectionEvents = false;
			logger.LogError(exception, "Failed to change selected mods enabled state. InstanceId={InstanceId} Enabled={Enabled}", selectedInstance?.Id ?? "<none>", enabled);
			statusService.Report(enabled ? Strings.Status_SelectedModsEnableFailed : Strings.Status_SelectedModsDisableFailed);
			RefreshFromLocalMods();
		}
	}

	private static string FormatModEnabledStateTargetExists(string targetPath)
	{
		return string.Format(Strings.Status_ModEnabledStateTargetExistsFormat, Path.GetFileName(targetPath));
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

	private void UpdateSelectedModState()
	{
		SelectedModCount = Mods.Count((ModManagementModItemViewModel mod) => mod.IsSelected);
	}

	private void SetVisibleMods(IReadOnlyList<ModManagementModItemViewModel> mods)
	{
		if (!IsSameVisibleMods(mods))
		{
			VisibleMods = mods;
		}
	}

	private bool IsSameVisibleMods(IReadOnlyList<ModManagementModItemViewModel> mods)
	{
		if (VisibleMods.Count != mods.Count)
		{
			return false;
		}
		for (int i = 0; i < mods.Count; i++)
		{
			if (VisibleMods[i] != mods[i])
			{
				return false;
			}
		}
		return true;
	}

	private void RefreshVisibleModListItems()
	{
		if (!CanShowModInfoSection)
		{
			if (VisibleModListItems.Count > 0)
			{
				VisibleModListItems = Array.Empty<object>();
			}
		}
		else if (!IsSameVisibleModListItems())
		{
			bool flag = localModsViewModel.CurrentMods.Count > 0;
			object[] array = new object[VisibleMods.Count + ((!flag) ? 1 : 2)];
			array[0] = ModManagementInfoPanelItem.Instance;
			if (flag)
			{
				array[1] = ModManagementListSectionItem.Instance;
			}
			for (int i = 0; i < VisibleMods.Count; i++)
			{
				array[i + ((!flag) ? 1 : 2)] = VisibleMods[i];
			}
			VisibleModListItems = array;
		}
	}

	private bool IsSameVisibleModListItems()
	{
		bool flag = localModsViewModel.CurrentMods.Count > 0;
		if (VisibleModListItems.Count != VisibleMods.Count + ((!flag) ? 1 : 2))
		{
			return false;
		}
		if (VisibleModListItems[0] != ModManagementInfoPanelItem.Instance)
		{
			return false;
		}
		if (!flag)
		{
			return true;
		}
		if (VisibleModListItems[1] != ModManagementListSectionItem.Instance)
		{
			return false;
		}
		for (int i = 0; i < VisibleMods.Count; i++)
		{
			if (VisibleModListItems[i + 2] != VisibleMods[i])
			{
				return false;
			}
		}
		return true;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnInstalledModCountChanged(int value)
	{
		OnPropertyChanged("InstalledSummaryText");
		RaiseAvailabilityPropertyChanges();
		OnPropertyChanged("ModEmptyMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnEnabledModCountChanged(int value)
	{
		OnPropertyChanged("InstalledSummaryText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnModSearchQueryChanged(string value)
	{
		RefreshFromLocalMods();
		OnPropertyChanged("ModEmptyMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnModFilterChanged(ModManagementFilter value)
	{
		RefreshFromLocalMods();
		OnPropertyChanged("IsAllModsFilterSelected");
		OnPropertyChanged("IsEnabledModsFilterSelected");
		OnPropertyChanged("IsDisabledModsFilterSelected");
		OnPropertyChanged("ModEmptyMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsMultiSelectModeChanged(bool value)
	{
		OnPropertyChanged("AreAllVisibleModsSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllModsCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedModCountChanged(int value)
	{
		OnPropertyChanged("HasSelectedMods");
		OnPropertyChanged("AreAllVisibleModsSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllModsCommand.NotifyCanExecuteChanged();
		EnableSelectedModsCommand.NotifyCanExecuteChanged();
		DisableSelectedModsCommand.NotifyCanExecuteChanged();
		RequestDeleteSelectedModsCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnVisibleModsChanged(IReadOnlyList<ModManagementModItemViewModel> value)
	{
		OnPropertyChanged("Mods");
		RefreshVisibleModListItems();
	}
}
