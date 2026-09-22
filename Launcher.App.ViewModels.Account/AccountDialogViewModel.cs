using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.App.Models;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.Application.Accounts;
using Launcher.Domain.Models;
using StartRide.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Account;

public sealed class AccountDialogViewModel : ObservableObject
{
	private const string DialogBusyIcon = "\ue895";

	private const string DialogSuccessIcon = "\ue73e";

	private const string DialogFailureIcon = "\ue783";

	private const string RenameInputIcon = "\ue70f";

	private readonly AccountListViewModel accountList;

	private readonly IMicrosoftAccountService microsoftAccountService;

	private readonly IThirdPartyAccountService thirdPartyAccountService;

	private readonly IOfflineAccountUuidService offlineUuidService;

	private readonly IStatusService statusService;

	private readonly ILogger<AccountDialogViewModel> logger;

	[ObservableProperty]
	private bool isAddAccountDialogOpen;

	[ObservableProperty]
	private bool isAddAccountDialogBusy;

	[ObservableProperty]
	private AccountTypeOption? selectedAccountTypeOption;

	[ObservableProperty]
	private string addAccountDialogStep = "Type";

	[ObservableProperty]
	private string newOfflineAccountName = string.Empty;

	[ObservableProperty]
	private bool isNewOfflineAccountNameInvalid;

	[ObservableProperty]
	private string microsoftLoginMessage = MicrosoftLoginInitialMessage;

	[ObservableProperty]
	private string microsoftLoginIcon = "\ue895";

	[ObservableProperty]
	private bool isMicrosoftLoginSuccessful;

	[ObservableProperty]
	private bool isMicrosoftAccountAlreadyAdded;

	[ObservableProperty]
	private LauncherAccount? accountPendingMicrosoftReauthentication;

	[ObservableProperty]
	private LauncherAccount? accountPendingThirdPartyReauthentication;

	[ObservableProperty]
	private string thirdPartyImportCurrentProfileName = string.Empty;

	[ObservableProperty]
	private int thirdPartyImportCompletedCount;

	[ObservableProperty]
	private int thirdPartyImportTotalCount;

	[ObservableProperty]
	private int thirdPartyImportFailedCount;

	private readonly List<ThirdPartyProfileOptionViewModel> thirdPartyFailedProfiles = new List<ThirdPartyProfileOptionViewModel>();

	private readonly List<LauncherAccount> thirdPartySuccessfulAccounts = new List<LauncherAccount>();

	private CancellationTokenSource? thirdPartyImportCancellationTokenSource;

	private CancellationTokenSource? microsoftAuthenticationCancellationTokenSource;

	private bool isDirectThirdPartyAddAccountEntry;

	private bool navigateToAccountAfterThirdPartyAddition;

	private bool hasPendingDroppedThirdPartyNavigation;

	[ObservableProperty]
	private bool isDeleteAccountDialogOpen;

	[ObservableProperty]
	private LauncherAccount? accountPendingDelete;

	[ObservableProperty]
	private bool isRenameAccountDialogOpen;

	[ObservableProperty]
	private bool isRenameAccountDialogBusy;

	[ObservableProperty]
	private LauncherAccount? accountPendingRename;

	[ObservableProperty]
	private string renameAccountDialogStep = "Input";

	[ObservableProperty]
	private string renameAccountName = string.Empty;

	[ObservableProperty]
	private bool isRenameAccountNameInvalid;

	[ObservableProperty]
	private bool isRenameAccountSuccessful;

	[ObservableProperty]
	private string renameAccountMessage = string.Empty;

	[ObservableProperty]
	private string renameAccountErrorCodeMessage = string.Empty;

	[ObservableProperty]
	private string renameAccountIcon = "\ue70f";

	private static string MicrosoftLoginInitialMessage => Strings.Status_OpeningMicrosoftLogin;

	private static string MicrosoftLoginActiveMessage => Strings.Status_LoginMicrosoftActive;

	public ObservableCollection<AccountTypeOption> AccountTypeOptions { get; } = new ObservableCollection<AccountTypeOption>(AccountTypeOptionFactory.Create());

	public ThirdPartyAccountDialogViewModel ThirdParty { get; }

	public bool IsAccountTypeStep => AddAccountDialogStep == "Type";

	public bool IsOfflineNameStep => AddAccountDialogStep == "OfflineName";

	public bool IsThirdPartyCredentialsStep => AddAccountDialogStep == "ThirdPartyCredentials";

	public bool IsThirdPartyReauthenticationStep => AddAccountDialogStep == "ThirdPartyReauthentication";

	public bool IsThirdPartyFormStep
	{
		get
		{
			if (!IsThirdPartyCredentialsStep)
			{
				return IsThirdPartyReauthenticationStep;
			}
			return true;
		}
	}

	public bool IsThirdPartyProfileSelectionStep => AddAccountDialogStep == "ThirdPartyProfileSelection";

	public bool IsThirdPartyImportProgressStep => AddAccountDialogStep == "ThirdPartyImportProgress";

	public bool IsThirdPartyImportResultStep => AddAccountDialogStep == "ThirdPartyImportResult";

	public bool IsMicrosoftLoginStep => AddAccountDialogStep == "MicrosoftLogin";

	public bool IsMicrosoftLoginResultStep => AddAccountDialogStep == "MicrosoftResult";

	public bool IsMicrosoftReauthenticationPromptStep => AddAccountDialogStep == "MicrosoftReauthenticationPrompt";

	public bool IsMicrosoftReauthenticationStep => AddAccountDialogStep == "MicrosoftReauthentication";

	public bool IsMicrosoftReauthenticationResultStep => AddAccountDialogStep == "MicrosoftReauthenticationResult";

	public bool IsMicrosoftReauthenticationMode => AccountPendingMicrosoftReauthentication != null;

	public bool IsMicrosoftStatusStep
	{
		get
		{
			if (!IsMicrosoftLoginStep && !IsMicrosoftLoginResultStep && !IsMicrosoftReauthenticationPromptStep && !IsMicrosoftReauthenticationStep)
			{
				return IsMicrosoftReauthenticationResultStep;
			}
			return true;
		}
	}

	public bool CanShowAddAccountBackButton
	{
		get
		{
			if (!IsAddAccountDialogBusy)
			{
				if (!IsOfflineNameStep && (!IsThirdPartyCredentialsStep || isDirectThirdPartyAddAccountEntry))
				{
					return IsMicrosoftLoginStep;
				}
				return true;
			}
			return false;
		}
	}

	public bool CanShowAddAccountCancelButton
	{
		get
		{
			if (!IsAddAccountDialogBusy)
			{
				if (IsMicrosoftLoginResultStep)
				{
					return IsMicrosoftReauthenticationMode;
				}
				return true;
			}
			return false;
		}
	}

	public bool CanShowMicrosoftAuthenticationCancelButton
	{
		get
		{
			if (IsAddAccountDialogBusy)
			{
				if (!IsMicrosoftLoginStep)
				{
					return IsMicrosoftReauthenticationStep;
				}
				return true;
			}
			return false;
		}
	}

	public bool IsAddAccountFooterEnabled => !IsAddAccountDialogBusy;

	public bool CanConfirmAddAccountDialog
	{
		get
		{
			if (!IsAddAccountDialogBusy)
			{
				if (!IsMicrosoftLoginResultStep && !IsMicrosoftReauthenticationPromptStep && !IsMicrosoftReauthenticationResultStep && !IsOfflineNameStep && (!IsThirdPartyFormStep || !ThirdParty.CanConfirm) && (!IsThirdPartyProfileSelectionStep || !ThirdParty.HasSelectedProfiles) && !IsThirdPartyImportResultStep)
				{
					if (IsAccountTypeStep)
					{
						return SelectedAccountTypeOption != null;
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public bool IsMicrosoftAccountTypeSelected
	{
		get
		{
			if (IsAccountTypeStep)
			{
				return SelectedAccountTypeOption?.Kind == "Microsoft";
			}
			return false;
		}
	}

	public bool IsRenameAccountInputStep => RenameAccountDialogStep == "Input";

	public bool IsRenameAccountStatusStep => RenameAccountDialogStep == "Status";

	public bool IsRenameAccountResultStep => RenameAccountDialogStep == "Result";

	public bool IsRenameAccountMessageStep
	{
		get
		{
			if (!IsRenameAccountStatusStep)
			{
				return IsRenameAccountResultStep;
			}
			return true;
		}
	}

	public bool IsRenameMicrosoftAccount => AccountPendingRename?.IsMicrosoft ?? false;

	public bool CanShowRenameAccountCancelButton
	{
		get
		{
			if (!IsRenameAccountDialogBusy)
			{
				return IsRenameAccountInputStep;
			}
			return false;
		}
	}

	public bool CanConfirmRenameAccountDialog
	{
		get
		{
			if (!IsRenameAccountDialogBusy)
			{
				if (!IsRenameAccountResultStep)
				{
					if (IsRenameAccountInputStep)
					{
						return !string.IsNullOrWhiteSpace(RenameAccountName);
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public bool HasRenameAccountErrorCode => !string.IsNullOrWhiteSpace(RenameAccountErrorCodeMessage);

	public string? MicrosoftLoginIconKey
	{
		get
		{
			if (!IsMicrosoftLoginStep && !IsMicrosoftReauthenticationStep)
			{
				if (!IsMicrosoftReauthenticationPromptStep)
				{
					if (!IsMicrosoftLoginResultStep && !IsMicrosoftReauthenticationResultStep)
					{
						return null;
					}
					if (!IsMicrosoftLoginSuccessful)
					{
						return "general/general_attention";
					}
					return "general/general_passed";
				}
				return "general/general_attention";
			}
			return "general/general_external-web";
		}
	}

	public string? RenameAccountIconKey
	{
		get
		{
			if (!IsRenameAccountResultStep)
			{
				return null;
			}
			if (!IsRenameAccountSuccessful)
			{
				return "general/general_attention";
			}
			return "general/general_passed";
		}
	}

	public string RenameAccountDialogTitle => AccountDialogText.GetRenameTitle(RenameAccountDialogStep, IsRenameAccountSuccessful);

	public string RenameAccountDialogSubtitle => AccountDialogText.GetRenameSubtitle(RenameAccountDialogStep, IsRenameMicrosoftAccount);

	public string AddAccountDialogTitle => AddAccountDialogStep switch
	{
		"MicrosoftReauthenticationPrompt" => Strings.Dialog_MicrosoftAccountExpiredTitle, 
		"MicrosoftReauthentication" => Strings.Dialog_ReauthenticateMicrosoftAccountTitle, 
		"MicrosoftReauthenticationResult" => Strings.Dialog_ReauthenticateMicrosoftAccountTitle, 
		"ThirdPartyReauthentication" => Strings.Dialog_ReauthenticateThirdPartyAccountTitle, 
		"ThirdPartyProfileSelection" => Strings.Dialog_ThirdPartyProfileSelectionTitle, 
		"ThirdPartyImportProgress" => Strings.Dialog_ThirdPartyImportProgressTitle, 
		"ThirdPartyImportResult" => Strings.Dialog_ThirdPartyImportResultTitle, 
		_ => AccountDialogText.GetAddTitle(AddAccountDialogStep, IsMicrosoftAccountAlreadyAdded, IsMicrosoftLoginSuccessful), 
	};

	public string AddAccountDialogSubtitle => AddAccountDialogStep switch
	{
		"MicrosoftReauthenticationPrompt" => Strings.Dialog_MicrosoftAccountExpiredSubtitle, 
		"MicrosoftReauthentication" => Strings.Dialog_ReauthenticateMicrosoftAccountSubtitle, 
		"MicrosoftReauthenticationResult" => Strings.Dialog_ReauthenticateMicrosoftAccountSubtitle, 
		"ThirdPartyReauthentication" => Strings.Dialog_ReauthenticateThirdPartyAccountSubtitle, 
		"ThirdPartyProfileSelection" => Strings.Dialog_ThirdPartyProfileSelectionSubtitle, 
		"ThirdPartyImportProgress" => Strings.Dialog_ThirdPartyImportProgressSubtitle, 
		"ThirdPartyImportResult" => Strings.Dialog_ThirdPartyImportResultSubtitle, 
		_ => AccountDialogText.GetAddSubtitle(AddAccountDialogStep), 
	};

	public bool IsThirdPartyIdentityReadOnly => IsThirdPartyReauthenticationStep;

	public bool CanSelectAllThirdPartyProfiles
	{
		get
		{
			if (IsThirdPartyProfileSelectionStep)
			{
				return ThirdParty.CanSelectAllProfiles;
			}
			return false;
		}
	}

	public bool CanShowStandardAddAccountFooter
	{
		get
		{
			if (!IsThirdPartyImportProgressStep && !IsThirdPartyImportResultStep)
			{
				return !CanShowMicrosoftAuthenticationCancelButton;
			}
			return false;
		}
	}

	public string AddAccountConfirmButtonText
	{
		get
		{
			if (!IsMicrosoftReauthenticationResultStep)
			{
				return Strings.Confirm_Button;
			}
			return Strings.Retry_Button;
		}
	}

	public string ThirdPartyImportProgressText => string.Format(Strings.Dialog_ThirdPartyImportProgressFormat, ThirdPartyImportCompletedCount, ThirdPartyImportTotalCount);

	public string ThirdPartyImportFailureText => string.Format(Strings.Dialog_ThirdPartyImportFailureFormat, ThirdPartyImportFailedCount);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsAddAccountDialogOpen
	{
		get
		{
			return isAddAccountDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isAddAccountDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsAddAccountDialogOpen);
				isAddAccountDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsAddAccountDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsAddAccountDialogBusy
	{
		get
		{
			return isAddAccountDialogBusy;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isAddAccountDialogBusy, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsAddAccountDialogBusy);
				isAddAccountDialogBusy = value;
				OnIsAddAccountDialogBusyChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsAddAccountDialogBusy);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public AccountTypeOption? SelectedAccountTypeOption
	{
		get
		{
			return selectedAccountTypeOption;
		}
		set
		{
			if (!EqualityComparer<AccountTypeOption>.Default.Equals(selectedAccountTypeOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedAccountTypeOption);
				selectedAccountTypeOption = value;
				OnSelectedAccountTypeOptionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedAccountTypeOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string AddAccountDialogStep
	{
		get
		{
			return addAccountDialogStep;
		}
		[MemberNotNull("addAccountDialogStep")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(addAccountDialogStep, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AddAccountDialogStep);
				addAccountDialogStep = value;
				OnAddAccountDialogStepChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AddAccountDialogStep);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string NewOfflineAccountName
	{
		get
		{
			return newOfflineAccountName;
		}
		[MemberNotNull("newOfflineAccountName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(newOfflineAccountName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.NewOfflineAccountName);
				newOfflineAccountName = value;
				OnNewOfflineAccountNameChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.NewOfflineAccountName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsNewOfflineAccountNameInvalid
	{
		get
		{
			return isNewOfflineAccountNameInvalid;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isNewOfflineAccountNameInvalid, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsNewOfflineAccountNameInvalid);
				isNewOfflineAccountNameInvalid = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsNewOfflineAccountNameInvalid);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string MicrosoftLoginMessage
	{
		get
		{
			return microsoftLoginMessage;
		}
		[MemberNotNull("microsoftLoginMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(microsoftLoginMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MicrosoftLoginMessage);
				microsoftLoginMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MicrosoftLoginMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string MicrosoftLoginIcon
	{
		get
		{
			return microsoftLoginIcon;
		}
		[MemberNotNull("microsoftLoginIcon")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(microsoftLoginIcon, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MicrosoftLoginIcon);
				microsoftLoginIcon = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MicrosoftLoginIcon);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsMicrosoftLoginSuccessful
	{
		get
		{
			return isMicrosoftLoginSuccessful;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isMicrosoftLoginSuccessful, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsMicrosoftLoginSuccessful);
				isMicrosoftLoginSuccessful = value;
				OnIsMicrosoftLoginSuccessfulChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsMicrosoftLoginSuccessful);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsMicrosoftAccountAlreadyAdded
	{
		get
		{
			return isMicrosoftAccountAlreadyAdded;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isMicrosoftAccountAlreadyAdded, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsMicrosoftAccountAlreadyAdded);
				isMicrosoftAccountAlreadyAdded = value;
				OnIsMicrosoftAccountAlreadyAddedChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsMicrosoftAccountAlreadyAdded);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public LauncherAccount? AccountPendingMicrosoftReauthentication
	{
		get
		{
			return accountPendingMicrosoftReauthentication;
		}
		set
		{
			if (!EqualityComparer<LauncherAccount>.Default.Equals(accountPendingMicrosoftReauthentication, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AccountPendingMicrosoftReauthentication);
				accountPendingMicrosoftReauthentication = value;
				OnAccountPendingMicrosoftReauthenticationChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AccountPendingMicrosoftReauthentication);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public LauncherAccount? AccountPendingThirdPartyReauthentication
	{
		get
		{
			return accountPendingThirdPartyReauthentication;
		}
		set
		{
			if (!EqualityComparer<LauncherAccount>.Default.Equals(accountPendingThirdPartyReauthentication, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AccountPendingThirdPartyReauthentication);
				accountPendingThirdPartyReauthentication = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AccountPendingThirdPartyReauthentication);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ThirdPartyImportCurrentProfileName
	{
		get
		{
			return thirdPartyImportCurrentProfileName;
		}
		[MemberNotNull("thirdPartyImportCurrentProfileName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(thirdPartyImportCurrentProfileName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ThirdPartyImportCurrentProfileName);
				thirdPartyImportCurrentProfileName = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ThirdPartyImportCurrentProfileName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int ThirdPartyImportCompletedCount
	{
		get
		{
			return thirdPartyImportCompletedCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(thirdPartyImportCompletedCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ThirdPartyImportCompletedCount);
				thirdPartyImportCompletedCount = value;
				OnThirdPartyImportCompletedCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ThirdPartyImportCompletedCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int ThirdPartyImportTotalCount
	{
		get
		{
			return thirdPartyImportTotalCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(thirdPartyImportTotalCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ThirdPartyImportTotalCount);
				thirdPartyImportTotalCount = value;
				OnThirdPartyImportTotalCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ThirdPartyImportTotalCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int ThirdPartyImportFailedCount
	{
		get
		{
			return thirdPartyImportFailedCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(thirdPartyImportFailedCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ThirdPartyImportFailedCount);
				thirdPartyImportFailedCount = value;
				OnThirdPartyImportFailedCountChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ThirdPartyImportFailedCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsDeleteAccountDialogOpen
	{
		get
		{
			return isDeleteAccountDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isDeleteAccountDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDeleteAccountDialogOpen);
				isDeleteAccountDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDeleteAccountDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public LauncherAccount? AccountPendingDelete
	{
		get
		{
			return accountPendingDelete;
		}
		set
		{
			if (!EqualityComparer<LauncherAccount>.Default.Equals(accountPendingDelete, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AccountPendingDelete);
				accountPendingDelete = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AccountPendingDelete);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsRenameAccountDialogOpen
	{
		get
		{
			return isRenameAccountDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isRenameAccountDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsRenameAccountDialogOpen);
				isRenameAccountDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsRenameAccountDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsRenameAccountDialogBusy
	{
		get
		{
			return isRenameAccountDialogBusy;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isRenameAccountDialogBusy, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsRenameAccountDialogBusy);
				isRenameAccountDialogBusy = value;
				OnIsRenameAccountDialogBusyChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsRenameAccountDialogBusy);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public LauncherAccount? AccountPendingRename
	{
		get
		{
			return accountPendingRename;
		}
		set
		{
			if (!EqualityComparer<LauncherAccount>.Default.Equals(accountPendingRename, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AccountPendingRename);
				accountPendingRename = value;
				OnAccountPendingRenameChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AccountPendingRename);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string RenameAccountDialogStep
	{
		get
		{
			return renameAccountDialogStep;
		}
		[MemberNotNull("renameAccountDialogStep")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(renameAccountDialogStep, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RenameAccountDialogStep);
				renameAccountDialogStep = value;
				OnRenameAccountDialogStepChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RenameAccountDialogStep);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string RenameAccountName
	{
		get
		{
			return renameAccountName;
		}
		[MemberNotNull("renameAccountName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(renameAccountName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RenameAccountName);
				renameAccountName = value;
				OnRenameAccountNameChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RenameAccountName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsRenameAccountNameInvalid
	{
		get
		{
			return isRenameAccountNameInvalid;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isRenameAccountNameInvalid, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsRenameAccountNameInvalid);
				isRenameAccountNameInvalid = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsRenameAccountNameInvalid);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsRenameAccountSuccessful
	{
		get
		{
			return isRenameAccountSuccessful;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isRenameAccountSuccessful, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsRenameAccountSuccessful);
				isRenameAccountSuccessful = value;
				OnIsRenameAccountSuccessfulChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsRenameAccountSuccessful);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string RenameAccountMessage
	{
		get
		{
			return renameAccountMessage;
		}
		[MemberNotNull("renameAccountMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(renameAccountMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RenameAccountMessage);
				renameAccountMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RenameAccountMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string RenameAccountErrorCodeMessage
	{
		get
		{
			return renameAccountErrorCodeMessage;
		}
		[MemberNotNull("renameAccountErrorCodeMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(renameAccountErrorCodeMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RenameAccountErrorCodeMessage);
				renameAccountErrorCodeMessage = value;
				OnRenameAccountErrorCodeMessageChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RenameAccountErrorCodeMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string RenameAccountIcon
	{
		get
		{
			return renameAccountIcon;
		}
		[MemberNotNull("renameAccountIcon")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(renameAccountIcon, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RenameAccountIcon);
				renameAccountIcon = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RenameAccountIcon);
			}
		}
	}

	public event Action? DroppedThirdPartyAccountAdditionCompleted;

	public async Task ConfirmAddAccountDialogAsync(string? thirdPartyPassword = null)
	{
		if (!IsAddAccountDialogBusy)
		{
			string password = thirdPartyPassword ?? string.Empty;
			if (!(await TryHandleAddAccountResultStepAsync(password)) && !(await TryHandleThirdPartyReauthenticationAsync(password)) && !(await TryHandleThirdPartyCredentialsAsync(password)) && !(await TryHandleSteamTypeStepAsync()) && SelectedAccountTypeOption != null && !TryAdvanceAccountTypeStep())
			{
				await AddOfflineAccountAsync();
			}
		}
	}

	private async Task<bool> TryHandleAddAccountResultStepAsync(string thirdPartyPassword)
	{
		if (IsMicrosoftLoginResultStep)
		{
			CloseAddAccountDialogAfterMicrosoftResult();
			return true;
		}
		if (IsThirdPartyImportResultStep)
		{
			IsAddAccountDialogOpen = false;
			await ThirdParty.CancelEmailLoginAsync();
			if (thirdPartySuccessfulAccounts.Count > 0)
			{
				CompleteDroppedThirdPartyAccountAddition();
			}
			return true;
		}
		if (!IsThirdPartyProfileSelectionStep)
		{
			return false;
		}
		await ImportThirdPartyProfilesAsync(ThirdParty.Profiles.Where((ThirdPartyProfileOptionViewModel profile) => profile.IsSelected).ToArray(), thirdPartyPassword);
		return true;
	}

	private async Task<bool> TryHandleThirdPartyReauthenticationAsync(string thirdPartyPassword)
	{
		if (IsThirdPartyReauthenticationStep)
		{
			LauncherAccount pendingAccount = AccountPendingThirdPartyReauthentication;
			if (pendingAccount != null)
			{
				IsAddAccountDialogBusy = true;
				try
				{
					LauncherAccount launcherAccount = await ThirdParty.ReauthenticateAsync(pendingAccount, thirdPartyPassword);
					if (launcherAccount != null)
					{
						await accountList.ReplaceSelectedAccountAndPersistAsync(pendingAccount, launcherAccount);
						IsAddAccountDialogOpen = false;
					}
				}
				finally
				{
					IsAddAccountDialogBusy = false;
				}
				return true;
			}
		}
		return false;
	}

	private bool TryAdvanceAccountTypeStep()
	{
		if (!IsAccountTypeStep)
		{
			return false;
		}
		if (SelectedAccountTypeOption.Kind == "Offline")
		{
			AddAccountDialogStep = "OfflineName";
		}
		else if (SelectedAccountTypeOption.Kind == "ThirdParty")
		{
			AddAccountDialogStep = "ThirdPartyCredentials";
		}
		return true;
	}

	private async Task<bool> TryHandleThirdPartyCredentialsAsync(string thirdPartyPassword)
	{
		if (!IsThirdPartyCredentialsStep)
		{
			return false;
		}
		IsAddAccountDialogBusy = true;
		try
		{
			if (ThirdParty.IsEmailIdentifier)
			{
				if (await ThirdParty.BeginEmailLoginAsync(thirdPartyPassword))
				{
					AddAccountDialogStep = "ThirdPartyProfileSelection";
				}
			}
			else if (await ThirdParty.LoginAsync(thirdPartyPassword))
			{
				IsAddAccountDialogOpen = false;
				CompleteDroppedThirdPartyAccountAddition();
			}
		}
		finally
		{
			IsAddAccountDialogBusy = false;
		}
		return true;
	}

	private async Task AddOfflineAccountAsync()
	{
		string accountName = NewOfflineAccountName.Trim();
		if (!AccountNameValidator.IsValid(accountName))
		{
			IsNewOfflineAccountNameInvalid = true;
			ReportStatus(AccountNameValidator.ValidationMessage);
			return;
		}
		LauncherAccount account = new LauncherAccount
		{
			Id = $"offline-{Guid.NewGuid():N}",
			DisplayName = accountName,
			Uuid = offlineUuidService.CreateUuid(accountName, OfflineUuidGenerationMode.Standard),
			OfflineUuidGenerationMode = OfflineUuidGenerationMode.Standard,
			Kind = LauncherAccountKind.Offline
		};
		await accountList.AddAndSelectAsync(account);
		IsAddAccountDialogOpen = false;
		ReportStatus(string.Format(Strings.Status_OfflineAccountAddedFormat, accountName));
	}

	/// <summary>
	/// Steam 授权登录：类型选择步骤选中"Steam 账户"后点确定，
	/// 检测本机 Steam 客户端的当前登录用户（loginusers.vdf），导入昵称与头像（可离线）。
	/// 未检测到 Steam / 登录用户时停留本步骤并给出状态提示。
	/// </summary>
	private async Task<bool> TryHandleSteamTypeStepAsync()
	{
		if (!IsAccountTypeStep || SelectedAccountTypeOption?.Kind != "Steam")
		{
			return false;
		}
		IsAddAccountDialogBusy = true;
		try
		{
			ReportStatus(Strings.Status_SteamDetecting);
			SteamUserInfo? steamUser = await Task.Run(() => SteamLoginClient.DetectSignedInUser());
			if (steamUser == null || string.IsNullOrWhiteSpace(steamUser.SteamId64))
			{
				ReportStatus(Strings.Status_SteamNotDetected);
				return true;
			}

			// 1) 把 SteamID64 上报云端，服务器用 Web API Key 查昵称/头像/BeamNG 库存与时长
			var api = new ApiService();
			var auth = await api.SteamLoginAsync(steamUser.SteamId64, steamUser.DisplayName);
			if (auth == null || !string.IsNullOrEmpty(auth.Error))
			{
				// 云端不可用也允许本地导入（降级为纯本地识别，不阻断登录）
				logger?.LogWarning("Steam login: cloud auth failed ({Error}), fallback to local", auth?.Error);
			}
			else
			{
				AppState.Current.CloudSync.SaveAuth(auth);
				logger?.LogInformation("Steam login: cloud auth ok username={U} ownsBeamng={O} playtime={P}min",
					auth.Username, auth.OwnsBeamng, auth.BeamngPlaytimeMin);

				// 2) 库存检测：只有"明确没有 BeamNG"才拦截；无 Key（null）降级放行
				if (auth.OwnsBeamng == false)
				{
					ReportStatus(Strings.Status_SteamNoBeamng);
					IsAddAccountDialogBusy = false;
					return true;
				}
				// 明确拥有：提示时长；无法判断（null）：静默放行
				if (auth.OwnsBeamng == true)
				{
					ReportStatus(string.Format(Strings.Status_SteamBeamngFoundFormat,
						auth.BeamngPlaytimeMin / 60.0));
				}
			}

			string accountId = "steam-" + steamUser.SteamId64;

			// 先解析头像：本地 vdf hash 下载 → 云端 Web API 的 CDN 地址下载 → 退回远程 URL 直连
			string? avatarSource = null;
			if (!string.IsNullOrWhiteSpace(steamUser.AvatarHash))
			{
				try
				{
					avatarSource = await SteamLoginClient.DownloadAvatarToFileAsync(steamUser.AvatarHash, steamUser.SteamId64);
				}
				catch
				{
					avatarSource = null;
				}
			}
			if (string.IsNullOrWhiteSpace(avatarSource))
			{
				// 已有本地缓存直接复用（离线可用、避免重复下载）
				avatarSource = SteamLoginClient.GetCachedAvatarPath(steamUser.SteamId64);
			}
			if (string.IsNullOrWhiteSpace(avatarSource) && auth != null && !string.IsNullOrEmpty(auth.Avatar))
			{
				try
				{
					avatarSource = await SteamLoginClient.DownloadAvatarFromUrlAsync(auth.Avatar, steamUser.SteamId64);
				}
				catch
				{
					avatarSource = null;
				}
				if (string.IsNullOrWhiteSpace(avatarSource))
				{
					avatarSource = auth.Avatar;
				}
			}
			// 云端昵称（PersonaName）优先于本地 vdf 的登录名
			string displayName = !string.IsNullOrWhiteSpace(auth?.Username) ? auth.Username : steamUser.DisplayName;

			LauncherAccount? existing = null;
			foreach (AccountItemViewModel item in accountList.Accounts)
			{
				if (string.Equals(item.Id, accountId, StringComparison.Ordinal))
				{
					existing = item.Account;
					break;
				}
			}
			if (existing != null)
			{
				// 已有账户：补齐头像（无 API Key 时期导入的账户头像为空），昵称不覆盖（尊重用户改过的名字）
				if (!string.IsNullOrWhiteSpace(avatarSource)
					&& !string.Equals(existing.AvatarSource, avatarSource, StringComparison.Ordinal))
				{
					// 注意：WithAvatar 会按名字重算联机 ID（SteamID64 会被覆盖成本地 UUID），
					// 这里把联机 ID 强制写回 SteamID64，保证联机身份稳定、且与 Steam 账号一一对应。
					LauncherAccount refreshed = AccountMapper.WithAvatar(existing, avatarSource);
					refreshed = AccountMapper.WithOfflineUuid(refreshed, existing.OfflineUuidGenerationMode, steamUser.SteamId64);
					await accountList.ReplaceSelectedAccountAndPersistAsync(existing, refreshed);
					existing = refreshed;
					logger?.LogInformation("Steam login: existing account avatar refreshed -> {A} (uuid preserved={U})", avatarSource, refreshed.Uuid);
				}
				else
				{
					logger?.LogInformation("Steam login: account already exists, select it");
				}
				accountList.SelectAccount(existing, persistSelection: true);
				IsAddAccountDialogOpen = false;
				ReportStatus(string.Format(Strings.Status_SteamAccountAddedFormat, existing.DisplayName));
				return true;
			}
			LauncherAccount account = new LauncherAccount
			{
				Id = accountId,
				DisplayName = displayName,
				Uuid = steamUser.SteamId64,
				Kind = LauncherAccountKind.Offline,
				AvatarSource = avatarSource
			};
			logger?.LogInformation("Steam login: adding account name={Name} avatar={Avatar} uuid={Uuid}", account.DisplayName, avatarSource ?? "<none>", account.Uuid);
			await accountList.AddAndSelectAsync(account);
			logger?.LogInformation("Steam login: account added and selected");
			IsAddAccountDialogOpen = false;
			ReportStatus(string.Format(Strings.Status_SteamAccountAddedFormat, account.DisplayName));
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Steam login: import failed");
			ReportStatus(Strings.Status_SteamNotDetected);
		}
		finally
		{
			IsAddAccountDialogBusy = false;
		}
		return true;
	}

	public AccountDialogViewModel(AccountListViewModel accountList, IMicrosoftAccountService microsoftAccountService, IThirdPartyAccountService thirdPartyAccountService, IOfflineAccountUuidService offlineUuidService, IStatusService statusService, ILogger<AccountDialogViewModel>? logger = null)
	{
		this.accountList = accountList;
		this.microsoftAccountService = microsoftAccountService;
		this.thirdPartyAccountService = thirdPartyAccountService;
		this.offlineUuidService = offlineUuidService;
		this.statusService = statusService;
		this.logger = logger ?? NullLogger<AccountDialogViewModel>.Instance;
		ThirdParty = new ThirdPartyAccountDialogViewModel(accountList, thirdPartyAccountService, this.logger);
		ThirdParty.PropertyChanged += delegate(object? _, PropertyChangedEventArgs e)
		{
			string propertyName = e.PropertyName;
			if ((propertyName == "CanConfirm" || propertyName == "HasSelectedProfiles") ? true : false)
			{
				OnPropertyChanged("CanConfirmAddAccountDialog");
			}
			if (e.PropertyName == "CanSelectAllProfiles")
			{
				OnPropertyChanged("CanSelectAllThirdPartyProfiles");
			}
		};
	}

	public void OpenDeleteAccountDialog(LauncherAccount account)
	{
		AccountPendingDelete = account;
		IsDeleteAccountDialogOpen = true;
	}

	public void CancelDeleteAccountDialog()
	{
		IsDeleteAccountDialogOpen = false;
		AccountPendingDelete = null;
	}

	public async Task ConfirmDeleteAccountDialogAsync()
	{
		if (AccountPendingDelete == null)
		{
			return;
		}
		LauncherAccount account = AccountPendingDelete;
		string deletedName = account.DisplayName;
		Task task = accountList.RemoveAsync(account);
		IsDeleteAccountDialogOpen = false;
		AccountPendingDelete = null;
		ReportStatus(string.Format(Strings.Status_AccountDeletedFormat, deletedName));
		try
		{
			await task;
			if (account.IsMicrosoft)
			{
				await microsoftAccountService.DeleteAccountAsync(account);
			}
			else if (account.IsThirdParty)
			{
				await thirdPartyAccountService.DeleteCredentialsAsync(account.Id);
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Account delete cleanup failed. AccountId={AccountId} IsOffline={IsOffline}", account.Id, account.IsOffline);
			ReportStatus(string.Format(Strings.Status_AccountDeletedCacheCleanupFailedFormat, deletedName));
		}
	}

	public void OpenAddAccountDialog()
	{
		ResetAddAccountDialogState(clearOfflineName: true);
		IsAddAccountDialogOpen = true;
	}

	public void OpenThirdPartyReauthenticationDialog(LauncherAccount account)
	{
		ResetAddAccountDialogState(clearOfflineName: true);
		AccountPendingThirdPartyReauthentication = account;
		AddAccountDialogStep = "ThirdPartyReauthentication";
		ThirdParty.PrepareReauthentication(account);
		IsAddAccountDialogOpen = true;
	}

	public void OpenMicrosoftReauthenticationDialog(LauncherAccount account)
	{
		ResetAddAccountDialogState(clearOfflineName: true);
		AccountPendingMicrosoftReauthentication = account;
		AddAccountDialogStep = "MicrosoftReauthenticationPrompt";
		IsAddAccountDialogBusy = false;
		ResetMicrosoftLoginResultState(string.Format(Strings.Dialog_MicrosoftAccountExpiredMessageFormat, account.DisplayName));
		IsAddAccountDialogOpen = true;
		ReportStatus(Strings.Status_MicrosoftReauthenticationRequired);
	}

	public void BeginMicrosoftAccountReauthentication()
	{
		if (AccountPendingMicrosoftReauthentication != null)
		{
			AddAccountDialogStep = "MicrosoftReauthentication";
			IsAddAccountDialogBusy = true;
			BeginMicrosoftAuthenticationCancellation();
			ResetMicrosoftLoginResultState(MicrosoftLoginActiveMessage);
			ReportStatus(Strings.Status_OpeningMicrosoftLogin);
		}
	}

	public void CancelAddAccountDialog()
	{
		if (IsThirdPartyImportProgressStep)
		{
			thirdPartyImportCancellationTokenSource?.Cancel();
		}
		else if (CanShowMicrosoftAuthenticationCancelButton)
		{
			microsoftAuthenticationCancellationTokenSource?.Cancel();
		}
		else if (!IsAddAccountDialogBusy)
		{
			IsAddAccountDialogOpen = false;
			ThirdParty.CancelEmailLoginAsync();
		}
	}

	public void ResetAddAccountDialog()
	{
		bool num = hasPendingDroppedThirdPartyNavigation;
		ResetAddAccountDialogState(clearOfflineName: true);
		if (num)
		{
			DroppedThirdPartyAccountAdditionCompleted?.Invoke();
		}
	}

	public void BackToAddAccountTypeStep()
	{
		if (!IsAddAccountDialogBusy)
		{
			ResetAddAccountDialogState(clearOfflineName: false);
		}
	}

	public void BeginMicrosoftAccountLogin()
	{
		AddAccountDialogStep = "MicrosoftLogin";
		IsAddAccountDialogBusy = true;
		BeginMicrosoftAuthenticationCancellation();
		ResetMicrosoftLoginResultState(MicrosoftLoginActiveMessage);
		ReportStatus(Strings.Status_OpeningMicrosoftLogin);
	}

	public async Task CompleteMicrosoftAccountLoginAsync()
	{
		CancellationTokenSource authenticationCancellation = microsoftAuthenticationCancellationTokenSource ?? BeginMicrosoftAuthenticationCancellation();
		try
		{
			LauncherAccount account = await microsoftAccountService.LoginInteractivelyAsync(authenticationCancellation.Token);
			if (string.IsNullOrWhiteSpace(account.DisplayName) || string.IsNullOrWhiteSpace(account.Uuid))
			{
				string status_LoginMissingProfile = Strings.Status_LoginMissingProfile;
				ReportStatus(status_LoginMissingProfile);
				ShowMicrosoftLoginResult(isSuccess: false, status_LoginMissingProfile);
				return;
			}
			AccountItemViewModel existing = accountList.Accounts.FirstOrDefault((AccountItemViewModel item) => item.Account.IsMicrosoft && string.Equals(item.Uuid, account.Uuid, StringComparison.OrdinalIgnoreCase));
			if (existing != null)
			{
				accountList.SelectItem(existing, persistSelection: false);
				await accountList.PersistAccountOrderAsync();
				string message = string.Format(Strings.Status_LoginAccountAlreadyAddedFormat, existing.DisplayName);
				ReportStatus(message);
				ShowMicrosoftLoginResult(isSuccess: true, message, alreadyAdded: true);
			}
			else
			{
				await accountList.AddAndSelectAsync(account);
				string message2 = string.Format(Strings.Status_LoginAccountAddedFormat, account.DisplayName);
				ReportStatus(message2);
				ShowMicrosoftLoginResult(isSuccess: true, message2);
			}
		}
		catch (OperationCanceledException)
		{
			logger.LogInformation("Microsoft account login canceled.");
			string status_LoginCanceled = Strings.Status_LoginCanceled;
			ReportStatus(status_LoginCanceled);
			ShowMicrosoftLoginResult(isSuccess: false, status_LoginCanceled);
		}
		catch (MicrosoftAccountLoginException ex2)
		{
			logger.LogWarning("Microsoft account login failed. Reason={Reason}", ex2.Reason);
			string message3 = ex2.Reason switch
			{
				MicrosoftAccountLoginFailureReason.NotConfigured => Strings.Status_MicrosoftLoginNotConfigured, 
				MicrosoftAccountLoginFailureReason.ApplicationNotAuthorized => Strings.Status_MicrosoftApplicationNotAuthorized, 
				MicrosoftAccountLoginFailureReason.TimedOut => Strings.Status_MicrosoftAuthenticationTimedOut, 
				MicrosoftAccountLoginFailureReason.GameOwnershipRequired => Strings.Status_MinecraftJavaOwnershipRequired, 
				MicrosoftAccountLoginFailureReason.AuthenticationServerUnavailable => Strings.Status_MicrosoftAuthenticationServerUnavailable, 
				MicrosoftAccountLoginFailureReason.CredentialStorageFailed => Strings.Status_MicrosoftCredentialStorageFailed, 
				_ => Strings.Status_LoginFailed, 
			};
			ReportStatus(message3);
			ShowMicrosoftLoginResult(isSuccess: false, message3);
		}
		catch (Exception ex3)
		{
			logger.LogError("Microsoft account login failed. ErrorType={ErrorType}", ex3.GetType().FullName);
			string status_LoginFailed = Strings.Status_LoginFailed;
			ReportStatus(status_LoginFailed);
			ShowMicrosoftLoginResult(isSuccess: false, status_LoginFailed);
		}
		finally
		{
			CompleteMicrosoftAuthenticationCancellation(authenticationCancellation);
			IsAddAccountDialogBusy = false;
		}
	}

	public void CloseAddAccountDialogAfterMicrosoftResult()
	{
		if (!IsAddAccountDialogBusy)
		{
			IsAddAccountDialogOpen = false;
		}
	}

	public async Task<bool> CompleteMicrosoftAccountReauthenticationAsync()
	{
		LauncherAccount account = AccountPendingMicrosoftReauthentication;
		if (account == null)
		{
			return false;
		}
		BeginMicrosoftAccountReauthentication();
		CancellationTokenSource authenticationCancellation = microsoftAuthenticationCancellationTokenSource ?? BeginMicrosoftAuthenticationCancellation();
		try
		{
			LauncherAccount refreshed = await microsoftAccountService.ReauthenticateInteractivelyAsync(account, authenticationCancellation.Token);
			await accountList.ReplaceSelectedAccountAndPersistAsync(account, refreshed);
			AccountPendingMicrosoftReauthentication = refreshed;
			ReportStatus(Strings.Status_MicrosoftReauthenticationSuccessful);
			IsAddAccountDialogOpen = false;
			return true;
		}
		catch (OperationCanceledException)
		{
			logger.LogInformation("Microsoft account reauthentication canceled. AccountId={AccountId}", account.Id);
			ShowMicrosoftReauthenticationFailure(Strings.Status_LoginCanceled);
			return false;
		}
		catch (MicrosoftAccountReauthenticationException ex2)
		{
			logger.LogWarning("Microsoft account reauthentication failed. AccountId={AccountId} Reason={Reason}", account.Id, ex2.Reason);
			ShowMicrosoftReauthenticationFailure(ex2.Reason switch
			{
				MicrosoftAccountReauthenticationFailureReason.NotConfigured => Strings.Status_MicrosoftLoginNotConfigured, 
				MicrosoftAccountReauthenticationFailureReason.ApplicationNotAuthorized => Strings.Status_MicrosoftApplicationNotAuthorized, 
				MicrosoftAccountReauthenticationFailureReason.TimedOut => Strings.Status_MicrosoftAuthenticationTimedOut, 
				MicrosoftAccountReauthenticationFailureReason.GameOwnershipRequired => Strings.Status_MinecraftJavaOwnershipRequired, 
				MicrosoftAccountReauthenticationFailureReason.AuthenticationServerUnavailable => Strings.Status_MicrosoftAuthenticationServerUnavailable, 
				MicrosoftAccountReauthenticationFailureReason.AccountMismatch => Strings.Status_MicrosoftReauthenticationAccountMismatch, 
				MicrosoftAccountReauthenticationFailureReason.CredentialStorageFailed => Strings.Status_MicrosoftCredentialStorageFailed, 
				_ => Strings.Status_LoginFailed, 
			});
			return false;
		}
		catch (Exception ex3)
		{
			logger.LogError("Microsoft account reauthentication failed. AccountId={AccountId} ErrorType={ErrorType}", account.Id, ex3.GetType().FullName);
			ShowMicrosoftReauthenticationFailure(Strings.Status_LoginFailed);
			return false;
		}
		finally
		{
			CompleteMicrosoftAuthenticationCancellation(authenticationCancellation);
			IsAddAccountDialogBusy = false;
		}
	}

	private async Task SelectLastSuccessfulThirdPartyAccountAsync()
	{
		if (thirdPartySuccessfulAccounts.Count != 0)
		{
			AccountListViewModel accountListViewModel = accountList;
			List<LauncherAccount> list = thirdPartySuccessfulAccounts;
			LauncherAccount launcherAccount = accountListViewModel.FindAccount(list[list.Count - 1].Id);
			if (launcherAccount != null)
			{
				accountList.SelectAccount(launcherAccount, persistSelection: false);
				await accountList.PersistAccountOrderAsync();
			}
		}
	}

	public void OpenRenameAccountDialog()
	{
		if (accountList.SelectedAccount != null && !accountList.SelectedAccount.IsThirdParty)
		{
			AccountPendingRename = accountList.SelectedAccount;
			ResetRenameAccountDialogState(accountList.SelectedAccount.DisplayName);
			IsRenameAccountDialogOpen = true;
		}
	}

	public void CancelRenameAccountDialog()
	{
		if (!IsRenameAccountDialogBusy)
		{
			IsRenameAccountDialogOpen = false;
		}
	}

	public void ResetRenameAccountDialog()
	{
		AccountPendingRename = null;
		ResetRenameAccountDialogState(string.Empty);
	}

	public async Task ConfirmRenameAccountDialogAsync()
	{
		if (IsRenameAccountDialogBusy)
		{
			return;
		}
		if (IsRenameAccountResultStep)
		{
			IsRenameAccountDialogOpen = false;
			return;
		}
		LauncherAccount account = AccountPendingRename;
		if (account == null)
		{
			return;
		}
		if (account.IsThirdParty)
		{
			IsRenameAccountDialogOpen = false;
			return;
		}
		string text = RenameAccountName.Trim();
		if (!AccountNameValidator.IsValid(text))
		{
			IsRenameAccountNameInvalid = true;
			ReportStatus(AccountNameValidator.ValidationMessage);
			return;
		}
		if (string.Equals(text, account.DisplayName, StringComparison.Ordinal))
		{
			ShowRenameAccountResult(isSuccess: true, Strings.Status_AccountNameUnchanged);
			return;
		}
		try
		{
			IsRenameAccountDialogBusy = true;
			RenameAccountDialogStep = "Status";
			RenameAccountIcon = "\ue895";
			RenameAccountMessage = (account.IsOffline ? Strings.Status_SavingOfflineAccountName : Strings.Status_ChangingMicrosoftAccountName);
			LauncherAccount updatedAccount;
			if (account.IsOffline)
			{
				string uuid = offlineUuidService.CreateUuid(text, account.OfflineUuidGenerationMode, account.Uuid);
				updatedAccount = AccountMapper.WithDisplayNameAndOfflineUuid(account, text, account.OfflineUuidGenerationMode, uuid);
			}
			else
			{
				updatedAccount = AccountMapper.WithCapeCache(AccountMapper.WithAppearanceFallback(await microsoftAccountService.ChangeNameAsync(account, text), account), account.CachedCapeOptions);
			}
			accountList.ReplaceSelectedAccount(account, updatedAccount);
			AccountPendingRename = updatedAccount;
			await accountList.PersistAccountOrderAsync();
			string message = string.Format(Strings.Status_AccountRenamedFormat, updatedAccount.DisplayName);
			ReportStatus(message);
			ShowRenameAccountResult(isSuccess: true, string.Format(Strings.Status_AccountRenameResultFormat, updatedAccount.DisplayName));
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Account rename failed. AccountId={AccountId} IsOffline={IsOffline} ErrorCode={ErrorCode}", account.Id, account.IsOffline, AccountErrorCodeMessageFormatter.Format(exception));
			string renameFailureMessage = GetRenameFailureMessage(exception);
			string errorCodeMessage = AccountErrorCodeMessageFormatter.Format(exception);
			ReportStatus(renameFailureMessage);
			ShowRenameAccountResult(isSuccess: false, renameFailureMessage, errorCodeMessage);
		}
		finally
		{
			IsRenameAccountDialogBusy = false;
		}
	}

	private void NotifyAddAccountDialogStepPropertiesChanged()
	{
		OnPropertyChanged("IsAccountTypeStep");
		OnPropertyChanged("IsOfflineNameStep");
		OnPropertyChanged("IsThirdPartyCredentialsStep");
		OnPropertyChanged("IsThirdPartyReauthenticationStep");
		OnPropertyChanged("IsThirdPartyFormStep");
		OnPropertyChanged("IsThirdPartyProfileSelectionStep");
		OnPropertyChanged("IsThirdPartyImportProgressStep");
		OnPropertyChanged("IsThirdPartyImportResultStep");
		OnPropertyChanged("CanSelectAllThirdPartyProfiles");
		OnPropertyChanged("CanShowStandardAddAccountFooter");
		OnPropertyChanged("IsThirdPartyIdentityReadOnly");
		OnPropertyChanged("IsMicrosoftLoginStep");
		OnPropertyChanged("IsMicrosoftLoginResultStep");
		OnPropertyChanged("IsMicrosoftReauthenticationPromptStep");
		OnPropertyChanged("IsMicrosoftReauthenticationStep");
		OnPropertyChanged("IsMicrosoftReauthenticationResultStep");
		OnPropertyChanged("IsMicrosoftStatusStep");
		OnPropertyChanged("MicrosoftLoginIconKey");
		OnPropertyChanged("AddAccountConfirmButtonText");
		NotifyAddAccountDialogActionPropertiesChanged();
		OnPropertyChanged("IsMicrosoftAccountTypeSelected");
		OnPropertyChanged("AddAccountDialogTitle");
		OnPropertyChanged("AddAccountDialogSubtitle");
	}

	private void NotifyAddAccountDialogActionPropertiesChanged()
	{
		OnPropertyChanged("CanShowAddAccountBackButton");
		OnPropertyChanged("CanShowAddAccountCancelButton");
		OnPropertyChanged("CanShowMicrosoftAuthenticationCancelButton");
		OnPropertyChanged("CanShowStandardAddAccountFooter");
		OnPropertyChanged("IsAddAccountFooterEnabled");
		OnPropertyChanged("CanConfirmAddAccountDialog");
	}

	private void NotifyRenameAccountDialogStepPropertiesChanged()
	{
		OnPropertyChanged("IsRenameAccountInputStep");
		OnPropertyChanged("IsRenameAccountStatusStep");
		OnPropertyChanged("IsRenameAccountResultStep");
		OnPropertyChanged("IsRenameAccountMessageStep");
		OnPropertyChanged("RenameAccountIconKey");
		NotifyRenameAccountDialogActionPropertiesChanged();
		OnPropertyChanged("RenameAccountDialogTitle");
		OnPropertyChanged("RenameAccountDialogSubtitle");
	}

	private void NotifyRenameAccountDialogActionPropertiesChanged()
	{
		OnPropertyChanged("CanShowRenameAccountCancelButton");
		OnPropertyChanged("CanConfirmRenameAccountDialog");
	}

	private void ResetAddAccountDialogState(bool clearOfflineName)
	{
		CancelAndDisposeMicrosoftAuthentication();
		thirdPartyImportCancellationTokenSource?.Cancel();
		thirdPartyImportCancellationTokenSource = null;
		thirdPartyFailedProfiles.Clear();
		thirdPartySuccessfulAccounts.Clear();
		ThirdPartyImportCurrentProfileName = string.Empty;
		ThirdPartyImportCompletedCount = 0;
		ThirdPartyImportTotalCount = 0;
		ThirdPartyImportFailedCount = 0;
		AccountPendingThirdPartyReauthentication = null;
		AccountPendingMicrosoftReauthentication = null;
		isDirectThirdPartyAddAccountEntry = false;
		navigateToAccountAfterThirdPartyAddition = false;
		hasPendingDroppedThirdPartyNavigation = false;
		AddAccountDialogStep = "Type";
		if (clearOfflineName)
		{
			NewOfflineAccountName = string.Empty;
			ThirdParty.Reset();
		}
		IsNewOfflineAccountNameInvalid = false;
		IsAddAccountDialogBusy = false;
		ResetMicrosoftLoginResultState();
		// 只剩 Steam 一种账户类型时直接选中，"确定"即可一键授权，省去一次点击。
		SelectedAccountTypeOption = AccountTypeOptions.Count == 1 ? AccountTypeOptions[0] : null;
	}

	private CancellationTokenSource BeginMicrosoftAuthenticationCancellation()
	{
		CancelAndDisposeMicrosoftAuthentication();
		microsoftAuthenticationCancellationTokenSource = new CancellationTokenSource();
		return microsoftAuthenticationCancellationTokenSource;
	}

	private void CompleteMicrosoftAuthenticationCancellation(CancellationTokenSource cancellationTokenSource)
	{
		if (microsoftAuthenticationCancellationTokenSource == cancellationTokenSource)
		{
			microsoftAuthenticationCancellationTokenSource.Dispose();
			microsoftAuthenticationCancellationTokenSource = null;
		}
	}

	private void CancelAndDisposeMicrosoftAuthentication()
	{
		microsoftAuthenticationCancellationTokenSource?.Cancel();
		microsoftAuthenticationCancellationTokenSource?.Dispose();
		microsoftAuthenticationCancellationTokenSource = null;
	}

	private void ResetMicrosoftLoginResultState(string? message = null)
	{
		IsMicrosoftLoginSuccessful = false;
		IsMicrosoftAccountAlreadyAdded = false;
		MicrosoftLoginIcon = "\ue895";
		MicrosoftLoginMessage = message ?? MicrosoftLoginInitialMessage;
	}

	private void ResetRenameAccountDialogState(string accountName)
	{
		IsRenameAccountDialogBusy = false;
		RenameAccountDialogStep = "Input";
		RenameAccountName = accountName;
		IsRenameAccountNameInvalid = false;
		IsRenameAccountSuccessful = false;
		RenameAccountIcon = "\ue70f";
		RenameAccountMessage = string.Empty;
		RenameAccountErrorCodeMessage = string.Empty;
	}

	private void ShowMicrosoftLoginResult(bool isSuccess, string message, bool alreadyAdded = false)
	{
		IsMicrosoftLoginSuccessful = isSuccess;
		IsMicrosoftAccountAlreadyAdded = alreadyAdded;
		MicrosoftLoginIcon = (isSuccess ? "\ue73e" : "\ue783");
		MicrosoftLoginMessage = message;
		AddAccountDialogStep = "MicrosoftResult";
	}

	private void ShowMicrosoftReauthenticationFailure(string message)
	{
		IsMicrosoftLoginSuccessful = false;
		IsMicrosoftAccountAlreadyAdded = false;
		MicrosoftLoginIcon = "\ue783";
		MicrosoftLoginMessage = message;
		AddAccountDialogStep = "MicrosoftReauthenticationResult";
		ReportStatus(message);
	}

	private void ShowRenameAccountResult(bool isSuccess, string message, string errorCodeMessage = "")
	{
		IsRenameAccountSuccessful = isSuccess;
		RenameAccountIcon = (isSuccess ? "\ue73e" : "\ue783");
		RenameAccountMessage = message;
		RenameAccountErrorCodeMessage = (isSuccess ? string.Empty : errorCodeMessage);
		RenameAccountDialogStep = "Result";
	}

	private static string GetRenameFailureMessage(Exception exception)
	{
		if (exception is MicrosoftAccountNameChangeException ex)
		{
			return ex.Reason switch
			{
				MicrosoftAccountNameChangeFailureReason.DuplicateName => Strings.Status_AccountRenameFailedDuplicateName, 
				MicrosoftAccountNameChangeFailureReason.NotAllowed => Strings.Status_AccountRenameFailedNotAllowed, 
				MicrosoftAccountNameChangeFailureReason.InvalidName => Strings.Status_AccountRenameFailedInvalidName, 
				_ => Strings.Status_AccountRenameFailed, 
			};
		}
		return Strings.Status_AccountRenameFailed;
	}

	private void ReportStatus(string message)
	{
		statusService.Report(message);
	}

	public void OpenThirdPartyAddAccountDialog(string authenticationServer)
	{
		ResetAddAccountDialogState(clearOfflineName: true);
		isDirectThirdPartyAddAccountEntry = true;
		navigateToAccountAfterThirdPartyAddition = true;
		ThirdParty.AuthenticationServer = authenticationServer;
		AddAccountDialogStep = "ThirdPartyCredentials";
		IsAddAccountDialogOpen = true;
	}

	public bool ApplyThirdPartyAuthenticationServer(string authenticationServer)
	{
		if (!IsAccountTypeStep && !IsThirdPartyCredentialsStep)
		{
			return false;
		}
		navigateToAccountAfterThirdPartyAddition = true;
		bool num = !string.Equals(ThirdParty.AuthenticationServer, authenticationServer, StringComparison.Ordinal);
		if (num)
		{
			ThirdParty.ResetCredentials();
		}
		ThirdParty.AuthenticationServer = authenticationServer;
		if (IsAccountTypeStep)
		{
			AddAccountDialogStep = "ThirdPartyCredentials";
		}
		return num;
	}

	public void SelectAllThirdPartyProfiles()
	{
		ThirdParty.SelectAllProfiles();
	}

	public Task RetryThirdPartyProfileImportAsync(string password)
	{
		return ImportThirdPartyProfilesAsync(thirdPartyFailedProfiles.ToArray(), password);
	}

	private async Task ImportThirdPartyProfilesAsync(IReadOnlyList<ThirdPartyProfileOptionViewModel> profiles, string password)
	{
		if (profiles.Count == 0)
		{
			return;
		}
		thirdPartyFailedProfiles.Clear();
		ThirdPartyImportFailedCount = 0;
		ThirdPartyImportCompletedCount = 0;
		ThirdPartyImportTotalCount = profiles.Count;
		AddAccountDialogStep = "ThirdPartyImportProgress";
		IsAddAccountDialogBusy = true;
		using CancellationTokenSource cancellation = new CancellationTokenSource();
		thirdPartyImportCancellationTokenSource = cancellation;
		try
		{
			int num = 0;
			try
			{
				foreach (ThirdPartyProfileOptionViewModel profile in profiles)
				{
					cancellation.Token.ThrowIfCancellationRequested();
					ThirdPartyImportCurrentProfileName = profile.Name;
					LauncherAccount imported = await ThirdParty.ImportEmailProfileAsync(profile, password, cancellation.Token);
					if (imported == null)
					{
						thirdPartyFailedProfiles.Add(profile);
					}
					else if (thirdPartySuccessfulAccounts.All((LauncherAccount account) => !string.Equals(account.Id, imported.Id, StringComparison.Ordinal)))
					{
						thirdPartySuccessfulAccounts.Add(imported);
					}
					ThirdPartyImportCompletedCount++;
				}
				await SelectLastSuccessfulThirdPartyAccountAsync();
				ThirdPartyImportFailedCount = thirdPartyFailedProfiles.Count;
				if (thirdPartyFailedProfiles.Count == 0)
				{
					IsAddAccountDialogOpen = false;
					await ThirdParty.CancelEmailLoginAsync();
					CompleteDroppedThirdPartyAccountAddition();
				}
				else
				{
					AddAccountDialogStep = "ThirdPartyImportResult";
				}
			}
			catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
			{
				num = 1;
			}
			if (num == 1)
			{
				await SelectLastSuccessfulThirdPartyAccountAsync();
				IsAddAccountDialogOpen = false;
				await ThirdParty.CancelEmailLoginAsync();
				if (thirdPartySuccessfulAccounts.Count > 0)
				{
					CompleteDroppedThirdPartyAccountAddition();
				}
			}
		}
		finally
		{
			thirdPartyImportCancellationTokenSource = null;
			IsAddAccountDialogBusy = false;
		}
	}

	private void CompleteDroppedThirdPartyAccountAddition()
	{
		if (navigateToAccountAfterThirdPartyAddition)
		{
			isDirectThirdPartyAddAccountEntry = false;
			navigateToAccountAfterThirdPartyAddition = false;
			hasPendingDroppedThirdPartyNavigation = true;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsAddAccountDialogBusyChanged(bool value)
	{
		NotifyAddAccountDialogActionPropertiesChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedAccountTypeOptionChanged(AccountTypeOption? value)
	{
		OnPropertyChanged("IsMicrosoftAccountTypeSelected");
		OnPropertyChanged("CanConfirmAddAccountDialog");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAddAccountDialogStepChanged(string value)
	{
		NotifyAddAccountDialogStepPropertiesChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnNewOfflineAccountNameChanged(string value)
	{
		IsNewOfflineAccountNameInvalid = false;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsMicrosoftLoginSuccessfulChanged(bool value)
	{
		OnPropertyChanged("AddAccountDialogTitle");
		OnPropertyChanged("MicrosoftLoginIconKey");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsMicrosoftAccountAlreadyAddedChanged(bool value)
	{
		OnPropertyChanged("AddAccountDialogTitle");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAccountPendingMicrosoftReauthenticationChanged(LauncherAccount? value)
	{
		OnPropertyChanged("IsMicrosoftReauthenticationMode");
		NotifyAddAccountDialogActionPropertiesChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnThirdPartyImportCompletedCountChanged(int value)
	{
		OnPropertyChanged("ThirdPartyImportProgressText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnThirdPartyImportTotalCountChanged(int value)
	{
		OnPropertyChanged("ThirdPartyImportProgressText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnThirdPartyImportFailedCountChanged(int value)
	{
		OnPropertyChanged("ThirdPartyImportFailureText");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsRenameAccountDialogBusyChanged(bool value)
	{
		NotifyRenameAccountDialogActionPropertiesChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAccountPendingRenameChanged(LauncherAccount? value)
	{
		OnPropertyChanged("IsRenameMicrosoftAccount");
		OnPropertyChanged("RenameAccountDialogSubtitle");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnRenameAccountDialogStepChanged(string value)
	{
		NotifyRenameAccountDialogStepPropertiesChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnRenameAccountNameChanged(string value)
	{
		IsRenameAccountNameInvalid = false;
		OnPropertyChanged("CanConfirmRenameAccountDialog");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsRenameAccountSuccessfulChanged(bool value)
	{
		OnPropertyChanged("RenameAccountDialogTitle");
		OnPropertyChanged("RenameAccountIconKey");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnRenameAccountErrorCodeMessageChanged(string value)
	{
		OnPropertyChanged("HasRenameAccountErrorCode");
	}
}
