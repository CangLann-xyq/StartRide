using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Models;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.Application.Accounts;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Account;

public sealed class AccountOfflineUuidViewModel : ObservableObject
{
	private readonly AccountListViewModel accountList;

	private readonly IOfflineAccountUuidService offlineUuidService;

	private readonly IStatusService statusService;

	private readonly IClipboardService clipboardService;

	private readonly ILogger<AccountOfflineUuidViewModel> logger;

	private bool isRefreshingSelection;

	private OfflineUuidModeOption? acceptedOfflineUuidOption;

	private OfflineUuidModeOption? pendingOfflineUuidOption;

	private LauncherAccount? pendingAccount;

	[ObservableProperty]
	[NotifyPropertyChangedFor("CanChangeOfflineUuidMode")]
	[NotifyPropertyChangedFor("CanApplyManualUuid")]
	[NotifyCanExecuteChangedFor("ApplyManualUuidCommand")]
	private bool isOfflineUuidModeChangeDialogOpen;

	[ObservableProperty]
	[NotifyPropertyChangedFor("CanChangeOfflineUuidMode")]
	[NotifyPropertyChangedFor("CanApplyManualUuid")]
	[NotifyCanExecuteChangedFor("ApplyManualUuidCommand")]
	private bool isSavingUuid;

	[ObservableProperty]
	private OfflineUuidModeOption? selectedOfflineUuidOption;

	[ObservableProperty]
	private string manualUuidText = string.Empty;

	[ObservableProperty]
	private bool isManualUuidInvalid;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelOfflineUuidModeChangeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmOfflineUuidModeChangeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? applyManualUuidCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? copySelectedUuidCommand;

	public ObservableCollection<OfflineUuidModeOption> OfflineUuidOptions { get; }

	public bool HasSelectedOfflineAccount => accountList.SelectedAccount?.IsOffline ?? false;

	public bool HasManualUuidEditor
	{
		get
		{
			if (HasSelectedOfflineAccount)
			{
				OfflineUuidModeOption? offlineUuidModeOption = acceptedOfflineUuidOption;
				if (offlineUuidModeOption == null)
				{
					return false;
				}
				return offlineUuidModeOption.Mode == OfflineUuidGenerationMode.Manual;
			}
			return false;
		}
	}

	public bool CanChangeOfflineUuidMode
	{
		get
		{
			if (HasSelectedOfflineAccount && !IsOfflineUuidModeChangeDialogOpen)
			{
				return !IsSavingUuid;
			}
			return false;
		}
	}

	public bool CanApplyManualUuid
	{
		get
		{
			if (CanChangeOfflineUuidMode && HasManualUuidEditor)
			{
				return !string.IsNullOrWhiteSpace(ManualUuidText);
			}
			return false;
		}
	}

	public string OfflineUuidModeChangeMessage => string.Format(Strings.Dialog_OfflineUuidModeChangeMessageFormat, pendingOfflineUuidOption?.Title);

	public string SelectedAccountUuidText
	{
		get
		{
			string text = accountList.SelectedAccount?.Uuid;
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
			return Strings.Account_NoneValue;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsOfflineUuidModeChangeDialogOpen
	{
		get
		{
			return isOfflineUuidModeChangeDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isOfflineUuidModeChangeDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsOfflineUuidModeChangeDialogOpen);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CanChangeOfflineUuidMode);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CanApplyManualUuid);
				isOfflineUuidModeChangeDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsOfflineUuidModeChangeDialogOpen);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CanChangeOfflineUuidMode);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CanApplyManualUuid);
				ApplyManualUuidCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsSavingUuid
	{
		get
		{
			return isSavingUuid;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isSavingUuid, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsSavingUuid);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CanChangeOfflineUuidMode);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CanApplyManualUuid);
				isSavingUuid = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsSavingUuid);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CanChangeOfflineUuidMode);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CanApplyManualUuid);
				ApplyManualUuidCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public OfflineUuidModeOption? SelectedOfflineUuidOption
	{
		get
		{
			return selectedOfflineUuidOption;
		}
		set
		{
			if (!EqualityComparer<OfflineUuidModeOption>.Default.Equals(selectedOfflineUuidOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedOfflineUuidOption);
				selectedOfflineUuidOption = value;
				OnSelectedOfflineUuidOptionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedOfflineUuidOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ManualUuidText
	{
		get
		{
			return manualUuidText;
		}
		[MemberNotNull("manualUuidText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(manualUuidText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ManualUuidText);
				manualUuidText = value;
				OnManualUuidTextChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ManualUuidText);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsManualUuidInvalid
	{
		get
		{
			return isManualUuidInvalid;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isManualUuidInvalid, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsManualUuidInvalid);
				isManualUuidInvalid = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsManualUuidInvalid);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelOfflineUuidModeChangeCommand => cancelOfflineUuidModeChangeCommand ?? (cancelOfflineUuidModeChangeCommand = new RelayCommand(CancelOfflineUuidModeChange));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmOfflineUuidModeChangeCommand => confirmOfflineUuidModeChangeCommand ?? (confirmOfflineUuidModeChangeCommand = new AsyncRelayCommand(ConfirmOfflineUuidModeChangeAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ApplyManualUuidCommand => applyManualUuidCommand ?? (applyManualUuidCommand = new AsyncRelayCommand(ApplyManualUuidAsync, () => CanApplyManualUuid));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand CopySelectedUuidCommand => copySelectedUuidCommand ?? (copySelectedUuidCommand = new AsyncRelayCommand(CopySelectedUuidAsync));

	public AccountOfflineUuidViewModel(AccountListViewModel accountList, IOfflineAccountUuidService offlineUuidService, IStatusService statusService, IClipboardService clipboardService, ILogger<AccountOfflineUuidViewModel>? logger = null)
	{
		this.accountList = accountList;
		this.offlineUuidService = offlineUuidService;
		this.statusService = statusService;
		this.clipboardService = clipboardService;
		this.logger = logger ?? NullLogger<AccountOfflineUuidViewModel>.Instance;
		int num = 2;
		List<OfflineUuidModeOption> list = new List<OfflineUuidModeOption>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<OfflineUuidModeOption> span = CollectionsMarshal.AsSpan(list);
		span[0] = new OfflineUuidModeOption
		{
			Mode = OfflineUuidGenerationMode.Standard,
			Title = Strings.Account_OfflineUuidStandardTitle,
			Description = Strings.Account_OfflineUuidStandardDescription
		};
		span[1] = new OfflineUuidModeOption
		{
			Mode = OfflineUuidGenerationMode.Manual,
			Title = Strings.Account_OfflineUuidManualTitle,
			Description = Strings.Account_OfflineUuidManualDescription
		};
		OfflineUuidOptions = new ObservableCollection<OfflineUuidModeOption>(list);
		accountList.PropertyChanged += AccountList_PropertyChanged;
		RefreshSelection();
	}

	[RelayCommand]
	private void CancelOfflineUuidModeChange()
	{
		pendingAccount = null;
		pendingOfflineUuidOption = null;
		IsOfflineUuidModeChangeDialogOpen = false;
		RestoreAcceptedOption();
	}

	[RelayCommand]
	private async Task ConfirmOfflineUuidModeChangeAsync()
	{
		LauncherAccount launcherAccount = pendingAccount;
		OfflineUuidModeOption offlineUuidModeOption = pendingOfflineUuidOption;
		if (!IsOfflineUuidModeChangeDialogOpen || IsSavingUuid || launcherAccount == null || offlineUuidModeOption == null || accountList.SelectedAccount != launcherAccount)
		{
			CancelOfflineUuidModeChange();
			return;
		}
		pendingAccount = null;
		pendingOfflineUuidOption = null;
		IsOfflineUuidModeChangeDialogOpen = false;
		logger.LogInformation("Offline UUID mode change confirmed. AccountId={AccountId} Mode={Mode}", launcherAccount.Id, offlineUuidModeOption.Mode);
		if (offlineUuidModeOption.Mode == OfflineUuidGenerationMode.Manual)
		{
			acceptedOfflineUuidOption = offlineUuidModeOption;
			RestoreAcceptedOption();
			IsManualUuidInvalid = false;
			ManualUuidText = launcherAccount.Uuid ?? string.Empty;
			OnPropertyChanged("HasManualUuidEditor");
			OnPropertyChanged("CanApplyManualUuid");
			ApplyManualUuidCommand.NotifyCanExecuteChanged();
		}
		else
		{
			await SelectOfflineUuidModeAsync(launcherAccount, offlineUuidModeOption);
		}
	}

	private void RestoreAcceptedOption()
	{
		isRefreshingSelection = true;
		try
		{
			SelectedOfflineUuidOption = acceptedOfflineUuidOption;
		}
		finally
		{
			isRefreshingSelection = false;
		}
	}

	private async Task SelectOfflineUuidModeAsync(LauncherAccount account, OfflineUuidModeOption option)
	{
		IsSavingUuid = true;
		try
		{
			string existingUuid = ((account.OfflineUuidGenerationMode == option.Mode) ? account.Uuid : null);
			string uuid = offlineUuidService.CreateUuid(account.DisplayName, option.Mode, existingUuid);
			LauncherAccount newAccount = AccountMapper.WithOfflineUuid(account, option.Mode, uuid);
			await accountList.ReplaceSelectedAccountAndPersistAsync(account, newAccount);
			logger.LogInformation("Offline UUID mode changed. AccountId={AccountId} Mode={Mode}", account.Id, option.Mode);
			statusService.Report(string.Format(Strings.Status_OfflineUuidModeChangedFormat, option.Title));
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Offline UUID mode change failed. AccountId={AccountId} Mode={Mode}", account.Id, option.Mode);
			RefreshSelection();
			statusService.Report(Strings.Status_OfflineUuidModeChangeFailed);
		}
		finally
		{
			IsSavingUuid = false;
		}
	}

	[RelayCommand(CanExecute = "CanApplyManualUuid")]
	private async Task ApplyManualUuidAsync()
	{
		if (!CanApplyManualUuid)
		{
			return;
		}
		LauncherAccount account = accountList.SelectedAccount;
		if (account == null || !account.IsOffline)
		{
			return;
		}
		if (!offlineUuidService.TryNormalizeUuid(ManualUuidText, out string uuid))
		{
			IsManualUuidInvalid = true;
			statusService.Report(Strings.Status_OfflineUuidInvalid);
			return;
		}
		IsSavingUuid = true;
		try
		{
			LauncherAccount updatedAccount = AccountMapper.WithOfflineUuid(account, OfflineUuidGenerationMode.Manual, uuid);
			await accountList.ReplaceSelectedAccountAndPersistAsync(account, updatedAccount);
			if (accountList.SelectedAccount == updatedAccount)
			{
				ManualUuidText = uuid;
			}
			logger.LogInformation("Manual offline UUID applied. AccountId={AccountId}", account.Id);
			statusService.Report(Strings.Status_OfflineUuidApplied);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Manual offline UUID apply failed. AccountId={AccountId}", account.Id);
			RefreshSelection();
			statusService.Report(Strings.Status_OfflineUuidModeChangeFailed);
		}
		finally
		{
			IsSavingUuid = false;
		}
	}

	[RelayCommand]
	private async Task CopySelectedUuidAsync(CancellationToken cancellationToken)
	{
		string text = accountList.SelectedAccount?.Uuid;
		if (!string.IsNullOrWhiteSpace(text))
		{
			await clipboardService.CopyTextAsync(text, cancellationToken);
		}
	}

	private void RefreshSelection()
	{
		pendingAccount = null;
		pendingOfflineUuidOption = null;
		IsOfflineUuidModeChangeDialogOpen = false;
		isRefreshingSelection = true;
		try
		{
			LauncherAccount selectedAccount = accountList.SelectedAccount;
			OfflineUuidGenerationMode? displayedMode = ((selectedAccount != null && selectedAccount.OfflineUuidGenerationMode == OfflineUuidGenerationMode.Random) ? new OfflineUuidGenerationMode?(OfflineUuidGenerationMode.Manual) : selectedAccount?.OfflineUuidGenerationMode);
			acceptedOfflineUuidOption = ((selectedAccount != null && selectedAccount.IsOffline) ? OfflineUuidOptions.FirstOrDefault((OfflineUuidModeOption option) => option.Mode == displayedMode) : null);
			SelectedOfflineUuidOption = acceptedOfflineUuidOption;
			ManualUuidText = selectedAccount?.Uuid ?? string.Empty;
			IsManualUuidInvalid = false;
		}
		finally
		{
			isRefreshingSelection = false;
		}
		OnPropertyChanged("HasSelectedOfflineAccount");
		OnPropertyChanged("CanChangeOfflineUuidMode");
		OnPropertyChanged("HasManualUuidEditor");
		OnPropertyChanged("CanApplyManualUuid");
		OnPropertyChanged("SelectedAccountUuidText");
		ApplyManualUuidCommand.NotifyCanExecuteChanged();
	}

	private void AccountList_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "SelectedAccount")
		{
			RefreshSelection();
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedOfflineUuidOptionChanged(OfflineUuidModeOption? value)
	{
		if (!isRefreshingSelection && value != null)
		{
			if (!CanChangeOfflineUuidMode)
			{
				RestoreAcceptedOption();
			}
			else if (value.Mode != acceptedOfflineUuidOption?.Mode)
			{
				pendingAccount = accountList.SelectedAccount;
				pendingOfflineUuidOption = value;
				OnPropertyChanged("OfflineUuidModeChangeMessage");
				IsOfflineUuidModeChangeDialogOpen = true;
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnManualUuidTextChanged(string value)
	{
		IsManualUuidInvalid = false;
		OnPropertyChanged("CanApplyManualUuid");
		ApplyManualUuidCommand.NotifyCanExecuteChanged();
	}
}
