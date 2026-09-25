using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class GameSettingsDialogsViewModel : ObservableObject
{
	private enum ContentKind
	{
		Mods,
		Saves,
		ResourcePacks,
		ShaderPacks
	}

	private sealed record PendingContentDeletion(ContentKind Kind, IReadOnlyList<string> FullPaths, IReadOnlyList<string> Titles);

	private readonly IGameInstanceService instanceService;

	private readonly IStatusService statusService;

	private readonly GameSettingsDetailsViewModel details;

	private readonly ILogger<GameSettingsDialogsViewModel> logger;

	[ObservableProperty]
	private bool isDeleteInstanceDialogOpen;

	[ObservableProperty]
	private GameSettingsInstanceItem? instancePendingDelete;

	[ObservableProperty]
	private bool isDeleteInstanceDialogBusy;

	[ObservableProperty]
	private bool hasDeleteInstanceDialogError;

	[ObservableProperty]
	private bool isDeleteContentDialogOpen;

	private PendingContentDeletion? pendingContentDeletion;

	[ObservableProperty]
	private bool isReplaceModImportDialogOpen;

	[ObservableProperty]
	private ModImportConflictRequest? pendingModImportConflict;

	[ObservableProperty]
	private bool isInvalidImportDialogOpen;

	[ObservableProperty]
	private string invalidImportDialogMessage = string.Empty;

	[ObservableProperty]
	private string invalidImportDialogTitle = Strings.Dialog_InvalidSaveImportTitle;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<GameSettingsInstanceItem>? openDeleteInstanceCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelDeleteInstanceDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmDeleteInstanceDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelDeleteContentDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmDeleteContentDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelReplaceModImportDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? confirmReplaceModImportDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? closeInvalidImportDialogCommand;

	public string DeleteInstanceDialogTitle
	{
		get
		{
			if (!IsDeleteInstanceDialogBusy)
			{
				if (!HasDeleteInstanceDialogError)
				{
					return Strings.Dialog_DeleteInstanceTitle;
				}
				return Strings.Dialog_DeleteInstanceFailedTitle;
			}
			return Strings.Dialog_DeleteInstanceBusyTitle;
		}
	}

	public string DeleteInstanceDialogMessage
	{
		get
		{
			if (InstancePendingDelete != null)
			{
				if (!IsDeleteInstanceDialogBusy)
				{
					if (!HasDeleteInstanceDialogError)
					{
						return string.Format(Strings.Dialog_DeleteInstanceMessageFormat, InstancePendingDelete.Name);
					}
					return Strings.Status_DeleteInstanceFailed;
				}
				return string.Format(Strings.Dialog_DeleteInstanceBusyMessageFormat, InstancePendingDelete.Name);
			}
			return string.Empty;
		}
	}

	public string DeleteInstanceDialogActionText
	{
		get
		{
			if (!IsDeleteInstanceDialogBusy)
			{
				if (!HasDeleteInstanceDialogError)
				{
					return Strings.Delete_Button;
				}
				return Strings.Retry_Button;
			}
			return Strings.Dialog_DeleteInstanceBusyTitle;
		}
	}

	public bool CanShowDeleteInstanceCancelButton => !IsDeleteInstanceDialogBusy;

	private bool CanCancelDeleteInstanceDialog => !IsDeleteInstanceDialogBusy;

	private bool CanConfirmDeleteInstanceDialog
	{
		get
		{
			if (!IsDeleteInstanceDialogBusy)
			{
				return InstancePendingDelete != null;
			}
			return false;
		}
	}

	public string DeleteContentDialogTitle => pendingContentDeletion?.Kind switch
	{
		ContentKind.Saves => Strings.Dialog_DeleteSavesTitle, 
		ContentKind.ResourcePacks => Strings.Dialog_DeleteResourcePacksTitle, 
		ContentKind.ShaderPacks => Strings.Dialog_DeleteShaderPacksTitle, 
		_ => Strings.Dialog_DeleteModsTitle, 
	};

	public string DeleteContentDialogMessage
	{
		get
		{
			if ((object)pendingContentDeletion != null)
			{
				return FormatDeleteMessage(pendingContentDeletion);
			}
			return string.Empty;
		}
	}

	public string ReplaceModImportDialogMessage
	{
		get
		{
			if ((object)PendingModImportConflict != null)
			{
				return string.Format(Strings.Dialog_ReplaceModImportMessageFormat, PendingModImportConflict.FileName);
			}
			return string.Empty;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsDeleteInstanceDialogOpen
	{
		get
		{
			return isDeleteInstanceDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isDeleteInstanceDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDeleteInstanceDialogOpen);
				isDeleteInstanceDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDeleteInstanceDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public GameSettingsInstanceItem? InstancePendingDelete
	{
		get
		{
			return instancePendingDelete;
		}
		set
		{
			if (!EqualityComparer<GameSettingsInstanceItem>.Default.Equals(instancePendingDelete, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InstancePendingDelete);
				instancePendingDelete = value;
				OnInstancePendingDeleteChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InstancePendingDelete);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsDeleteInstanceDialogBusy
	{
		get
		{
			return isDeleteInstanceDialogBusy;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isDeleteInstanceDialogBusy, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDeleteInstanceDialogBusy);
				isDeleteInstanceDialogBusy = value;
				OnIsDeleteInstanceDialogBusyChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDeleteInstanceDialogBusy);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasDeleteInstanceDialogError
	{
		get
		{
			return hasDeleteInstanceDialogError;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(hasDeleteInstanceDialogError, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasDeleteInstanceDialogError);
				hasDeleteInstanceDialogError = value;
				OnHasDeleteInstanceDialogErrorChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasDeleteInstanceDialogError);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsDeleteContentDialogOpen
	{
		get
		{
			return isDeleteContentDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isDeleteContentDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDeleteContentDialogOpen);
				isDeleteContentDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDeleteContentDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsReplaceModImportDialogOpen
	{
		get
		{
			return isReplaceModImportDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isReplaceModImportDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsReplaceModImportDialogOpen);
				isReplaceModImportDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsReplaceModImportDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ModImportConflictRequest? PendingModImportConflict
	{
		get
		{
			return pendingModImportConflict;
		}
		set
		{
			if (!EqualityComparer<ModImportConflictRequest>.Default.Equals(pendingModImportConflict, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PendingModImportConflict);
				pendingModImportConflict = value;
				OnPendingModImportConflictChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PendingModImportConflict);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsInvalidImportDialogOpen
	{
		get
		{
			return isInvalidImportDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isInvalidImportDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsInvalidImportDialogOpen);
				isInvalidImportDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsInvalidImportDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string InvalidImportDialogMessage
	{
		get
		{
			return invalidImportDialogMessage;
		}
		[MemberNotNull("invalidImportDialogMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(invalidImportDialogMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InvalidImportDialogMessage);
				invalidImportDialogMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InvalidImportDialogMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string InvalidImportDialogTitle
	{
		get
		{
			return invalidImportDialogTitle;
		}
		[MemberNotNull("invalidImportDialogTitle")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(invalidImportDialogTitle, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InvalidImportDialogTitle);
				invalidImportDialogTitle = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InvalidImportDialogTitle);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<GameSettingsInstanceItem> OpenDeleteInstanceCommand => openDeleteInstanceCommand ?? (openDeleteInstanceCommand = new RelayCommand<GameSettingsInstanceItem>(OpenDeleteInstance));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelDeleteInstanceDialogCommand => cancelDeleteInstanceDialogCommand ?? (cancelDeleteInstanceDialogCommand = new RelayCommand(CancelDeleteInstanceDialog, () => CanCancelDeleteInstanceDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmDeleteInstanceDialogCommand => confirmDeleteInstanceDialogCommand ?? (confirmDeleteInstanceDialogCommand = new AsyncRelayCommand(ConfirmDeleteInstanceDialogAsync, () => CanConfirmDeleteInstanceDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelDeleteContentDialogCommand => cancelDeleteContentDialogCommand ?? (cancelDeleteContentDialogCommand = new RelayCommand(CancelDeleteContentDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmDeleteContentDialogCommand => confirmDeleteContentDialogCommand ?? (confirmDeleteContentDialogCommand = new AsyncRelayCommand(ConfirmDeleteContentDialogAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelReplaceModImportDialogCommand => cancelReplaceModImportDialogCommand ?? (cancelReplaceModImportDialogCommand = new RelayCommand(CancelReplaceModImportDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ConfirmReplaceModImportDialogCommand => confirmReplaceModImportDialogCommand ?? (confirmReplaceModImportDialogCommand = new RelayCommand(ConfirmReplaceModImportDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CloseInvalidImportDialogCommand => closeInvalidImportDialogCommand ?? (closeInvalidImportDialogCommand = new RelayCommand(CloseInvalidImportDialog));

	public event Action<GameSettingsInstanceItem>? InstanceDeleted;

	public GameSettingsDialogsViewModel(IGameInstanceService instanceService, IStatusService statusService, GameSettingsDetailsViewModel details, ILogger<GameSettingsDialogsViewModel>? logger = null)
	{
		this.instanceService = instanceService;
		this.statusService = statusService;
		this.details = details;
		this.logger = logger ?? NullLogger<GameSettingsDialogsViewModel>.Instance;
	}

	[RelayCommand]
	public void OpenDeleteInstance(GameSettingsInstanceItem instance)
	{
		if (!IsDeleteInstanceDialogBusy)
		{
			IsDeleteInstanceDialogBusy = false;
			HasDeleteInstanceDialogError = false;
			InstancePendingDelete = instance;
			IsDeleteInstanceDialogOpen = true;
		}
	}

	public void OpenDeleteMods(ModDeleteRequest request)
	{
		OpenContentDeletion(ContentKind.Mods, request.FullPaths, request.Titles);
	}

	public void OpenDeleteSaves(SaveDeleteRequest request)
	{
		OpenContentDeletion(ContentKind.Saves, request.FullPaths, request.Titles);
	}

	public void OpenDeleteResourcePacks(ResourcePackDeleteRequest request)
	{
		OpenContentDeletion(ContentKind.ResourcePacks, request.FullPaths, request.Titles);
	}

	public void OpenDeleteShaderPacks(ShaderPackDeleteRequest request)
	{
		OpenContentDeletion(ContentKind.ShaderPacks, request.FullPaths, request.Titles);
	}

	public void OpenModImportConflict(ModImportConflictRequest request)
	{
		PendingModImportConflict = request;
		IsReplaceModImportDialogOpen = true;
	}

	public void OpenSaveImportFailure(SaveImportFailureRequest request)
	{
		OpenImportFailure(Strings.Dialog_InvalidSaveImportTitle, request.Message);
	}

	public void OpenResourcePackImportFailure(ResourcePackImportFailureRequest request)
	{
		OpenImportFailure(Strings.Dialog_InvalidResourcePackImportTitle, request.Message);
	}

	public void OpenShaderPackImportFailure(ShaderPackImportFailureRequest request)
	{
		OpenImportFailure(Strings.Dialog_InvalidShaderPackImportTitle, request.Message);
	}

	[RelayCommand(CanExecute = "CanCancelDeleteInstanceDialog")]
	private void CancelDeleteInstanceDialog()
	{
		if (!IsDeleteInstanceDialogBusy)
		{
			IsDeleteInstanceDialogOpen = false;
			InstancePendingDelete = null;
			HasDeleteInstanceDialogError = false;
		}
	}

	[RelayCommand(CanExecute = "CanConfirmDeleteInstanceDialog")]
	private async Task ConfirmDeleteInstanceDialogAsync()
	{
		if (IsDeleteInstanceDialogBusy || InstancePendingDelete == null)
		{
			return;
		}
		GameSettingsInstanceItem pending = InstancePendingDelete;
		HasDeleteInstanceDialogError = false;
		IsDeleteInstanceDialogBusy = true;
		details.SuspendLocalWatchersForInstanceMove();
		bool deletionCommitted = false;
		try
		{
			if (!(await instanceService.DeleteInstanceAsync(pending.Instance.Id)))
			{
				statusService.Report(Strings.Status_DeleteInstanceFailed);
				HasDeleteInstanceDialogError = true;
				return;
			}
			deletionCommitted = true;
			details.ClearSelectedInstanceIf(pending.Instance.Id);
			statusService.Report(string.Format(Strings.Status_InstanceDeletedFormat, pending.Name));
			InstanceDeleted?.Invoke(pending);
			IsDeleteInstanceDialogOpen = false;
			InstancePendingDelete = null;
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to delete game instance. InstanceId={InstanceId}", pending.Instance.Id);
			statusService.Report(Strings.Status_DeleteInstanceFailed);
			HasDeleteInstanceDialogError = true;
		}
		finally
		{
			IsDeleteInstanceDialogBusy = false;
			details.ResumeLocalWatchersAfterInstanceMove(!deletionCommitted);
		}
	}

	[RelayCommand]
	private void CancelDeleteContentDialog()
	{
		IsDeleteContentDialogOpen = false;
		SetPendingContentDeletion(null);
	}

	[RelayCommand]
	private async Task ConfirmDeleteContentDialogAsync()
	{
		PendingContentDeletion pendingContentDeletion = this.pendingContentDeletion;
		if ((object)pendingContentDeletion != null)
		{
			IsDeleteContentDialogOpen = false;
			SetPendingContentDeletion(null);
			switch (pendingContentDeletion.Kind)
			{
			case ContentKind.Mods:
				await details.DeleteModsAsync(pendingContentDeletion.FullPaths);
				break;
			case ContentKind.Saves:
				await details.DeleteSavesAsync(pendingContentDeletion.FullPaths);
				break;
			case ContentKind.ResourcePacks:
				await details.DeleteResourcePacksAsync(pendingContentDeletion.FullPaths);
				break;
			case ContentKind.ShaderPacks:
				await details.DeleteShaderPacksAsync(pendingContentDeletion.FullPaths);
				break;
			}
		}
	}

	[RelayCommand]
	private void CancelReplaceModImportDialog()
	{
		IsReplaceModImportDialogOpen = false;
		PendingModImportConflict = null;
		details.ResolvePendingModImportConflict(shouldReplace: false);
	}

	[RelayCommand]
	private void ConfirmReplaceModImportDialog()
	{
		if ((object)PendingModImportConflict != null)
		{
			IsReplaceModImportDialogOpen = false;
			PendingModImportConflict = null;
			details.ResolvePendingModImportConflict(shouldReplace: true);
		}
	}

	[RelayCommand]
	private void CloseInvalidImportDialog()
	{
		IsInvalidImportDialogOpen = false;
		InvalidImportDialogMessage = string.Empty;
		InvalidImportDialogTitle = Strings.Dialog_InvalidSaveImportTitle;
	}

	private void OpenContentDeletion(ContentKind kind, IReadOnlyList<string> fullPaths, IReadOnlyList<string> titles)
	{
		SetPendingContentDeletion(new PendingContentDeletion(kind, fullPaths, titles));
		IsDeleteContentDialogOpen = true;
	}

	private void SetPendingContentDeletion(PendingContentDeletion? value)
	{
		if (SetProperty(ref pendingContentDeletion, value, "SetPendingContentDeletion"))
		{
			OnPropertyChanged("DeleteContentDialogTitle");
			OnPropertyChanged("DeleteContentDialogMessage");
		}
	}

	private void OpenImportFailure(string title, string message)
	{
		InvalidImportDialogTitle = title;
		InvalidImportDialogMessage = message;
		IsInvalidImportDialogOpen = true;
	}

	private static string FormatDeleteMessage(PendingContentDeletion pending)
	{
		bool flag = pending.Titles.Count == 1;
		return pending.Kind switch
		{
			ContentKind.Saves => flag ? string.Format(Strings.Dialog_DeleteSingleSaveMessageFormat, pending.Titles[0]) : string.Format(Strings.Dialog_DeleteMultipleSavesMessageFormat, pending.Titles.Count), 
			ContentKind.ResourcePacks => flag ? string.Format(Strings.Dialog_DeleteSingleResourcePackMessageFormat, pending.Titles[0]) : string.Format(Strings.Dialog_DeleteMultipleResourcePacksMessageFormat, pending.Titles.Count), 
			ContentKind.ShaderPacks => flag ? string.Format(Strings.Dialog_DeleteSingleShaderPackMessageFormat, pending.Titles[0]) : string.Format(Strings.Dialog_DeleteMultipleShaderPacksMessageFormat, pending.Titles.Count), 
			_ => flag ? string.Format(Strings.Dialog_DeleteSingleModMessageFormat, pending.Titles[0]) : string.Format(Strings.Dialog_DeleteMultipleModsMessageFormat, pending.Titles.Count), 
		};
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnInstancePendingDeleteChanged(GameSettingsInstanceItem? value)
	{
		OnPropertyChanged("DeleteInstanceDialogMessage");
		ConfirmDeleteInstanceDialogCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsDeleteInstanceDialogBusyChanged(bool value)
	{
		OnPropertyChanged("DeleteInstanceDialogTitle");
		OnPropertyChanged("DeleteInstanceDialogMessage");
		OnPropertyChanged("DeleteInstanceDialogActionText");
		OnPropertyChanged("CanShowDeleteInstanceCancelButton");
		CancelDeleteInstanceDialogCommand.NotifyCanExecuteChanged();
		ConfirmDeleteInstanceDialogCommand.NotifyCanExecuteChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnHasDeleteInstanceDialogErrorChanged(bool value)
	{
		OnPropertyChanged("DeleteInstanceDialogTitle");
		OnPropertyChanged("DeleteInstanceDialogMessage");
		OnPropertyChanged("DeleteInstanceDialogActionText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnPendingModImportConflictChanged(ModImportConflictRequest? value)
	{
		OnPropertyChanged("ReplaceModImportDialogMessage");
	}
}
