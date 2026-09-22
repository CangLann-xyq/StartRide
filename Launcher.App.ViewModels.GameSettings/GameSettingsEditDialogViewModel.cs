using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.GameSettings;

public sealed class GameSettingsEditDialogViewModel : ObservableObject
{
	private const string InputGlyph = "\ue70f";

	private const string BusyGlyph = "\ue895";

	private readonly IGameInstanceService instanceService;

	private readonly IStatusService statusService;

	private string originalResolvedIconSource = string.Empty;

	private string? originalExplicitIconSource;

	private GameSettingsIconOption? transientIconOption;

	[ObservableProperty]
	private bool isEditInstanceDialogOpen;

	[ObservableProperty]
	private bool isEditInstanceDialogBusy;

	[ObservableProperty]
	private GameSettingsInstanceItem? instancePendingEdit;

	[ObservableProperty]
	private string editInstanceDialogStep = "Input";

	[ObservableProperty]
	private string instanceName = string.Empty;

	[ObservableProperty]
	private bool isInstanceNameInvalid;

	[ObservableProperty]
	private GameSettingsIconOption? selectedIconOption;

	[ObservableProperty]
	private bool isEditInstanceSuccessful;

	[ObservableProperty]
	private string editInstanceMessage = string.Empty;

	[ObservableProperty]
	private string editInstanceGlyph = "\ue70f";

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmCommand;

	public ObservableCollection<GameSettingsIconOption> IconOptions { get; } = new ObservableCollection<GameSettingsIconOption>();

	public bool IsEditInstanceInputStep => EditInstanceDialogStep == "Input";

	public bool IsEditInstanceStatusStep => EditInstanceDialogStep == "Status";

	public bool IsEditInstanceResultStep => EditInstanceDialogStep == "Result";

	public bool IsEditInstanceMessageStep
	{
		get
		{
			if (!IsEditInstanceStatusStep)
			{
				return IsEditInstanceResultStep;
			}
			return true;
		}
	}

	public bool CanShowEditInstanceCancelButton
	{
		get
		{
			if (!IsEditInstanceDialogBusy)
			{
				return IsEditInstanceInputStep;
			}
			return false;
		}
	}

	public bool CanConfirmEditInstanceDialog
	{
		get
		{
			if (!IsEditInstanceDialogBusy)
			{
				if (!IsEditInstanceResultStep)
				{
					if (IsEditInstanceInputStep && !string.IsNullOrWhiteSpace(InstanceName))
					{
						return SelectedIconOption != null;
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public string? EditInstanceGlyphIconKey
	{
		get
		{
			if (!IsEditInstanceResultStep)
			{
				return null;
			}
			if (!IsEditInstanceSuccessful)
			{
				return "general/general_attention";
			}
			return "general/general_passed";
		}
	}

	public string EditInstanceDialogTitle
	{
		get
		{
			string text = EditInstanceDialogStep;
			if (!(text == "Status"))
			{
				if (text == "Result")
				{
					return IsEditInstanceSuccessful ? Strings.Dialog_RenameInstanceSuccessTitle : Strings.Dialog_RenameInstanceFailedTitle;
				}
				return Strings.Dialog_RenameInstanceTitle;
			}
			return Strings.Dialog_RenameInstanceBusyTitle;
		}
	}

	public string EditInstanceDialogSubtitle
	{
		get
		{
			string text = EditInstanceDialogStep;
			if (!(text == "Status"))
			{
				if (text == "Result")
				{
					return Strings.Dialog_RenameInstanceResultSubtitle;
				}
				return Strings.Dialog_RenameInstanceSubtitle;
			}
			return Strings.Dialog_RenameInstanceBusySubtitle;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsEditInstanceDialogOpen
	{
		get
		{
			return isEditInstanceDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isEditInstanceDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsEditInstanceDialogOpen);
				isEditInstanceDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsEditInstanceDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsEditInstanceDialogBusy
	{
		get
		{
			return isEditInstanceDialogBusy;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isEditInstanceDialogBusy, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsEditInstanceDialogBusy);
				isEditInstanceDialogBusy = value;
				OnIsEditInstanceDialogBusyChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsEditInstanceDialogBusy);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public GameSettingsInstanceItem? InstancePendingEdit
	{
		get
		{
			return instancePendingEdit;
		}
		set
		{
			if (!EqualityComparer<GameSettingsInstanceItem>.Default.Equals(instancePendingEdit, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InstancePendingEdit);
				instancePendingEdit = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InstancePendingEdit);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string EditInstanceDialogStep
	{
		get
		{
			return editInstanceDialogStep;
		}
		[MemberNotNull("editInstanceDialogStep")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(editInstanceDialogStep, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EditInstanceDialogStep);
				editInstanceDialogStep = value;
				OnEditInstanceDialogStepChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EditInstanceDialogStep);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string InstanceName
	{
		get
		{
			return instanceName;
		}
		[MemberNotNull("instanceName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(instanceName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InstanceName);
				instanceName = value;
				OnInstanceNameChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InstanceName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsInstanceNameInvalid
	{
		get
		{
			return isInstanceNameInvalid;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isInstanceNameInvalid, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsInstanceNameInvalid);
				isInstanceNameInvalid = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsInstanceNameInvalid);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public GameSettingsIconOption? SelectedIconOption
	{
		get
		{
			return selectedIconOption;
		}
		set
		{
			if (!EqualityComparer<GameSettingsIconOption>.Default.Equals(selectedIconOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedIconOption);
				selectedIconOption = value;
				OnSelectedIconOptionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedIconOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsEditInstanceSuccessful
	{
		get
		{
			return isEditInstanceSuccessful;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isEditInstanceSuccessful, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsEditInstanceSuccessful);
				isEditInstanceSuccessful = value;
				OnIsEditInstanceSuccessfulChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsEditInstanceSuccessful);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string EditInstanceMessage
	{
		get
		{
			return editInstanceMessage;
		}
		[MemberNotNull("editInstanceMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(editInstanceMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EditInstanceMessage);
				editInstanceMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EditInstanceMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string EditInstanceGlyph
	{
		get
		{
			return editInstanceGlyph;
		}
		[MemberNotNull("editInstanceGlyph")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(editInstanceGlyph, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EditInstanceGlyph);
				editInstanceGlyph = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EditInstanceGlyph);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelCommand => cancelCommand ?? (cancelCommand = new RelayCommand(Cancel));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmCommand => confirmCommand ?? (confirmCommand = new AsyncRelayCommand(ConfirmAsync));

	public event Action<GameInstance>? InstanceUpdated;

	public event Action? InstanceRenameStarting;

	public event Action? InstanceRenameFinished;

	public GameSettingsEditDialogViewModel(IGameInstanceService instanceService, IStatusService statusService)
	{
		this.instanceService = instanceService;
		this.statusService = statusService;
		foreach (GameSettingsIconOption item in GameSettingsIconOptionFactory.Create())
		{
			IconOptions.Add(item);
		}
	}

	public void Open(GameSettingsInstanceItem instance)
	{
		RemoveTransientIconOption();
		InstancePendingEdit = instance;
		originalResolvedIconSource = instance.IconSource;
		originalExplicitIconSource = (string.IsNullOrWhiteSpace(instance.Instance.IconSource) ? null : instance.Instance.IconSource);
		ResetEditInstanceDialogState(instance.Name, ResolveInitialIconOption(instance));
		IsEditInstanceDialogOpen = true;
	}

	[RelayCommand]
	public void Cancel()
	{
		if (!IsEditInstanceDialogBusy)
		{
			IsEditInstanceDialogOpen = false;
		}
	}

	[RelayCommand]
	public async Task ConfirmAsync()
	{
		if (IsEditInstanceDialogBusy)
		{
			return;
		}
		if (IsEditInstanceResultStep)
		{
			IsEditInstanceDialogOpen = false;
			return;
		}
		GameSettingsInstanceItem gameSettingsInstanceItem = InstancePendingEdit;
		GameSettingsIconOption gameSettingsIconOption = SelectedIconOption;
		if (gameSettingsInstanceItem == null || gameSettingsIconOption == null)
		{
			return;
		}
		string text = InstanceName.Trim();
		if (!IsValidInstanceName(text))
		{
			IsInstanceNameInvalid = true;
			return;
		}
		string text2 = ResolveIconSourceToPersist(gameSettingsIconOption);
		bool num = string.Equals(text, gameSettingsInstanceItem.Instance.Name, StringComparison.Ordinal) && string.Equals(text, gameSettingsInstanceItem.Instance.VersionName, StringComparison.OrdinalIgnoreCase);
		bool flag = string.Equals(text2, gameSettingsInstanceItem.Instance.IconSource, StringComparison.Ordinal);
		if (num & flag)
		{
			statusService.Report(Strings.Status_InstanceRenameUnchanged);
			CloseAfterSuccess();
			return;
		}
		try
		{
			IsEditInstanceDialogBusy = true;
			EditInstanceDialogStep = "Status";
			EditInstanceGlyph = "\ue895";
			EditInstanceMessage = Strings.Status_RenamingInstance;
			InstanceRenameStarting?.Invoke();
			GameInstance gameInstance = await instanceService.RenameInstanceAsync(gameSettingsInstanceItem.Instance.Id, text, text2);
			statusService.Report(string.Format(Strings.Status_InstanceRenamedFormat, gameInstance.Name));
			InstanceUpdated?.Invoke(gameInstance);
			CloseAfterSuccess();
		}
		catch (DuplicateGameInstanceNameException)
		{
			statusService.Report(Strings.Status_DuplicateInstanceName);
			ShowEditInstanceResult(isSuccess: false, Strings.Status_DuplicateInstanceName);
		}
		catch (Exception)
		{
			statusService.Report(Strings.Status_InstanceRenameFailed);
			ShowEditInstanceResult(isSuccess: false, Strings.Status_InstanceRenameFailed);
		}
		finally
		{
			InstanceRenameFinished?.Invoke();
			IsEditInstanceDialogBusy = false;
		}
	}

	private void ResetEditInstanceDialogState(string currentName, GameSettingsIconOption? iconOption)
	{
		IsEditInstanceDialogBusy = false;
		EditInstanceDialogStep = "Input";
		InstanceName = currentName;
		IsInstanceNameInvalid = false;
		SelectedIconOption = iconOption ?? IconOptions.FirstOrDefault();
		IsEditInstanceSuccessful = false;
		EditInstanceMessage = string.Empty;
		EditInstanceGlyph = "\ue70f";
	}

	private void ShowEditInstanceResult(bool isSuccess, string message)
	{
		IsEditInstanceSuccessful = isSuccess;
		EditInstanceMessage = message;
		EditInstanceDialogStep = "Result";
	}

	private void CloseAfterSuccess()
	{
		IsEditInstanceSuccessful = true;
		IsEditInstanceDialogOpen = false;
	}

	private string? ResolveIconSourceToPersist(GameSettingsIconOption selectedIcon)
	{
		if (!string.IsNullOrWhiteSpace(originalExplicitIconSource) || !string.Equals(selectedIcon.IconSource, originalResolvedIconSource, StringComparison.OrdinalIgnoreCase))
		{
			return selectedIcon.IconSource;
		}
		return null;
	}

	private GameSettingsIconOption? ResolveInitialIconOption(GameSettingsInstanceItem instance)
	{
		string preferredIconSource = (string.IsNullOrWhiteSpace(instance.Instance.IconSource) ? instance.IconSource : instance.Instance.IconSource);
		GameSettingsIconOption gameSettingsIconOption = IconOptions.FirstOrDefault((GameSettingsIconOption option) => string.Equals(option.IconSource, preferredIconSource, StringComparison.OrdinalIgnoreCase));
		if (gameSettingsIconOption != null || string.IsNullOrWhiteSpace(preferredIconSource))
		{
			return gameSettingsIconOption;
		}
		transientIconOption = new GameSettingsIconOption(Strings.GameSettings_InstanceIconLabel, preferredIconSource);
		IconOptions.Insert(0, transientIconOption);
		return transientIconOption;
	}

	private void RemoveTransientIconOption()
	{
		if (transientIconOption != null)
		{
			IconOptions.Remove(transientIconOption);
			transientIconOption = null;
		}
	}

	private static bool IsValidInstanceName(string value)
	{
		return VersionDirectoryName.IsSafeDirectoryName(value);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsEditInstanceDialogBusyChanged(bool value)
	{
		OnPropertyChanged("CanShowEditInstanceCancelButton");
		OnPropertyChanged("CanConfirmEditInstanceDialog");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnEditInstanceDialogStepChanged(string value)
	{
		OnPropertyChanged("IsEditInstanceInputStep");
		OnPropertyChanged("IsEditInstanceStatusStep");
		OnPropertyChanged("IsEditInstanceResultStep");
		OnPropertyChanged("IsEditInstanceMessageStep");
		OnPropertyChanged("EditInstanceDialogTitle");
		OnPropertyChanged("EditInstanceDialogSubtitle");
		OnPropertyChanged("EditInstanceGlyphIconKey");
		OnPropertyChanged("CanShowEditInstanceCancelButton");
		OnPropertyChanged("CanConfirmEditInstanceDialog");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnInstanceNameChanged(string value)
	{
		IsInstanceNameInvalid = false;
		OnPropertyChanged("CanConfirmEditInstanceDialog");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedIconOptionChanged(GameSettingsIconOption? value)
	{
		OnPropertyChanged("CanConfirmEditInstanceDialog");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsEditInstanceSuccessfulChanged(bool value)
	{
		OnPropertyChanged("EditInstanceDialogTitle");
		OnPropertyChanged("EditInstanceGlyphIconKey");
	}
}
