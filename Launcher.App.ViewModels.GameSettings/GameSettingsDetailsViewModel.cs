using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.ViewModels.Download;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.GameSettings;

public sealed class GameSettingsDetailsViewModel : ObservableObject, IDisposable
{
	private readonly InstanceSettingsPersistenceCoordinator persistence;

	private readonly ILogger logger;

	private bool isPageActive;

	[ObservableProperty]
	private GameSettingsInstanceItem? selectedInstance;

	[ObservableProperty]
	private GameSettingsDetailSectionItem? selectedSection;

	[ObservableProperty]
	private GameSettingsDetailsSectionViewModelBase? currentSectionViewModel;

	public bool HasSelectedInstance => SelectedInstance != null;

	public string SectionTitle => SelectedSection?.Title ?? Strings.GameSettings_DetailGeneral;

	public string SectionPlaceholderBody => string.Format(Strings.GameSettings_DetailPlaceholderBodyFormat, SectionTitle);

	public GameSettingsDetailsSectionViewModelBase? ScrollSectionViewModel
	{
		get
		{
			if ((!(CurrentSectionViewModel?.UsesFullViewportLayout)) ?? true)
			{
				return CurrentSectionViewModel;
			}
			return null;
		}
	}

	public GameSettingsDetailsSectionViewModelBase? FullViewportSectionViewModel
	{
		get
		{
			if ((!(CurrentSectionViewModel?.UsesFullViewportLayout)) ?? true)
			{
				return null;
			}
			return CurrentSectionViewModel;
		}
	}

	public GameSettingsDetailsSectionViewModelBase? NonRetainedFullViewportSectionViewModel
	{
		get
		{
			bool? flag = CurrentSectionViewModel?.UsesFullViewportLayout;
			if (!flag.HasValue || flag != true || IsRetainedLocalContentSection)
			{
				return null;
			}
			return CurrentSectionViewModel;
		}
	}

	public bool IsModManagementSection => CurrentSectionViewModel == ModManagement;

	public bool IsSaveManagementSection => CurrentSectionViewModel == SaveManagement;

	public bool IsResourcePackManagementSection => CurrentSectionViewModel == ResourcePackManagement;

	public bool IsShaderPackManagementSection => CurrentSectionViewModel == ShaderPackManagement;

	private bool IsRetainedLocalContentSection
	{
		get
		{
			if (!IsModManagementSection && !IsSaveManagementSection && !IsResourcePackManagementSection)
			{
				return IsShaderPackManagementSection;
			}
			return true;
		}
	}

	public bool IsGeneralSection => string.Equals(SelectedSection?.Id, "general", StringComparison.OrdinalIgnoreCase);

	public bool IsLaunchSection => string.Equals(SelectedSection?.Id, "launch", StringComparison.OrdinalIgnoreCase);

	public bool IsJavaSection => string.Equals(SelectedSection?.Id, "java", StringComparison.OrdinalIgnoreCase);

	public InstanceGeneralSettingsViewModel General { get; }

	public InstanceLaunchSettingsViewModel Launch { get; }

	public InstanceJavaSettingsViewModel Java { get; }

	public InstanceModManagementSettingsViewModel ModManagement { get; }

	public InstanceSaveManagementSettingsViewModel SaveManagement { get; }

	public InstanceResourcePackManagementSettingsViewModel ResourcePackManagement { get; }

	public InstanceShaderPackManagementSettingsViewModel ShaderPackManagement { get; }

	public InstanceBackupSettingsViewModel Backup { get; }

	public InstanceExportSettingsViewModel Export { get; }

	public InstancePlaceholderSettingsViewModel Placeholder { get; }

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public GameSettingsInstanceItem? SelectedInstance
	{
		get
		{
			return selectedInstance;
		}
		set
		{
			if (!EqualityComparer<GameSettingsInstanceItem>.Default.Equals(selectedInstance, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedInstance);
				selectedInstance = value;
				OnSelectedInstanceChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedInstance);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public GameSettingsDetailSectionItem? SelectedSection
	{
		get
		{
			return selectedSection;
		}
		set
		{
			if (!EqualityComparer<GameSettingsDetailSectionItem>.Default.Equals(selectedSection, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedSection);
				selectedSection = value;
				OnSelectedSectionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedSection);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public GameSettingsDetailsSectionViewModelBase? CurrentSectionViewModel
	{
		get
		{
			return currentSectionViewModel;
		}
		set
		{
			if (!EqualityComparer<GameSettingsDetailsSectionViewModelBase>.Default.Equals(currentSectionViewModel, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentSectionViewModel);
				currentSectionViewModel = value;
				OnCurrentSectionViewModelChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentSectionViewModel);
			}
		}
	}

	public event Action<GameInstance>? InstanceSettingsSaved;

	public event Action<GameSettingsInstanceItem>? DeleteInstanceRequested;

	public event Action<ModDeleteRequest>? DeleteModsRequested;

	public event Action<SaveDeleteRequest>? DeleteSavesRequested;

	public event Action<ModImportConflictRequest>? ImportModConflictRequested;

	public event Action<GameInstance>? OnlineModInstallRequested;

	public event Action<SaveImportFailureRequest>? SaveImportFailedRequested;

	public event Action<ResourcePackDeleteRequest>? DeleteResourcePacksRequested;

	public event Action<ResourcePackImportFailureRequest>? ResourcePackImportFailedRequested;

	public event Action<ShaderPackDeleteRequest>? DeleteShaderPacksRequested;

	public event Action<ShaderPackImportFailureRequest>? ShaderPackImportFailedRequested;

	public GameSettingsDetailsViewModel(GameSettingsEditDialogViewModel editDialog, IGameInstanceService instanceService, IStatusService statusService, IInstanceFolderService instanceFolderService, ISystemMemoryService systemMemoryService, IModService modService, IInstanceBackupService backupService, DownloadTasksPageViewModel downloadTasksPage, LocalModsViewModel localModsViewModel, LocalSavesViewModel localSavesViewModel, LocalResourcePacksViewModel localResourcePacksViewModel, LocalShaderPacksViewModel localShaderPacksViewModel, IJavaRuntimeDiscoveryService javaRuntimeDiscoveryService, IFilePickerService filePickerService, IInstanceContentImportPathValidator importPathValidator, IFloatingMessageService floatingMessageService, IUiDispatcher uiDispatcher, ILogger<GameSettingsDetailsViewModel>? logger = null, ILoggerFactory? loggerFactory = null, IModpackExportService? modpackExportService = null)
	{
		persistence = new InstanceSettingsPersistenceCoordinator(instanceService, statusService, uiDispatcher, this.logger = logger ?? NullLogger<GameSettingsDetailsViewModel>.Instance);
		persistence.InstanceSaved += Persistence_InstanceSaved;
		General = new InstanceGeneralSettingsViewModel(editDialog, instanceFolderService, statusService, persistence);
		General.DeleteInstanceRequested += General_DeleteInstanceRequested;
		Launch = new InstanceLaunchSettingsViewModel(systemMemoryService, modService, persistence);
		Java = new InstanceJavaSettingsViewModel(persistence, javaRuntimeDiscoveryService, statusService, filePickerService, floatingMessageService);
		ModManagement = new InstanceModManagementSettingsViewModel(this, localModsViewModel, statusService, instanceFolderService, filePickerService, importPathValidator, floatingMessageService, null, loggerFactory?.CreateLogger<InstanceModManagementSettingsViewModel>());
		ModManagement.DeleteModsRequested += ModManagement_DeleteModsRequested;
		ModManagement.ImportModConflictRequested += ModManagement_ImportModConflictRequested;
		ModManagement.OnlineModInstallRequested += ModManagement_OnlineModInstallRequested;
		SaveManagement = new InstanceSaveManagementSettingsViewModel(this, localSavesViewModel, statusService, instanceFolderService, filePickerService, importPathValidator, null, loggerFactory?.CreateLogger<InstanceSaveManagementSettingsViewModel>());
		SaveManagement.DeleteSavesRequested += SaveManagement_DeleteSavesRequested;
		SaveManagement.SaveImportFailedRequested += SaveManagement_SaveImportFailedRequested;
		ResourcePackManagement = new InstanceResourcePackManagementSettingsViewModel(this, localResourcePacksViewModel, statusService, instanceFolderService, filePickerService, importPathValidator, null, loggerFactory?.CreateLogger<InstanceResourcePackManagementSettingsViewModel>());
		ResourcePackManagement.DeleteResourcePacksRequested += ResourcePackManagement_DeleteResourcePacksRequested;
		ResourcePackManagement.ResourcePackImportFailedRequested += ResourcePackManagement_ResourcePackImportFailedRequested;
		ShaderPackManagement = new InstanceShaderPackManagementSettingsViewModel(this, localShaderPacksViewModel, statusService, instanceFolderService, filePickerService, importPathValidator, null, loggerFactory?.CreateLogger<InstanceShaderPackManagementSettingsViewModel>());
		ShaderPackManagement.DeleteShaderPacksRequested += ShaderPackManagement_DeleteShaderPacksRequested;
		ShaderPackManagement.ShaderPackImportFailedRequested += ShaderPackManagement_ShaderPackImportFailedRequested;
		Backup = new InstanceBackupSettingsViewModel(this, instanceService, backupService, downloadTasksPage, statusService, instanceFolderService, filePickerService, floatingMessageService, loggerFactory?.CreateLogger<InstanceBackupSettingsViewModel>());
		Export = new InstanceExportSettingsViewModel(this, filePickerService, statusService, floatingMessageService, modpackExportService);
		Placeholder = new InstancePlaceholderSettingsViewModel(this);
		CurrentSectionViewModel = General;
	}

	public void PrimeFromSettings(LauncherSettings launcherSettings)
	{
		Launch.PrimeFromSettings(launcherSettings);
		Java.PrimeFromSettings(launcherSettings);
	}

	public void SetSelectedInstance(GameSettingsInstanceItem? instance)
	{
		GameSettingsInstanceItem? gameSettingsInstanceItem = SelectedInstance;
		SelectedInstance = instance;
		if (gameSettingsInstanceItem == instance)
		{
			RefreshSelectedInstanceReference(instance);
		}
	}

	public void SetSelectedSection(GameSettingsDetailSectionItem? section)
	{
		SelectedSection = section;
	}

	public void SetPageActive(bool value, bool releaseLocalContentObservation = false)
	{
		if (isPageActive == value)
		{
			if (!value & releaseLocalContentObservation)
			{
				ReleaseLocalContentObservation();
			}
			return;
		}
		isPageActive = value;
		if (isPageActive)
		{
			ActivateCurrentSection();
			return;
		}
		CurrentSectionViewModel?.OnSectionDeactivated();
		if (releaseLocalContentObservation)
		{
			ReleaseLocalContentObservation();
		}
	}

	private void ReleaseLocalContentObservation()
	{
		ModManagement.ReleaseLocalObservation();
		SaveManagement.ReleaseLocalObservation();
		ResourcePackManagement.ReleaseLocalObservation();
		ShaderPackManagement.ReleaseLocalObservation();
	}

	public void NotifyInstanceSettingsSaved(GameInstance instance)
	{
		InstanceSettingsSaved?.Invoke(instance);
	}

	public void SuspendLocalWatchersForInstanceMove()
	{
		ModManagement.SuspendLocalWatchersForInstanceRename();
		SaveManagement.SuspendLocalWatchersForInstanceRename();
		ResourcePackManagement.SuspendLocalWatchersForInstanceRename();
		ShaderPackManagement.SuspendLocalWatchersForInstanceRename();
	}

	public void ResumeLocalWatchersAfterInstanceMove(bool restart = true)
	{
		ModManagement.ResumeLocalWatchersAfterInstanceRename(restart);
		SaveManagement.ResumeLocalWatchersAfterInstanceRename(restart);
		ResourcePackManagement.ResumeLocalWatchersAfterInstanceRename(restart);
		ShaderPackManagement.ResumeLocalWatchersAfterInstanceRename(restart);
		if (restart)
		{
			ActivateCurrentSection();
		}
	}

	public void ClearSelectedInstanceIf(string instanceId)
	{
		if (string.Equals(SelectedInstance?.Instance.Id, instanceId, StringComparison.Ordinal))
		{
			SetSelectedInstance(null);
		}
	}

	public Task DeleteModsAsync(IReadOnlyList<string> fullPaths)
	{
		return ModManagement.DeleteModsAsync(fullPaths);
	}

	public Task DeleteSavesAsync(IReadOnlyList<string> fullPaths)
	{
		return SaveManagement.DeleteSavesAsync(fullPaths);
	}

	public Task DeleteResourcePacksAsync(IReadOnlyList<string> fullPaths)
	{
		return ResourcePackManagement.DeleteResourcePacksAsync(fullPaths);
	}

	public Task DeleteShaderPacksAsync(IReadOnlyList<string> fullPaths)
	{
		return ShaderPackManagement.DeleteShaderPacksAsync(fullPaths);
	}

	private void Persistence_InstanceSaved(GameInstance instance)
	{
		InstanceSettingsSaved?.Invoke(instance);
	}

	private void General_DeleteInstanceRequested(GameSettingsInstanceItem instance)
	{
		DeleteInstanceRequested?.Invoke(instance);
	}

	private void ModManagement_OnlineModInstallRequested(GameInstance instance)
	{
		OnlineModInstallRequested?.Invoke(instance);
	}

	private void ModManagement_DeleteModsRequested(ModDeleteRequest request)
	{
		DeleteModsRequested?.Invoke(request);
	}

	private void SaveManagement_DeleteSavesRequested(SaveDeleteRequest request)
	{
		DeleteSavesRequested?.Invoke(request);
	}

	private void SaveManagement_SaveImportFailedRequested(SaveImportFailureRequest request)
	{
		SaveImportFailedRequested?.Invoke(request);
	}

	private void ResourcePackManagement_DeleteResourcePacksRequested(ResourcePackDeleteRequest request)
	{
		DeleteResourcePacksRequested?.Invoke(request);
	}

	private void ResourcePackManagement_ResourcePackImportFailedRequested(ResourcePackImportFailureRequest request)
	{
		ResourcePackImportFailedRequested?.Invoke(request);
	}

	private void ShaderPackManagement_DeleteShaderPacksRequested(ShaderPackDeleteRequest request)
	{
		DeleteShaderPacksRequested?.Invoke(request);
	}

	private void ShaderPackManagement_ShaderPackImportFailedRequested(ShaderPackImportFailureRequest request)
	{
		ShaderPackImportFailedRequested?.Invoke(request);
	}

	private void ModManagement_ImportModConflictRequested(ModImportConflictRequest request)
	{
		ImportModConflictRequested?.Invoke(request);
	}

	public GameSettingsFileDropEvaluation EvaluateImportDrop(IReadOnlyList<string> paths)
	{
		if (SelectedInstance == null)
		{
			return GameSettingsFileDropEvaluation.Hidden;
		}
		return SelectedSection?.Id?.ToLowerInvariant() switch
		{
			"mod_management" => ModManagement.EvaluateDroppedFiles(paths), 
			"saves" => SaveManagement.EvaluateDroppedFiles(paths), 
			"resource_packs" => ResourcePackManagement.EvaluateDroppedFiles(paths), 
			"shaders" => ShaderPackManagement.EvaluateDroppedFiles(paths), 
			_ => GameSettingsFileDropEvaluation.Hidden, 
		};
	}

	public Task HandleImportDropAsync(IReadOnlyList<string> paths)
	{
		if (SelectedInstance == null)
		{
			return Task.CompletedTask;
		}
		return SelectedSection?.Id?.ToLowerInvariant() switch
		{
			"mod_management" => ModManagement.ImportDroppedModFilesAsync(paths), 
			"saves" => SaveManagement.ImportDroppedSaveArchivesAsync(paths), 
			"resource_packs" => ResourcePackManagement.ImportDroppedResourcePackArchivesAsync(paths), 
			"shaders" => ShaderPackManagement.ImportDroppedShaderPackArchivesAsync(paths), 
			_ => Task.CompletedTask, 
		};
	}

	public void ResolvePendingModImportConflict(bool shouldReplace)
	{
		if (shouldReplace)
		{
			ModManagement.ReplaceImportedModAsync(string.Empty);
		}
		else
		{
			ModManagement.SkipPendingImportedModReplacement();
		}
	}

	public Task ReplaceImportedModAsync(string sourcePath)
	{
		return ModManagement.ReplaceImportedModAsync(sourcePath);
	}

	public void Dispose()
	{
		persistence.InstanceSaved -= Persistence_InstanceSaved;
		General.DeleteInstanceRequested -= General_DeleteInstanceRequested;
		General.Dispose();
		Launch.Dispose();
		Java.Dispose();
		persistence.Dispose();
	}

	private void ApplySelectedInstanceChanged(GameSettingsInstanceItem? value)
	{
		GameInstance instance = value?.Instance;
		persistence.SetInstance(instance);
		General.SetSelectedInstance(value);
		Launch.SetSelectedInstance(instance);
		Java.SetSelectedInstance(instance);
		ModManagement.OnSelectedInstanceChanged(instance);
		SaveManagement.OnSelectedInstanceChanged(instance);
		ResourcePackManagement.OnSelectedInstanceChanged(instance);
		ShaderPackManagement.OnSelectedInstanceChanged(instance);
		Backup.OnSelectedInstanceChanged(instance);
		Export.OnSelectedInstanceChanged(instance);
		OnPropertyChanged("HasSelectedInstance");
		ActivateCurrentSection();
	}

	private void RefreshSelectedInstanceReference(GameSettingsInstanceItem? value)
	{
		GameInstance instance = value?.Instance;
		persistence.SetInstance(instance);
		General.SetSelectedInstance(value);
		Launch.SetSelectedInstance(instance);
		Java.SetSelectedInstance(instance);
		bool num = ModManagement.RefreshSelectedInstanceReference(instance) | SaveManagement.RefreshSelectedInstanceReference(instance) | ResourcePackManagement.RefreshSelectedInstanceReference(instance) | ShaderPackManagement.RefreshSelectedInstanceReference(instance);
		Backup.OnSelectedInstanceChanged(instance);
		Export.OnSelectedInstanceChanged(instance);
		if (num)
		{
			ActivateCurrentSection();
		}
	}

	private void ActivateCurrentSection()
	{
		if (isPageActive && SelectedInstance != null)
		{
			GameSettingsDetailsSectionViewModelBase gameSettingsDetailsSectionViewModelBase = CurrentSectionViewModel;
			if (gameSettingsDetailsSectionViewModelBase != null)
			{
				ObserveSectionActivationAsync(gameSettingsDetailsSectionViewModelBase);
			}
		}
	}

	private async Task ObserveSectionActivationAsync(GameSettingsDetailsSectionViewModelBase section)
	{
		try
		{
			await section.OnSectionActivatedAsync();
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to activate game settings section. SectionType={SectionType} InstanceId={InstanceId}", section.GetType().Name, SelectedInstance?.Instance.Id);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedInstanceChanged(GameSettingsInstanceItem? value)
	{
		ApplySelectedInstanceChanged(value);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedSectionChanged(GameSettingsDetailSectionItem? value)
	{
		GameSettingsDetailsSectionViewModelBase? gameSettingsDetailsSectionViewModelBase = CurrentSectionViewModel;
		OnPropertyChanged("IsGeneralSection");
		OnPropertyChanged("IsLaunchSection");
		OnPropertyChanged("IsJavaSection");
		OnPropertyChanged("SectionTitle");
		OnPropertyChanged("SectionPlaceholderBody");
		gameSettingsDetailsSectionViewModelBase?.OnSectionDeactivated();
		CurrentSectionViewModel = value?.Id?.ToLowerInvariant() switch
		{
			"general" => General, 
			"launch" => Launch, 
			"java" => Java, 
			"mod_management" => ModManagement, 
			"saves" => SaveManagement, 
			"resource_packs" => ResourcePackManagement, 
			"shaders" => ShaderPackManagement, 
			"backup" => Backup, 
			"export" => Export, 
			_ => Placeholder, 
		};
		ActivateCurrentSection();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnCurrentSectionViewModelChanged(GameSettingsDetailsSectionViewModelBase? value)
	{
		OnPropertyChanged("ScrollSectionViewModel");
		OnPropertyChanged("FullViewportSectionViewModel");
		OnPropertyChanged("NonRetainedFullViewportSectionViewModel");
		OnPropertyChanged("IsModManagementSection");
		OnPropertyChanged("IsSaveManagementSection");
		OnPropertyChanged("IsResourcePackManagementSection");
		OnPropertyChanged("IsShaderPackManagementSection");
	}
}
