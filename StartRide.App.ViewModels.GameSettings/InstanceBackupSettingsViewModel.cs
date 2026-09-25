using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Download;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class InstanceBackupSettingsViewModel : GameSettingsDetailsSectionViewModelBase
{
	private readonly IGameInstanceService instanceService;

	private readonly IInstanceBackupService backupService;

	private readonly DownloadTasksPageViewModel downloadTasksPage;

	private readonly IStatusService statusService;

	private readonly IInstanceFolderService instanceFolderService;

	private readonly IFilePickerService filePickerService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly ILogger<InstanceBackupSettingsViewModel> logger;

	private readonly HashSet<string> selectedBackupPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private GameInstance? selectedInstance;

	private IReadOnlyList<InstanceBackupItemViewModel> pendingDeleteBackups = Array.Empty<InstanceBackupItemViewModel>();

	private InstanceBackupItemViewModel? pendingRestoreBackup;

	private IReadOnlyList<InstanceBackupItemViewModel> allBackups = Array.Empty<InstanceBackupItemViewModel>();

	private int refreshToken;

	[ObservableProperty]
	private int backupCount;

	[ObservableProperty]
	private string backupDirectory = string.Empty;

	[ObservableProperty]
	private string backupSearchQuery = string.Empty;

	[ObservableProperty]
	private bool isMultiSelectMode;

	[ObservableProperty]
	private int selectedBackupCount;

	[ObservableProperty]
	private bool isLoadingBackups;

	[ObservableProperty]
	private bool hasLoadedBackups;

	[ObservableProperty]
	private bool isCreatingBackup;

	[ObservableProperty]
	private bool isRestoringBackup;

	[ObservableProperty]
	private bool isCreateBackupDialogOpen;

	[ObservableProperty]
	private string newBackupName = string.Empty;

	[ObservableProperty]
	private bool isBackupFailureDialogOpen;

	[ObservableProperty]
	private string backupFailureDialogMessage = string.Empty;

	[ObservableProperty]
	private bool isDeleteBackupDialogOpen;

	[ObservableProperty]
	private bool isRestoreBackupDialogOpen;

	[ObservableProperty]
	private int listEntranceAnimationToken;

	[ObservableProperty]
	private IReadOnlyList<InstanceBackupItemViewModel> visibleBackups = Array.Empty<InstanceBackupItemViewModel>();

	[ObservableProperty]
	private IReadOnlyList<object> visibleBackupListItems = Array.Empty<object>();

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? openBackupFolderCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? toggleMultiSelectModeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? selectAllBackupsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? createBackupNowCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmCreateBackupDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelCreateBackupDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? closeBackupFailureDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<InstanceBackupItemViewModel?>? openBackupLocationCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<InstanceBackupItemViewModel?>? requestDeleteBackupCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestDeleteSelectedBackupsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<InstanceBackupItemViewModel?>? selectBackupCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<InstanceBackupItemViewModel?>? requestRestoreBackupCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelRestoreBackupCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmRestoreBackupCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelDeleteBackupCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmDeleteBackupCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? changeBackupDirectoryCommand;

	public string BackupInfoText => string.Format(Strings.GameSettings_BackupInfoSummaryFormat, BackupCount);

	public override bool UsesFullViewportLayout => true;

	public string BackupDirectoryText
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(BackupDirectory))
			{
				return BackupDirectory;
			}
			return Strings.GameSettings_BackupDirectoryNotSelected;
		}
	}

	public bool CanOpenBackupDirectory => !string.IsNullOrWhiteSpace(BackupDirectory);

	public bool CanCreateBackupNow
	{
		get
		{
			if (selectedInstance != null && !IsCreatingBackup && !IsRestoringBackup)
			{
				return !string.IsNullOrWhiteSpace(BackupDirectory);
			}
			return false;
		}
	}

	public bool CanConfirmCreateBackupDialog
	{
		get
		{
			if (!IsCreatingBackup && !IsRestoringBackup)
			{
				return !string.IsNullOrWhiteSpace(NewBackupName);
			}
			return false;
		}
	}

	public bool CanRestoreBackup
	{
		get
		{
			if (selectedInstance != null && !IsCreatingBackup && !IsRestoringBackup)
			{
				return !string.IsNullOrWhiteSpace(BackupDirectory);
			}
			return false;
		}
	}

	public bool CanShowBackupScrollableContent
	{
		get
		{
			if (selectedInstance != null)
			{
				return HasLoadedBackups;
			}
			return false;
		}
	}

	public bool HasVisibleBackups => VisibleBackups.Count > 0;

	public bool HasSelectedBackups => SelectedBackupCount > 0;

	public bool AreAllVisibleBackupsSelected
	{
		get
		{
			if (HasVisibleBackups)
			{
				return SelectedBackupCount == VisibleBackups.Count;
			}
			return false;
		}
	}

	public bool CanShowBackupLoadingState
	{
		get
		{
			if (selectedInstance != null && IsLoadingBackups)
			{
				return !HasLoadedBackups;
			}
			return false;
		}
	}

	public bool CanShowBackupEmptyState
	{
		get
		{
			if (selectedInstance != null && HasLoadedBackups && !IsLoadingBackups && !HasVisibleBackups)
			{
				return !string.IsNullOrWhiteSpace(BackupDirectory);
			}
			return false;
		}
	}

	public string BackupEmptyMessage
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(BackupSearchQuery))
			{
				return Strings.GameSettings_BackupSearchEmpty;
			}
			return Strings.GameSettings_BackupEmpty;
		}
	}

	public string SelectAllButtonText
	{
		get
		{
			if (!AreAllVisibleBackupsSelected)
			{
				return Strings.GameSettings_BackupSelectAllButton;
			}
			return Strings.GameSettings_BackupCancelSelectAllButton;
		}
	}

	public string DeleteBackupDialogMessage => pendingDeleteBackups.Count switch
	{
		0 => string.Empty, 
		1 => string.Format(Strings.Dialog_DeleteBackupMessageFormat, pendingDeleteBackups[0].Title), 
		_ => string.Format(Strings.Dialog_DeleteMultipleBackupsMessageFormat, pendingDeleteBackups.Count), 
	};

	public string RestoreBackupDialogMessage
	{
		get
		{
			if (pendingRestoreBackup != null)
			{
				return string.Format(Strings.Dialog_RestoreBackupMessageFormat, pendingRestoreBackup.Title);
			}
			return string.Empty;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int BackupCount
	{
		get
		{
			return backupCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(backupCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.BackupCount);
				backupCount = value;
				OnBackupCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.BackupCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string BackupDirectory
	{
		get
		{
			return backupDirectory;
		}
		[MemberNotNull("backupDirectory")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(backupDirectory, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.BackupDirectory);
				backupDirectory = value;
				OnBackupDirectoryChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.BackupDirectory);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string BackupSearchQuery
	{
		get
		{
			return backupSearchQuery;
		}
		[MemberNotNull("backupSearchQuery")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(backupSearchQuery, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.BackupSearchQuery);
				backupSearchQuery = value;
				OnBackupSearchQueryChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.BackupSearchQuery);
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
	public int SelectedBackupCount
	{
		get
		{
			return selectedBackupCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(selectedBackupCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedBackupCount);
				selectedBackupCount = value;
				OnSelectedBackupCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedBackupCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLoadingBackups
	{
		get
		{
			return isLoadingBackups;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLoadingBackups, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLoadingBackups);
				isLoadingBackups = value;
				OnIsLoadingBackupsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLoadingBackups);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasLoadedBackups
	{
		get
		{
			return hasLoadedBackups;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(hasLoadedBackups, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasLoadedBackups);
				hasLoadedBackups = value;
				OnHasLoadedBackupsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasLoadedBackups);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsCreatingBackup
	{
		get
		{
			return isCreatingBackup;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isCreatingBackup, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsCreatingBackup);
				isCreatingBackup = value;
				OnIsCreatingBackupChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsCreatingBackup);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsRestoringBackup
	{
		get
		{
			return isRestoringBackup;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isRestoringBackup, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsRestoringBackup);
				isRestoringBackup = value;
				OnIsRestoringBackupChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsRestoringBackup);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsCreateBackupDialogOpen
	{
		get
		{
			return isCreateBackupDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isCreateBackupDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsCreateBackupDialogOpen);
				isCreateBackupDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsCreateBackupDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string NewBackupName
	{
		get
		{
			return newBackupName;
		}
		[MemberNotNull("newBackupName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(newBackupName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.NewBackupName);
				newBackupName = value;
				OnNewBackupNameChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.NewBackupName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsBackupFailureDialogOpen
	{
		get
		{
			return isBackupFailureDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isBackupFailureDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsBackupFailureDialogOpen);
				isBackupFailureDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsBackupFailureDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string BackupFailureDialogMessage
	{
		get
		{
			return backupFailureDialogMessage;
		}
		[MemberNotNull("backupFailureDialogMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(backupFailureDialogMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.BackupFailureDialogMessage);
				backupFailureDialogMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.BackupFailureDialogMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsDeleteBackupDialogOpen
	{
		get
		{
			return isDeleteBackupDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isDeleteBackupDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDeleteBackupDialogOpen);
				isDeleteBackupDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDeleteBackupDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsRestoreBackupDialogOpen
	{
		get
		{
			return isRestoreBackupDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isRestoreBackupDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsRestoreBackupDialogOpen);
				isRestoreBackupDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsRestoreBackupDialogOpen);
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

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<InstanceBackupItemViewModel> VisibleBackups
	{
		get
		{
			return visibleBackups;
		}
		[MemberNotNull("visibleBackups")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<InstanceBackupItemViewModel>>.Default.Equals(visibleBackups, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VisibleBackups);
				visibleBackups = value;
				OnVisibleBackupsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VisibleBackups);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<object> VisibleBackupListItems
	{
		get
		{
			return visibleBackupListItems;
		}
		[MemberNotNull("visibleBackupListItems")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<object>>.Default.Equals(visibleBackupListItems, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VisibleBackupListItems);
				visibleBackupListItems = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VisibleBackupListItems);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand OpenBackupFolderCommand => openBackupFolderCommand ?? (openBackupFolderCommand = new AsyncRelayCommand(OpenBackupFolderAsync, () => CanOpenBackupDirectory));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ToggleMultiSelectModeCommand => toggleMultiSelectModeCommand ?? (toggleMultiSelectModeCommand = new RelayCommand(ToggleMultiSelectMode));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand SelectAllBackupsCommand => selectAllBackupsCommand ?? (selectAllBackupsCommand = new RelayCommand(SelectAllBackups, CanToggleSelectAllBackups));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CreateBackupNowCommand => createBackupNowCommand ?? (createBackupNowCommand = new RelayCommand(CreateBackupNow, () => CanCreateBackupNow));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmCreateBackupDialogCommand => confirmCreateBackupDialogCommand ?? (confirmCreateBackupDialogCommand = new AsyncRelayCommand(ConfirmCreateBackupDialogAsync, () => CanConfirmCreateBackupDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelCreateBackupDialogCommand => cancelCreateBackupDialogCommand ?? (cancelCreateBackupDialogCommand = new RelayCommand(CancelCreateBackupDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CloseBackupFailureDialogCommand => closeBackupFailureDialogCommand ?? (closeBackupFailureDialogCommand = new RelayCommand(CloseBackupFailureDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<InstanceBackupItemViewModel?> OpenBackupLocationCommand => openBackupLocationCommand ?? (openBackupLocationCommand = new RelayCommand<InstanceBackupItemViewModel>(OpenBackupLocation));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<InstanceBackupItemViewModel?> RequestDeleteBackupCommand => requestDeleteBackupCommand ?? (requestDeleteBackupCommand = new RelayCommand<InstanceBackupItemViewModel>(RequestDeleteBackup));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestDeleteSelectedBackupsCommand => requestDeleteSelectedBackupsCommand ?? (requestDeleteSelectedBackupsCommand = new RelayCommand(RequestDeleteSelectedBackups, () => HasSelectedBackups));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<InstanceBackupItemViewModel?> SelectBackupCommand => selectBackupCommand ?? (selectBackupCommand = new RelayCommand<InstanceBackupItemViewModel>(SelectBackup));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<InstanceBackupItemViewModel?> RequestRestoreBackupCommand => requestRestoreBackupCommand ?? (requestRestoreBackupCommand = new RelayCommand<InstanceBackupItemViewModel>(RequestRestoreBackup, (InstanceBackupItemViewModel? _) => CanRestoreBackup));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelRestoreBackupCommand => cancelRestoreBackupCommand ?? (cancelRestoreBackupCommand = new RelayCommand(CancelRestoreBackup));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmRestoreBackupCommand => confirmRestoreBackupCommand ?? (confirmRestoreBackupCommand = new AsyncRelayCommand(ConfirmRestoreBackupAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelDeleteBackupCommand => cancelDeleteBackupCommand ?? (cancelDeleteBackupCommand = new RelayCommand(CancelDeleteBackup));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmDeleteBackupCommand => confirmDeleteBackupCommand ?? (confirmDeleteBackupCommand = new AsyncRelayCommand(ConfirmDeleteBackupAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ChangeBackupDirectoryCommand => changeBackupDirectoryCommand ?? (changeBackupDirectoryCommand = new AsyncRelayCommand(ChangeBackupDirectoryAsync));

	[RelayCommand(CanExecute = "CanOpenBackupDirectory")]
	private async Task OpenBackupFolderAsync()
	{
		if (selectedInstance == null || string.IsNullOrWhiteSpace(BackupDirectory))
		{
			return;
		}
		try
		{
			string text = (BackupDirectory = await backupService.EnsureBackupDirectoryAsync(BackupDirectory));
			selectedInstance.BackupDirectory = text;
			logger.LogDebug("Opening instance backup folder. InstanceId={InstanceId} BackupDirectory={BackupDirectory}", selectedInstance.Id, text);
			if (!instanceFolderService.TryOpen(text))
			{
				logger.LogWarning("Failed to open instance backup folder. InstanceId={InstanceId} BackupDirectory={BackupDirectory}", selectedInstance.Id, text);
				statusService.Report(Strings.Status_OpenBackupDirectoryFailed);
			}
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to prepare instance backup folder for opening. InstanceId={InstanceId}", selectedInstance.Id);
			statusService.Report(Strings.Status_OpenBackupDirectoryFailed);
		}
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

	[RelayCommand(CanExecute = "CanToggleSelectAllBackups")]
	private void SelectAllBackups()
	{
		if (AreAllVisibleBackupsSelected)
		{
			ClearVisibleSelections();
			selectedBackupPaths.Clear();
			UpdateSelectedBackupState();
			return;
		}
		foreach (InstanceBackupItemViewModel visibleBackup in VisibleBackups)
		{
			visibleBackup.IsSelected = true;
			selectedBackupPaths.Add(visibleBackup.FullPath);
		}
		UpdateSelectedBackupState();
	}

	[RelayCommand(CanExecute = "CanCreateBackupNow")]
	private void CreateBackupNow()
	{
		if (CanCreateBackupNow)
		{
			NewBackupName = string.Format(Strings.GameSettings_BackupDefaultNameFormat, DateTimeOffset.Now.ToString("yyyy-MM-dd HH-mm"));
			IsCreateBackupDialogOpen = true;
			logger.LogDebug("Instance backup naming dialog opened. InstanceId={InstanceId}", selectedInstance?.Id ?? "<none>");
		}
	}

	[RelayCommand(CanExecute = "CanConfirmCreateBackupDialog")]
	private Task ConfirmCreateBackupDialogAsync()
	{
		Task task = ConfirmCreateBackupDialogCoreAsync();
		downloadTasksPage.TrackBackgroundTask(task);
		return task;
	}

	private async Task ConfirmCreateBackupDialogCoreAsync()
	{
		if (selectedInstance == null || string.IsNullOrWhiteSpace(BackupDirectory) || string.IsNullOrWhiteSpace(NewBackupName))
		{
			return;
		}
		GameInstance instance = selectedInstance;
		string text = BackupDirectory;
		string backupName = NewBackupName.Trim();
		IsCreateBackupDialogOpen = false;
		IsCreatingBackup = true;
		floatingMessageService.Show(Strings.Status_BackupCreating);
		try
		{
			await backupService.CreateBackupAsync(instance, text, backupName);
			await RefreshBackupsAsync();
			floatingMessageService.Show(Strings.Status_BackupCreated);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to create instance backup from backup settings page. InstanceId={InstanceId}", instance.Id);
			BackupFailureDialogMessage = string.Format(Strings.Dialog_BackupCreateFailedMessageFormat, GetFriendlyBackupFailureMessage(exception), GetExceptionSummary(exception));
			IsBackupFailureDialogOpen = true;
		}
		finally
		{
			IsCreatingBackup = false;
		}
	}

	[RelayCommand]
	private void CancelCreateBackupDialog()
	{
		IsCreateBackupDialogOpen = false;
	}

	[RelayCommand]
	private void CloseBackupFailureDialog()
	{
		IsBackupFailureDialogOpen = false;
	}

	[RelayCommand]
	private void OpenBackupLocation(InstanceBackupItemViewModel? backup)
	{
		if (backup == null)
		{
			return;
		}
		try
		{
			if (!instanceFolderService.TryRevealFile(backup.FullPath))
			{
				logger.LogWarning("Failed to reveal instance backup file. InstanceId={InstanceId} BackupFile={BackupFile}", selectedInstance?.Id ?? "<none>", backup.FullPath);
				statusService.Report(Strings.Status_OpenBackupLocationFailed);
			}
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to reveal instance backup file. InstanceId={InstanceId} BackupFile={BackupFile}", selectedInstance?.Id ?? "<none>", backup.FullPath);
			statusService.Report(Strings.Status_OpenBackupLocationFailed);
		}
	}

	[RelayCommand]
	private void RequestDeleteBackup(InstanceBackupItemViewModel? backup)
	{
		if (backup != null)
		{
			pendingDeleteBackups = new global::_003C_003Ez__ReadOnlySingleElementList<InstanceBackupItemViewModel>(backup);
			OnPropertyChanged("DeleteBackupDialogMessage");
			IsDeleteBackupDialogOpen = true;
		}
	}

	[RelayCommand(CanExecute = "HasSelectedBackups")]
	private void RequestDeleteSelectedBackups()
	{
		IReadOnlyList<InstanceBackupItemViewModel> selectedVisibleBackups = GetSelectedVisibleBackups();
		if (selectedVisibleBackups.Count != 0)
		{
			pendingDeleteBackups = selectedVisibleBackups;
			OnPropertyChanged("DeleteBackupDialogMessage");
			IsDeleteBackupDialogOpen = true;
		}
	}

	[RelayCommand]
	private void SelectBackup(InstanceBackupItemViewModel? backup)
	{
		if (backup != null && IsMultiSelectMode)
		{
			if (backup.IsSelected = !backup.IsSelected)
			{
				selectedBackupPaths.Add(backup.FullPath);
			}
			else
			{
				selectedBackupPaths.Remove(backup.FullPath);
			}
			UpdateSelectedBackupState();
		}
	}

	[RelayCommand(CanExecute = "CanRestoreBackup")]
	private void RequestRestoreBackup(InstanceBackupItemViewModel? backup)
	{
		if (backup != null)
		{
			pendingRestoreBackup = backup;
			OnPropertyChanged("RestoreBackupDialogMessage");
			IsRestoreBackupDialogOpen = true;
		}
	}

	[RelayCommand]
	private void CancelRestoreBackup()
	{
		pendingRestoreBackup = null;
		IsRestoreBackupDialogOpen = false;
		OnPropertyChanged("RestoreBackupDialogMessage");
	}

	[RelayCommand]
	private Task ConfirmRestoreBackupAsync()
	{
		Task task = ConfirmRestoreBackupCoreAsync();
		downloadTasksPage.TrackBackgroundTask(task);
		return task;
	}

	private async Task ConfirmRestoreBackupCoreAsync()
	{
		if (selectedInstance == null || pendingRestoreBackup == null || string.IsNullOrWhiteSpace(BackupDirectory))
		{
			return;
		}
		GameInstance instance = selectedInstance;
		string backupDirectory = BackupDirectory;
		InstanceBackupItemViewModel backup = pendingRestoreBackup;
		pendingRestoreBackup = null;
		IsRestoreBackupDialogOpen = false;
		OnPropertyChanged("RestoreBackupDialogMessage");
		IsRestoringBackup = true;
		floatingMessageService.Show(Strings.Status_BackupRestoring);
		try
		{
			string backupName = string.Format(Strings.GameSettings_BackupPreRestoreNameFormat, DateTimeOffset.Now.ToString("yyyy-MM-dd HH-mm"));
			await backupService.CreateBackupAsync(instance, backupDirectory, backupName);
			await backupService.RestoreBackupAsync(instance, backupDirectory, backup.FullPath);
			await RefreshBackupsAsync();
			floatingMessageService.Show(Strings.Status_BackupRestored);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to restore instance backup from backup settings page. InstanceId={InstanceId} BackupFile={BackupFile}", instance.Id, backup.FullPath);
			statusService.Report(Strings.Status_BackupRestoreFailed);
		}
		finally
		{
			IsRestoringBackup = false;
		}
	}

	[RelayCommand]
	private void CancelDeleteBackup()
	{
		pendingDeleteBackups = Array.Empty<InstanceBackupItemViewModel>();
		IsDeleteBackupDialogOpen = false;
		OnPropertyChanged("DeleteBackupDialogMessage");
	}

	[RelayCommand]
	private Task ConfirmDeleteBackupAsync()
	{
		Task task = ConfirmDeleteBackupCoreAsync();
		downloadTasksPage.TrackBackgroundTask(task);
		return task;
	}

	private async Task ConfirmDeleteBackupCoreAsync()
	{
		if (pendingDeleteBackups.Count == 0 || string.IsNullOrWhiteSpace(BackupDirectory))
		{
			return;
		}
		string instanceId = selectedInstance?.Id ?? "<none>";
		string backupDirectory = BackupDirectory;
		IReadOnlyList<InstanceBackupItemViewModel> backups = pendingDeleteBackups;
		pendingDeleteBackups = Array.Empty<InstanceBackupItemViewModel>();
		IsDeleteBackupDialogOpen = false;
		OnPropertyChanged("DeleteBackupDialogMessage");
		try
		{
			foreach (InstanceBackupItemViewModel item in backups)
			{
				await backupService.DeleteBackupAsync(backupDirectory, item.FullPath);
			}
			if (backups.Count > 1)
			{
				ExitMultiSelectMode();
			}
			await RefreshBackupsAsync();
			statusService.Report((backups.Count == 1) ? Strings.Status_BackupDeleted : string.Format(Strings.Status_SelectedBackupsDeletedFormat, backups.Count));
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to delete instance backup from backup settings page. InstanceId={InstanceId} BackupCount={BackupCount}", instanceId, backups.Count);
			statusService.Report(Strings.Status_BackupDeleteFailed);
		}
	}

	public InstanceBackupSettingsViewModel(GameSettingsDetailsViewModel parent, IGameInstanceService instanceService, IInstanceBackupService backupService, DownloadTasksPageViewModel downloadTasksPage, IStatusService statusService, IInstanceFolderService instanceFolderService, IFilePickerService filePickerService, IFloatingMessageService floatingMessageService, ILogger<InstanceBackupSettingsViewModel>? logger = null)
		: base(parent)
	{
		this.instanceService = instanceService;
		this.backupService = backupService;
		this.downloadTasksPage = downloadTasksPage;
		this.statusService = statusService;
		this.instanceFolderService = instanceFolderService;
		this.filePickerService = filePickerService;
		this.floatingMessageService = floatingMessageService;
		this.logger = logger ?? NullLogger<InstanceBackupSettingsViewModel>.Instance;
	}

	public override void OnSelectedInstanceChanged(GameInstance? instance)
	{
		selectedInstance = instance;
		pendingDeleteBackups = Array.Empty<InstanceBackupItemViewModel>();
		pendingRestoreBackup = null;
		selectedBackupPaths.Clear();
		BackupDirectory = instance?.BackupDirectory ?? string.Empty;
		BackupCount = 0;
		SelectedBackupCount = 0;
		allBackups = Array.Empty<InstanceBackupItemViewModel>();
		VisibleBackups = Array.Empty<InstanceBackupItemViewModel>();
		HasLoadedBackups = false;
		IsLoadingBackups = false;
		IsCreateBackupDialogOpen = false;
		IsBackupFailureDialogOpen = false;
		IsDeleteBackupDialogOpen = false;
		IsRestoreBackupDialogOpen = false;
		NewBackupName = string.Empty;
		IsMultiSelectMode = false;
		RefreshVisibleBackupItems(playEntranceAnimation: false);
		OnPropertyChanged("CanShowBackupScrollableContent");
		OpenBackupFolderCommand.NotifyCanExecuteChanged();
		CreateBackupNowCommand.NotifyCanExecuteChanged();
		RequestRestoreBackupCommand.NotifyCanExecuteChanged();
		RefreshBackupsAsync();
	}

	public override Task OnSectionActivatedAsync()
	{
		return RefreshBackupsAsync();
	}

	private static string GetFriendlyBackupFailureMessage(Exception exception)
	{
		if (exception is InstanceBackupException { Reason: var reason })
		{
			return reason switch
			{
				InstanceBackupFailureReason.BackupDirectoryInsideInstance => Strings.BackupFailure_BackupDirectoryInsideInstance, 
				InstanceBackupFailureReason.InstanceDirectoryNotFound => Strings.BackupFailure_InstanceDirectoryMissing, 
				_ => Strings.BackupFailure_Generic, 
			};
		}
		return Strings.BackupFailure_Generic;
	}

	private static string GetExceptionSummary(Exception exception)
	{
		return exception.GetType().Name + ": " + exception.Message;
	}

	private async Task RefreshBackupsAsync()
	{
		int token = ++refreshToken;
		string directory = BackupDirectory;
		bool isInitialProjection = !HasLoadedBackups;
		if (string.IsNullOrWhiteSpace(directory))
		{
			allBackups = Array.Empty<InstanceBackupItemViewModel>();
			VisibleBackups = Array.Empty<InstanceBackupItemViewModel>();
			selectedBackupPaths.Clear();
			SelectedBackupCount = 0;
			BackupCount = 0;
			IsLoadingBackups = false;
			RefreshVisibleBackupItems(!isInitialProjection);
			if (isInitialProjection)
			{
				PublishInitialProjectionReady();
			}
			NotifyBackupListStateChanged();
			return;
		}
		IsLoadingBackups = true;
		try
		{
			IReadOnlyList<InstanceBackupRecord> source = await backupService.GetBackupsAsync(directory);
			if (token == refreshToken && string.Equals(directory, BackupDirectory, StringComparison.OrdinalIgnoreCase))
			{
				allBackups = source.Select((InstanceBackupRecord backup) => new InstanceBackupItemViewModel(backup)).ToArray();
				BackupCount = allBackups.Count;
				RefreshVisibleBackupItems(!isInitialProjection);
			}
		}
		catch (Exception exception)
		{
			if (token == refreshToken)
			{
				logger.LogWarning(exception, "Failed to refresh instance backups. InstanceId={InstanceId} BackupDirectory={BackupDirectory}", selectedInstance?.Id ?? "<none>", directory);
				allBackups = Array.Empty<InstanceBackupItemViewModel>();
				VisibleBackups = Array.Empty<InstanceBackupItemViewModel>();
				selectedBackupPaths.Clear();
				SelectedBackupCount = 0;
				BackupCount = 0;
				RefreshVisibleBackupItems(!isInitialProjection);
				statusService.Report(Strings.Status_LoadBackupsFailed);
			}
		}
		finally
		{
			if (token == refreshToken)
			{
				IsLoadingBackups = false;
				if (isInitialProjection)
				{
					PublishInitialProjectionReady();
				}
				NotifyBackupListStateChanged();
			}
		}
	}

	private void RefreshVisibleBackupItems(bool playEntranceAnimation = true)
	{
		if (selectedInstance == null)
		{
			VisibleBackups = Array.Empty<InstanceBackupItemViewModel>();
			VisibleBackupListItems = Array.Empty<object>();
			selectedBackupPaths.Clear();
			UpdateSelectedBackupState();
			NotifyBackupListStateChanged();
			return;
		}
		string query = BackupSearchQuery.Trim();
		IReadOnlyList<InstanceBackupItemViewModel> readOnlyList2;
		if (!string.IsNullOrWhiteSpace(query))
		{
			IReadOnlyList<InstanceBackupItemViewModel> readOnlyList = allBackups.Where((InstanceBackupItemViewModel backup) => backup.Matches(query)).ToArray();
			readOnlyList2 = readOnlyList;
		}
		else
		{
			readOnlyList2 = allBackups;
		}
		VisibleBackups = readOnlyList2;
		if (IsMultiSelectMode)
		{
			selectedBackupPaths.IntersectWith(VisibleBackups.Select((InstanceBackupItemViewModel backup) => backup.FullPath));
		}
		foreach (InstanceBackupItemViewModel allBackup in allBackups)
		{
			allBackup.IsSelected = IsMultiSelectMode && selectedBackupPaths.Contains(allBackup.FullPath);
		}
		UpdateSelectedBackupState();
		bool flag = VisibleBackups.Count > 0;
		object[] array = new object[VisibleBackups.Count + ((!flag) ? 1 : 2)];
		array[0] = BackupManagementInfoPanelItem.Instance;
		if (flag)
		{
			array[1] = BackupManagementListSectionItem.Instance;
		}
		for (int num = 0; num < VisibleBackups.Count; num++)
		{
			array[num + ((!flag) ? 1 : 2)] = VisibleBackups[num];
		}
		VisibleBackupListItems = array;
		if (playEntranceAnimation && HasLoadedBackups && selectedInstance != null)
		{
			ListEntranceAnimationToken++;
		}
		NotifyBackupListStateChanged();
	}

	private void PublishInitialProjectionReady()
	{
		HasLoadedBackups = true;
		if (selectedInstance != null)
		{
			ListEntranceAnimationToken++;
		}
	}

	private void NotifyBackupListStateChanged()
	{
		OnPropertyChanged("HasVisibleBackups");
		OnPropertyChanged("AreAllVisibleBackupsSelected");
		OnPropertyChanged("SelectAllButtonText");
		OnPropertyChanged("CanShowBackupLoadingState");
		OnPropertyChanged("CanShowBackupEmptyState");
		OnPropertyChanged("BackupEmptyMessage");
		SelectAllBackupsCommand.NotifyCanExecuteChanged();
	}

	private bool CanToggleSelectAllBackups()
	{
		if (IsMultiSelectMode)
		{
			return HasVisibleBackups;
		}
		return false;
	}

	private void EnterMultiSelectMode()
	{
		IsMultiSelectMode = true;
		selectedBackupPaths.Clear();
		ClearVisibleSelections();
		UpdateSelectedBackupState();
	}

	private void ExitMultiSelectMode()
	{
		IsMultiSelectMode = false;
		selectedBackupPaths.Clear();
		ClearVisibleSelections();
		UpdateSelectedBackupState();
	}

	private void ClearVisibleSelections()
	{
		foreach (InstanceBackupItemViewModel visibleBackup in VisibleBackups)
		{
			visibleBackup.IsSelected = false;
		}
	}

	private IReadOnlyList<InstanceBackupItemViewModel> GetSelectedVisibleBackups()
	{
		return VisibleBackups.Where((InstanceBackupItemViewModel backup) => selectedBackupPaths.Contains(backup.FullPath)).ToArray();
	}

	private void UpdateSelectedBackupState()
	{
		SelectedBackupCount = VisibleBackups.Count((InstanceBackupItemViewModel backup) => backup.IsSelected);
	}

	[RelayCommand]
	private async Task ChangeBackupDirectoryAsync()
	{
		if (selectedInstance == null)
		{
			return;
		}
		string value = filePickerService.PickFolder(Strings.FilePicker_BackupDirectoryTitle, string.IsNullOrWhiteSpace(BackupDirectory) ? null : BackupDirectory);
		if (string.IsNullOrWhiteSpace(value))
		{
			return;
		}
		string normalizedDirectory;
		try
		{
			normalizedDirectory = await backupService.EnsureBackupDirectoryAsync(value);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to change instance backup directory. InstanceId={InstanceId}", selectedInstance.Id);
			statusService.Report(Strings.Status_BackupDirectoryChangeFailed);
			return;
		}
		if (!string.Equals(BackupDirectory, normalizedDirectory, StringComparison.OrdinalIgnoreCase))
		{
			string originalDirectory = selectedInstance.BackupDirectory;
			try
			{
				selectedInstance.BackupDirectory = normalizedDirectory;
				await instanceService.SaveInstanceAsync(selectedInstance);
				BackupDirectory = normalizedDirectory;
				base.Parent.NotifyInstanceSettingsSaved(selectedInstance);
			}
			catch (Exception exception2)
			{
				selectedInstance.BackupDirectory = originalDirectory;
				BackupDirectory = originalDirectory;
				logger.LogError(exception2, "Failed to save instance backup directory. InstanceId={InstanceId}", selectedInstance.Id);
				statusService.Report(Strings.Status_BackupDirectoryChangeFailed);
				return;
			}
			statusService.Report(Strings.Status_BackupDirectoryChanged);
			await RefreshBackupsAsync();
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnBackupCountChanged(int value)
	{
		OnPropertyChanged("BackupInfoText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnBackupDirectoryChanged(string value)
	{
		OnPropertyChanged("BackupDirectoryText");
		OnPropertyChanged("CanOpenBackupDirectory");
		OnPropertyChanged("CanCreateBackupNow");
		OnPropertyChanged("CanRestoreBackup");
		OpenBackupFolderCommand.NotifyCanExecuteChanged();
		CreateBackupNowCommand.NotifyCanExecuteChanged();
		RequestRestoreBackupCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnBackupSearchQueryChanged(string value)
	{
		RefreshVisibleBackupItems();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsMultiSelectModeChanged(bool value)
	{
		OnPropertyChanged("AreAllVisibleBackupsSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllBackupsCommand.NotifyCanExecuteChanged();
		RequestDeleteSelectedBackupsCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedBackupCountChanged(int value)
	{
		OnPropertyChanged("HasSelectedBackups");
		OnPropertyChanged("AreAllVisibleBackupsSelected");
		OnPropertyChanged("SelectAllButtonText");
		SelectAllBackupsCommand.NotifyCanExecuteChanged();
		RequestDeleteSelectedBackupsCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsLoadingBackupsChanged(bool value)
	{
		NotifyBackupListStateChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnHasLoadedBackupsChanged(bool value)
	{
		OnPropertyChanged("CanShowBackupScrollableContent");
		NotifyBackupListStateChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsCreatingBackupChanged(bool value)
	{
		OnPropertyChanged("CanCreateBackupNow");
		OnPropertyChanged("CanConfirmCreateBackupDialog");
		OnPropertyChanged("CanRestoreBackup");
		CreateBackupNowCommand.NotifyCanExecuteChanged();
		ConfirmCreateBackupDialogCommand.NotifyCanExecuteChanged();
		RequestRestoreBackupCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsRestoringBackupChanged(bool value)
	{
		OnPropertyChanged("CanCreateBackupNow");
		OnPropertyChanged("CanConfirmCreateBackupDialog");
		OnPropertyChanged("CanRestoreBackup");
		CreateBackupNowCommand.NotifyCanExecuteChanged();
		ConfirmCreateBackupDialogCommand.NotifyCanExecuteChanged();
		RequestRestoreBackupCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnNewBackupNameChanged(string value)
	{
		OnPropertyChanged("CanConfirmCreateBackupDialog");
		ConfirmCreateBackupDialogCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnVisibleBackupsChanged(IReadOnlyList<InstanceBackupItemViewModel> value)
	{
		NotifyBackupListStateChanged();
	}
}
