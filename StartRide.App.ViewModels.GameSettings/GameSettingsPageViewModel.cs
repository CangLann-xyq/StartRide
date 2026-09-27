using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class GameSettingsPageViewModel : ObservableObject
{
	private readonly IStatusService statusService;

	private readonly IInstanceFolderService instanceFolderService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly ILogger<GameSettingsPageViewModel> logger;

	private INotifyPropertyChanged? selectedInstanceNotifier;

	private bool isShellPageActive;

	[ObservableProperty]
	private GameSettingsPageStep currentStep;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<GameSettingsInstanceItem>? selectInstanceCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<object?>? selectSecondaryMenuItemCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? backToInstanceListCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestMinecraftDirectorySwitchCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<GameSettingsInstanceItem>? openInstanceFolderCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<GameSettingsInstanceItem>? selectInstanceAndGoHomeCommand;

	public GameSettingsInstanceListViewModel InstanceList { get; }

	public GameSettingsDetailsViewModel Details { get; }

	public GameSettingsEditDialogViewModel EditDialog { get; }

	public GameSettingsDialogsViewModel Dialogs { get; }

	public ObservableCollection<GameSettingsDetailSectionItem> DetailSections { get; } = new ObservableCollection<GameSettingsDetailSectionItem>();

	public bool IsListStep => CurrentStep == GameSettingsPageStep.List;

	public bool IsDetailsStep => CurrentStep == GameSettingsPageStep.Details;

	public bool IsGameSettingsContentVisible
	{
		get
		{
			if (!IsDetailsStep)
			{
				return InstanceList.HasVisibleInstances;
			}
			return true;
		}
	}

	public IEnumerable CurrentSecondaryMenuItems
	{
		get
		{
			if (!IsDetailsStep)
			{
				return InstanceList.Categories;
			}
			return DetailSections;
		}
	}

	public string PageTitle
	{
		get
		{
			object obj;
			if (!IsDetailsStep || InstanceList.SelectedInstance == null)
			{
				obj = InstanceList.SelectedCategory?.Title;
				if (obj == null)
				{
					return Strings.GameSettings_AllCategory;
				}
			}
			else
			{
				obj = InstanceList.SelectedInstance.Name;
			}
			return (string)obj;
		}
	}

	public string? PageTitleIconSource
	{
		get
		{
			if (!IsDetailsStep)
			{
				// 列表步骤显示的是版本分类（正式版/全部），标题前统一挂 BeamNG 官方 logo。
				return BeamNgLogoIconSource;
			}
			return InstanceList.SelectedInstance?.IconSource ?? BeamNgLogoIconSource;
		}
	}

	/// <summary>BeamNG.drive 官方 logo（随 EXE 打包的品牌资源，统一走 BrandingIcons）。</summary>
	private const string BeamNgLogoIconSource = StartRide.Core.BrandingIcons.BeamNgLogo;

	public bool IsModManagementDetailsStep => IsDetailsSection("mod_management");

	public bool IsSaveManagementDetailsStep => IsDetailsSection("saves");

	public bool IsResourcePackManagementDetailsStep => IsDetailsSection("resource_packs");

	public bool IsShaderPackManagementDetailsStep => IsDetailsSection("shaders");

	public bool IsBackupManagementDetailsStep => IsDetailsSection("backup");

	public bool IsExportDetailsStep => IsDetailsSection("export");

	public bool IsTopResourceManagementDetailsStep
	{
		get
		{
			if (!IsModManagementDetailsStep && !IsSaveManagementDetailsStep && !IsResourcePackManagementDetailsStep && !IsShaderPackManagementDetailsStep)
			{
				return IsBackupManagementDetailsStep;
			}
			return true;
		}
	}

	public bool IsTopSearchVisible
	{
		get
		{
			if (!IsListStep)
			{
				return IsTopResourceManagementDetailsStep;
			}
			return true;
		}
	}

	public string TopSearchQuery
	{
		get
		{
			if (IsModManagementDetailsStep)
			{
				return Details.ModManagement.ModSearchQuery;
			}
			if (IsSaveManagementDetailsStep)
			{
				return Details.SaveManagement.SaveSearchQuery;
			}
			if (IsResourcePackManagementDetailsStep)
			{
				return Details.ResourcePackManagement.ResourcePackSearchQuery;
			}
			if (IsShaderPackManagementDetailsStep)
			{
				return Details.ShaderPackManagement.ShaderPackSearchQuery;
			}
			if (IsBackupManagementDetailsStep)
			{
				return Details.Backup.BackupSearchQuery;
			}
			return InstanceList.SearchQuery;
		}
		set
		{
			if (IsModManagementDetailsStep)
			{
				Details.ModManagement.ModSearchQuery = value;
			}
			else if (IsSaveManagementDetailsStep)
			{
				Details.SaveManagement.SaveSearchQuery = value;
			}
			else if (IsResourcePackManagementDetailsStep)
			{
				Details.ResourcePackManagement.ResourcePackSearchQuery = value;
			}
			else if (IsShaderPackManagementDetailsStep)
			{
				Details.ShaderPackManagement.ShaderPackSearchQuery = value;
			}
			else if (IsBackupManagementDetailsStep)
			{
				Details.Backup.BackupSearchQuery = value;
			}
			else if (IsListStep)
			{
				InstanceList.SearchQuery = value;
			}
			OnPropertyChanged("TopSearchQuery");
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public GameSettingsPageStep CurrentStep
	{
		get
		{
			return currentStep;
		}
		set
		{
			if (!EqualityComparer<GameSettingsPageStep>.Default.Equals(currentStep, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentStep);
				currentStep = value;
				OnCurrentStepChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentStep);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<GameSettingsInstanceItem> SelectInstanceCommand => selectInstanceCommand ?? (selectInstanceCommand = new RelayCommand<GameSettingsInstanceItem>(SelectInstance));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<object?> SelectSecondaryMenuItemCommand => selectSecondaryMenuItemCommand ?? (selectSecondaryMenuItemCommand = new RelayCommand<object>(SelectSecondaryMenuItem));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand BackToInstanceListCommand => backToInstanceListCommand ?? (backToInstanceListCommand = new RelayCommand(BackToInstanceList));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestMinecraftDirectorySwitchCommand => requestMinecraftDirectorySwitchCommand ?? (requestMinecraftDirectorySwitchCommand = new RelayCommand(RequestMinecraftDirectorySwitch, CanRequestMinecraftDirectorySwitch));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<GameSettingsInstanceItem> OpenInstanceFolderCommand => openInstanceFolderCommand ?? (openInstanceFolderCommand = new RelayCommand<GameSettingsInstanceItem>(OpenInstanceFolder));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<GameSettingsInstanceItem> SelectInstanceAndGoHomeCommand => selectInstanceAndGoHomeCommand ?? (selectInstanceAndGoHomeCommand = new RelayCommand<GameSettingsInstanceItem>(SelectInstanceAndGoHome));

	public event Action<GameInstance>? LaunchInstanceRequested;

	public event Action<GameSettingsInstancesChangedEventArgs>? InstancesChanged;

	public event Action<GameInstance>? OnlineModInstallRequested;

	public event Action? LocalImportRequested;

	public event Action? InstanceListActivated;

	public event Action? MinecraftDirectorySwitchRequested;

	public event Action<ResourceProjectReference>? ResourceProjectDetailsRequested;

	public GameSettingsPageViewModel(GameSettingsInstanceListViewModel instanceList, GameSettingsDetailsViewModel details, GameSettingsEditDialogViewModel editDialog, GameSettingsDialogsViewModel dialogs, IStatusService statusService, IInstanceFolderService instanceFolderService, IFloatingMessageService floatingMessageService, ILogger<GameSettingsPageViewModel>? logger = null)
	{
		InstanceList = instanceList;
		Details = details;
		EditDialog = editDialog;
		Dialogs = dialogs;
		this.statusService = statusService;
		this.instanceFolderService = instanceFolderService;
		this.floatingMessageService = floatingMessageService;
		this.logger = logger ?? NullLogger<GameSettingsPageViewModel>.Instance;
		foreach (GameSettingsDetailSectionItem item in GameSettingsDetailSectionFactory.Create())
		{
			DetailSections.Add(item);
		}
		SelectDetailsSectionCore(DetailSections.FirstOrDefault());
		InstanceList.PropertyChanged += InstanceList_PropertyChanged;
		InstanceList.LocalImportRequested += InstanceList_LocalImportRequested;
		EditDialog.InstanceUpdated += EditDialog_InstanceUpdated;
		EditDialog.InstanceRenameStarting += EditDialog_InstanceRenameStarting;
		EditDialog.InstanceRenameFinished += EditDialog_InstanceRenameFinished;
		Dialogs.InstanceDeleted += Dialogs_InstanceDeleted;
		Details.InstanceSettingsSaved += Details_InstanceSettingsSaved;
		Details.DeleteInstanceRequested += Dialogs.OpenDeleteInstance;
		Details.DeleteModsRequested += Dialogs.OpenDeleteMods;
		Details.DeleteSavesRequested += Dialogs.OpenDeleteSaves;
		Details.DeleteResourcePacksRequested += Dialogs.OpenDeleteResourcePacks;
		Details.DeleteShaderPacksRequested += Dialogs.OpenDeleteShaderPacks;
		Details.ImportModConflictRequested += Dialogs.OpenModImportConflict;
		Details.SaveImportFailedRequested += Dialogs.OpenSaveImportFailure;
		Details.ResourcePackImportFailedRequested += Dialogs.OpenResourcePackImportFailure;
		Details.ShaderPackImportFailedRequested += Dialogs.OpenShaderPackImportFailure;
		Details.OnlineModInstallRequested += Details_OnlineModInstallRequested;
		Details.ModManagement.ResourceDetailsRequested += delegate(ResourceProjectReference reference)
		{
			ResourceProjectDetailsRequested?.Invoke(reference);
		};
		Details.ResourcePackManagement.ResourceDetailsRequested += delegate(ResourceProjectReference reference)
		{
			ResourceProjectDetailsRequested?.Invoke(reference);
		};
		Details.ShaderPackManagement.ResourceDetailsRequested += delegate(ResourceProjectReference reference)
		{
			ResourceProjectDetailsRequested?.Invoke(reference);
		};
		Details.PropertyChanged += Details_PropertyChanged;
		Details.ModManagement.PropertyChanged += ModManagement_PropertyChanged;
		Details.SaveManagement.PropertyChanged += SaveManagement_PropertyChanged;
		Details.ResourcePackManagement.PropertyChanged += ResourcePackManagement_PropertyChanged;
		Details.ShaderPackManagement.PropertyChanged += ShaderPackManagement_PropertyChanged;
		Details.Backup.PropertyChanged += Backup_PropertyChanged;
		HandleSelectedInstanceChanged();
	}

	public void SetShellPageActive(bool active)
	{
		if (isShellPageActive != active)
		{
			isShellPageActive = active;
			UpdateDetailsActivation();
		}
	}

	public bool UpdateImportDropState(IReadOnlyList<string> paths)
	{
		GameSettingsFileDropEvaluation evaluation = EvaluateImportDrop(paths);
		ApplyImportDropHint(evaluation);
		if (evaluation.ShouldHandle)
		{
			return evaluation.CanAccept;
		}
		return false;
	}

	public void ClearImportDropState()
	{
		floatingMessageService.ClearDragHint(this);
	}

	public async Task HandleImportDropAsync(IReadOnlyList<string> paths)
	{
		GameSettingsFileDropEvaluation gameSettingsFileDropEvaluation = EvaluateImportDrop(paths);
		ClearImportDropState();
		if (!gameSettingsFileDropEvaluation.ShouldHandle || !gameSettingsFileDropEvaluation.CanAccept)
		{
			return;
		}
		try
		{
			logger.LogInformation("Handling game settings import drop. Section={SectionId} FileCount={FileCount} InstanceId={InstanceId}", Details.SelectedSection?.Id ?? "<none>", paths.Count, InstanceList.SelectedInstance?.Instance.Id ?? "<none>");
			await Details.HandleImportDropAsync(paths);
		}
		finally
		{
			ClearImportDropState();
		}
	}

	public void PrimeFromSettings(LauncherSettings settings)
	{
		Details.PrimeFromSettings(settings);
	}

	public bool ApplyInstanceCatalog(IReadOnlyList<GameInstance> instances, long catalogRevision, bool playEntranceAnimation = true)
	{
		return InstanceList.ApplyInstanceCatalog(instances, catalogRevision, playEntranceAnimation);
	}

	public async Task OpenInstanceDetailsAsync(GameInstance? instance, CancellationToken cancellationToken = default(CancellationToken))
	{
		await OpenInstanceDetailsAsync(instance, null, cancellationToken);
	}

	public async Task OpenInstanceJavaSettingsAsync(GameInstance instance, CancellationToken cancellationToken = default(CancellationToken))
	{
		await OpenInstanceDetailsAsync(instance, "java", cancellationToken);
	}

	public void ShowInstanceDetails(GameInstance? instance, string? sectionId = null)
	{
		if (instance == null)
		{
			CurrentStep = GameSettingsPageStep.List;
			return;
		}
		GameSettingsInstanceItem orAdd = InstanceList.GetOrAdd(instance);
		InstanceList.SelectInstance(orAdd);
		SelectDetailsSectionCore(ResolveDetailSection(sectionId));
		CurrentStep = GameSettingsPageStep.Details;
	}

	[RelayCommand]
	private void SelectInstance(GameSettingsInstanceItem instance)
	{
		InstanceList.SelectInstance(instance);
		SelectDetailsSectionCore(DetailSections.FirstOrDefault());
		CurrentStep = GameSettingsPageStep.Details;
	}

	[RelayCommand]
	private void SelectSecondaryMenuItem(object? item)
	{
		if (!(item is GameSettingsInstanceCategory category))
		{
			if (item is GameSettingsDetailSectionItem section)
			{
				SelectDetailsSectionCore(section);
			}
		}
		else
		{
			CurrentStep = GameSettingsPageStep.List;
			InstanceList.SelectCategory(category);
		}
	}

	[RelayCommand]
	private void BackToInstanceList()
	{
		CurrentStep = GameSettingsPageStep.List;
		InstanceList.SetPreserveFilteredSelection(value: false);
	}

	private bool CanRequestMinecraftDirectorySwitch()
	{
		return IsListStep;
	}

	[RelayCommand(CanExecute = "CanRequestMinecraftDirectorySwitch")]
	private void RequestMinecraftDirectorySwitch()
	{
		MinecraftDirectorySwitchRequested?.Invoke();
	}

	[RelayCommand]
	private void OpenInstanceFolder(GameSettingsInstanceItem instance)
	{
		string instanceDirectory = instance.Instance.InstanceDirectory;
		if (!instanceFolderService.DirectoryExists(instanceDirectory))
		{
			statusService.Report(Strings.Status_InstanceFolderNotFound);
		}
		else if (!instanceFolderService.TryOpen(instanceDirectory))
		{
			statusService.Report(Strings.Status_OpenInstanceFolderFailed);
		}
	}

	[RelayCommand]
	private void SelectInstanceAndGoHome(GameSettingsInstanceItem instance)
	{
		LaunchInstanceRequested?.Invoke(instance.Instance);
	}

	private void UpdateDetailsActivation()
	{
		Details.SetPageActive(isShellPageActive && IsDetailsStep, IsListStep);
	}

	private Task OpenInstanceDetailsAsync(GameInstance? instance, string? sectionId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ShowInstanceDetails(instance, sectionId);
		return Task.CompletedTask;
	}

	private bool IsDetailsSection(string sectionId)
	{
		if (IsDetailsStep)
		{
			return string.Equals(Details.SelectedSection?.Id, sectionId, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private void SelectDetailsSectionCore(GameSettingsDetailSectionItem? section)
	{
		foreach (GameSettingsDetailSectionItem detailSection in DetailSections)
		{
			detailSection.IsSelected = detailSection == section;
		}
		Details.SetSelectedSection(section);
	}

	private GameSettingsDetailSectionItem? ResolveDetailSection(string? sectionId)
	{
		if (!string.IsNullOrWhiteSpace(sectionId))
		{
			GameSettingsDetailSectionItem gameSettingsDetailSectionItem = DetailSections.FirstOrDefault((GameSettingsDetailSectionItem item) => string.Equals(item.Id, sectionId, StringComparison.OrdinalIgnoreCase));
			if (gameSettingsDetailSectionItem != null)
			{
				return gameSettingsDetailSectionItem;
			}
		}
		return DetailSections.FirstOrDefault();
	}

	private GameSettingsFileDropEvaluation EvaluateImportDrop(IReadOnlyList<string> paths)
	{
		if (!IsDetailsStep)
		{
			return GameSettingsFileDropEvaluation.Hidden;
		}
		return Details.EvaluateImportDrop(paths);
	}

	private void ApplyImportDropHint(GameSettingsFileDropEvaluation evaluation)
	{
		string message = ((!evaluation.ShouldHandle) ? string.Empty : (evaluation.CanAccept ? Strings.GameSettings_DropReleaseToImportMessage : Strings.GameSettings_DropUnsupportedFileMessage));
		floatingMessageService.ShowDragHint(this, message);
	}

	private void HandleSelectedInstanceChanged()
	{
		if (selectedInstanceNotifier != null)
		{
			selectedInstanceNotifier.PropertyChanged -= SelectedInstance_PropertyChanged;
		}
		selectedInstanceNotifier = InstanceList.SelectedInstance;
		if (selectedInstanceNotifier != null)
		{
			selectedInstanceNotifier.PropertyChanged += SelectedInstance_PropertyChanged;
		}
		Details.SetSelectedInstance(InstanceList.SelectedInstance);
		OnPropertyChanged("PageTitle");
		OnPropertyChanged("PageTitleIconSource");
		if (InstanceList.SelectedInstance == null && IsDetailsStep)
		{
			CurrentStep = GameSettingsPageStep.List;
		}
	}

	private void InstanceList_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
		case "SelectedInstance":
			HandleSelectedInstanceChanged();
			break;
		case "SelectedCategory":
			OnPropertyChanged("PageTitle");
			break;
		case "SearchQuery":
			OnPropertyChanged("TopSearchQuery");
			break;
		case "HasVisibleInstances":
			OnPropertyChanged("IsGameSettingsContentVisible");
			break;
		}
	}

	private void InstanceList_LocalImportRequested()
	{
		LocalImportRequested?.Invoke();
	}

	private void SelectedInstance_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		OnPropertyChanged("PageTitle");
		OnPropertyChanged("PageTitleIconSource");
	}

	private void EditDialog_InstanceRenameStarting()
	{
		Details.SuspendLocalWatchersForInstanceMove();
	}

	private void EditDialog_InstanceRenameFinished()
	{
		Details.ResumeLocalWatchersAfterInstanceMove();
	}

	private void EditDialog_InstanceUpdated(GameInstance instance)
	{
		InstanceList.AddOrUpdate(instance);
		GameSettingsInstanceItem gameSettingsInstanceItem = InstanceList.Find(instance.Id);
		if (gameSettingsInstanceItem != null)
		{
			InstanceList.SelectInstance(gameSettingsInstanceItem);
			CurrentStep = GameSettingsPageStep.Details;
		}
		InstancesChanged?.Invoke(GameSettingsInstancesChangedEventArgs.Updated(instance));
	}

	private void Dialogs_InstanceDeleted(GameSettingsInstanceItem item)
	{
		InstanceList.Remove(item.Instance.Id);
		InstancesChanged?.Invoke(GameSettingsInstancesChangedEventArgs.Deleted(item.Instance.Id));
	}

	private void Details_InstanceSettingsSaved(GameInstance instance)
	{
		InstanceList.AddOrUpdate(instance);
		InstancesChanged?.Invoke(GameSettingsInstancesChangedEventArgs.Updated(instance));
	}

	private void Details_OnlineModInstallRequested(GameInstance instance)
	{
		OnlineModInstallRequested?.Invoke(instance);
	}

	private void Details_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if ((propertyName == "SelectedSection" || propertyName == "CurrentSectionViewModel") ? true : false)
		{
			RaiseTopSearchPropertyChanges();
		}
	}

	private void ModManagement_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "ModSearchQuery")
		{
			OnPropertyChanged("TopSearchQuery");
		}
	}

	private void SaveManagement_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "SaveSearchQuery")
		{
			OnPropertyChanged("TopSearchQuery");
		}
	}

	private void ResourcePackManagement_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "ResourcePackSearchQuery")
		{
			OnPropertyChanged("TopSearchQuery");
		}
	}

	private void ShaderPackManagement_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "ShaderPackSearchQuery")
		{
			OnPropertyChanged("TopSearchQuery");
		}
	}

	private void Backup_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "BackupSearchQuery")
		{
			OnPropertyChanged("TopSearchQuery");
		}
	}

	private void RaiseTopSearchPropertyChanges()
	{
		OnPropertyChanged("IsModManagementDetailsStep");
		OnPropertyChanged("IsSaveManagementDetailsStep");
		OnPropertyChanged("IsResourcePackManagementDetailsStep");
		OnPropertyChanged("IsShaderPackManagementDetailsStep");
		OnPropertyChanged("IsBackupManagementDetailsStep");
		OnPropertyChanged("IsExportDetailsStep");
		OnPropertyChanged("IsTopResourceManagementDetailsStep");
		OnPropertyChanged("IsTopSearchVisible");
		OnPropertyChanged("TopSearchQuery");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnCurrentStepChanged(GameSettingsPageStep value)
	{
		bool preserveFilteredSelection = value == GameSettingsPageStep.Details;
		InstanceList.SetPreserveFilteredSelection(preserveFilteredSelection);
		UpdateDetailsActivation();
		OnPropertyChanged("IsListStep");
		OnPropertyChanged("IsDetailsStep");
		OnPropertyChanged("IsGameSettingsContentVisible");
		OnPropertyChanged("CurrentSecondaryMenuItems");
		OnPropertyChanged("PageTitle");
		OnPropertyChanged("PageTitleIconSource");
		RequestMinecraftDirectorySwitchCommand.NotifyCanExecuteChanged();
		RaiseTopSearchPropertyChanges();
		if (value == GameSettingsPageStep.List)
		{
			InstanceListActivated?.Invoke();
		}
	}
}
