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
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Application.Accounts;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace StartRide.App.ViewModels.Account;

public sealed class AccountSkinLibraryViewModel : ObservableObject
{
	private readonly AccountListViewModel accountList;

	private readonly IMicrosoftAccountService microsoftAccountService;

	private readonly IAccountSkinLibraryService skinLibraryService;

	private readonly IAccountDialogService dialogService;

	private readonly IFilePickerService filePickerService;

	private readonly IMinecraftSkinFileValidator skinFileValidator;

	private readonly AccountProfileViewModel profile;

	private readonly MicrosoftAccountOperationRetryHandler microsoftOperationRetryHandler;

	private readonly ILogger logger;

	private LauncherSkinRecord? skinPendingModelChange;

	[ObservableProperty]
	private LauncherSkinRecord? selectedSkin;

	[ObservableProperty]
	private bool isManagerDialogOpen;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? pickAndChangeSkinCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestOpenManagerDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestCancelManagerDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<LauncherSkinRecord?>? selectSkinCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? applySkinCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? changeSelectedSkinModelCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<LauncherSkinRecord?>? changeSkinModelCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? deleteSelectedSkinCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<LauncherSkinRecord?>? deleteSkinCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestCancelModelDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? requestConfirmModelDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? selectPreviousCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? selectNextCommand;

	public AccountSkinModelDialogViewModel SkinModelDialog { get; }

	public ObservableCollection<LauncherSkinRecord> Skins { get; } = new ObservableCollection<LauncherSkinRecord>();

	public LauncherSkinRecord? PreviousSkin => GetAdjacent(-1);

	public LauncherSkinRecord? NextSkin => GetAdjacent(1);

	public bool HasSkins => Skins.Count > 0;

	public bool IsOffline => accountList.SelectedAccount?.IsOffline ?? false;

	public bool IsThirdParty => accountList.SelectedAccount?.IsThirdParty ?? false;

	public bool CanShowStandardActions => !IsThirdParty;

	public bool CanShowThirdPartyRefresh => IsThirdParty;

	public bool HasPreview => SelectedSkin != null;

	public bool CanShowPreviewEmptyState
	{
		get
		{
			if (accountList.SelectedAccount != null)
			{
				return !HasPreview;
			}
			return false;
		}
	}

	public bool CanChangeSkin
	{
		get
		{
			LauncherAccount selectedAccount = accountList.SelectedAccount;
			if (selectedAccount != null)
			{
				return !selectedAccount.IsThirdParty;
			}
			return false;
		}
	}

	public bool CanManageSkins
	{
		get
		{
			LauncherAccount selectedAccount = accountList.SelectedAccount;
			if (selectedAccount != null)
			{
				return !selectedAccount.IsThirdParty;
			}
			return false;
		}
	}

	public bool CanApplySkin
	{
		get
		{
			LauncherAccount selectedAccount = accountList.SelectedAccount;
			if (selectedAccount != null && !selectedAccount.IsThirdParty && !profile.IsBusy)
			{
				LauncherSkinRecord launcherSkinRecord = SelectedSkin;
				if (launcherSkinRecord != null)
				{
					if (IsAlreadyApplied(selectedAccount, launcherSkinRecord))
					{
						return NeedsOfflineAvatarRefresh(selectedAccount);
					}
					return true;
				}
			}
			return false;
		}
	}

	public bool CanEditSelectedSkin
	{
		get
		{
			LauncherAccount selectedAccount = accountList.SelectedAccount;
			if (selectedAccount != null && !selectedAccount.IsThirdParty && !profile.IsBusy)
			{
				LauncherSkinRecord launcherSkinRecord = SelectedSkin;
				if (launcherSkinRecord != null)
				{
					return !IsActiveForSharedAccount(launcherSkinRecord);
				}
			}
			return false;
		}
	}

	public bool CanDeleteSelectedSkin
	{
		get
		{
			if (CanEditSelectedSkin)
			{
				LauncherAccount selectedAccount = accountList.SelectedAccount;
				if (selectedAccount != null)
				{
					LauncherSkinRecord launcherSkinRecord = SelectedSkin;
					if (launcherSkinRecord != null)
					{
						return !string.Equals(selectedAccount.ActiveSkinId, launcherSkinRecord.Id, StringComparison.Ordinal);
					}
				}
			}
			return false;
		}
	}

	public bool CanShowManagerEmptyState => !HasSkins;

	public string? ActiveSkinId => accountList.SelectedAccount?.ActiveSkinId;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public LauncherSkinRecord? SelectedSkin
	{
		get
		{
			return selectedSkin;
		}
		set
		{
			if (!EqualityComparer<LauncherSkinRecord>.Default.Equals(selectedSkin, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedSkin);
				selectedSkin = value;
				OnSelectedSkinChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedSkin);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsManagerDialogOpen
	{
		get
		{
			return isManagerDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isManagerDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsManagerDialogOpen);
				isManagerDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsManagerDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand PickAndChangeSkinCommand => pickAndChangeSkinCommand ?? (pickAndChangeSkinCommand = new AsyncRelayCommand(PickAndChangeSkinAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestOpenManagerDialogCommand => requestOpenManagerDialogCommand ?? (requestOpenManagerDialogCommand = new RelayCommand(RequestOpenManagerDialog, () => CanManageSkins));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestCancelManagerDialogCommand => requestCancelManagerDialogCommand ?? (requestCancelManagerDialogCommand = new RelayCommand(RequestCancelManagerDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<LauncherSkinRecord?> SelectSkinCommand => selectSkinCommand ?? (selectSkinCommand = new RelayCommand<LauncherSkinRecord>(SelectSkin));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ApplySkinCommand => applySkinCommand ?? (applySkinCommand = new AsyncRelayCommand(ApplySkinAsync, () => CanApplySkin));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ChangeSelectedSkinModelCommand => changeSelectedSkinModelCommand ?? (changeSelectedSkinModelCommand = new RelayCommand(ChangeSelectedSkinModel, () => CanEditSelectedSkin));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<LauncherSkinRecord?> ChangeSkinModelCommand => changeSkinModelCommand ?? (changeSkinModelCommand = new RelayCommand<LauncherSkinRecord>(ChangeSkinModel, CanChangeSkinModel));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand DeleteSelectedSkinCommand => deleteSelectedSkinCommand ?? (deleteSelectedSkinCommand = new AsyncRelayCommand(DeleteSelectedSkinAsync, () => CanDeleteSelectedSkin));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<LauncherSkinRecord?> DeleteSkinCommand => deleteSkinCommand ?? (deleteSkinCommand = new AsyncRelayCommand<LauncherSkinRecord>(DeleteSkinAsync, CanDeleteSkin));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestCancelModelDialogCommand => requestCancelModelDialogCommand ?? (requestCancelModelDialogCommand = new RelayCommand(RequestCancelModelDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RequestConfirmModelDialogCommand => requestConfirmModelDialogCommand ?? (requestConfirmModelDialogCommand = new AsyncRelayCommand(RequestConfirmModelDialogAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand SelectPreviousCommand => selectPreviousCommand ?? (selectPreviousCommand = new RelayCommand(SelectPrevious, CanSelectPrevious));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand SelectNextCommand => selectNextCommand ?? (selectNextCommand = new RelayCommand(SelectNext, CanSelectNext));

	internal AccountSkinLibraryViewModel(AccountListViewModel accountList, IMicrosoftAccountService microsoftAccountService, IAccountSkinLibraryService skinLibraryService, AccountSkinModelDialogViewModel skinModelDialog, IAccountDialogService dialogService, IFilePickerService filePickerService, IMinecraftSkinFileValidator skinFileValidator, AccountProfileViewModel profile, MicrosoftAccountOperationRetryHandler microsoftOperationRetryHandler, ILogger logger)
	{
		this.accountList = accountList;
		this.microsoftAccountService = microsoftAccountService;
		this.skinLibraryService = skinLibraryService;
		SkinModelDialog = skinModelDialog;
		this.dialogService = dialogService;
		this.filePickerService = filePickerService;
		this.skinFileValidator = skinFileValidator;
		this.profile = profile;
		this.microsoftOperationRetryHandler = microsoftOperationRetryHandler;
		this.logger = logger;
		profile.PropertyChanged += delegate
		{
			NotifyCommandState();
		};
	}

	public void SetAccount(LauncherAccount? account)
	{
		skinPendingModelChange = null;
		if (account == null)
		{
			Skins.Clear();
			SelectedSkin = null;
			IsManagerDialogOpen = false;
		}
		else
		{
			Populate(account, account.ActiveSkinId);
		}
		NotifyState();
	}

	public async Task ConfirmSkinModelDialogAsync()
	{
		string skinFilePath;
		MinecraftSkinModel skinModel;
		if (SkinModelDialog.IsSkinFormatError)
		{
			SkinModelDialog.Cancel();
		}
		else if (SkinModelDialog.TryConsumeSelection(out skinFilePath, out skinModel))
		{
			LauncherSkinRecord launcherSkinRecord = skinPendingModelChange;
			if (launcherSkinRecord != null)
			{
				skinPendingModelChange = null;
				await ChangeSkinModelAsync(launcherSkinRecord, skinModel);
			}
			else
			{
				await AddSkinAsync(skinFilePath, skinModel);
			}
		}
	}

	[RelayCommand]
	public async Task PickAndChangeSkinAsync()
	{
		if (!CanChangeSkin)
		{
			return;
		}
		string path = filePickerService.PickMinecraftSkin();
		if (!string.IsNullOrWhiteSpace(path))
		{
			if ((await skinFileValidator.ValidateAsync(path)).IsValid)
			{
				dialogService.ShowSkinModelDialog(path);
			}
			else
			{
				dialogService.ShowSkinFormatErrorDialog();
			}
		}
	}

	[RelayCommand(CanExecute = "CanManageSkins")]
	public void RequestOpenManagerDialog()
	{
		dialogService.ShowSkinManagerDialog();
	}

	[RelayCommand]
	public void RequestCancelManagerDialog()
	{
		dialogService.CancelSkinManagerDialog();
	}

	public void OpenManagerDialog()
	{
		if (CanManageSkins)
		{
			IsManagerDialogOpen = true;
		}
	}

	public void CloseManagerDialog()
	{
		IsManagerDialogOpen = false;
	}

	[RelayCommand]
	public void SelectSkin(LauncherSkinRecord? skin)
	{
		if (skin != null && Skins.Any((LauncherSkinRecord candidate) => string.Equals(candidate.Id, skin.Id, StringComparison.Ordinal)))
		{
			SelectedSkin = skin;
		}
	}

	[RelayCommand(CanExecute = "CanApplySkin")]
	public async Task ApplySkinAsync()
	{
		LauncherAccount account = accountList.SelectedAccount;
		LauncherSkinRecord skin = SelectedSkin;
		if (account == null || skin == null || !CanApplySkin)
		{
			return;
		}
		AccountAppearanceOperation operation = profile.BeginOperation(account, account.IsMicrosoft ? Strings.Status_UploadingSkin : string.Empty);
		try
		{
			if (account.IsOffline)
			{
				string avatarSource = await skinLibraryService.CreateAvatarSourceAsync(account, skin, operation.Token);
				if (profile.IsCurrent(account, operation))
				{
					LauncherAccount launcherAccount = AccountMapper.WithAvatar(AccountMapper.WithSkinLibrary(account, new global::_003C_003Ez__ReadOnlySingleElementList<LauncherSkinRecord>(skin), skin.Id, skin.Source, skin.SkinModel), avatarSource);
					accountList.ReplaceSelectedAccount(account, launcherAccount);
					Populate(launcherAccount, skin.Id);
					await accountList.PersistAccountOrderAsync();
					profile.SetMessage(Strings.Status_SkinUpdated, showFloating: true);
				}
				return;
			}
			MicrosoftAccountOperationResult<LauncherAccount> microsoftAccountOperationResult = await microsoftOperationRetryHandler.ExecuteAsync(account, (LauncherAccount current) => microsoftAccountService.UploadSkinAsync(current, ResolveLocalPath(skin.Source), skin.SkinModel, operation.Token));
			if (profile.IsCurrent(microsoftAccountOperationResult.Account, operation))
			{
				LauncherAccount launcherAccount2 = AccountMapper.WithCapeCache(AccountMapper.WithSkinLibrary(AccountMapper.WithAppearanceFallback(microsoftAccountOperationResult.Value, microsoftAccountOperationResult.Account), new global::_003C_003Ez__ReadOnlySingleElementList<LauncherSkinRecord>(skin), skin.Id, skin.Source, skin.SkinModel), microsoftAccountOperationResult.Account.CachedCapeOptions);
				accountList.ReplaceSelectedAccount(microsoftAccountOperationResult.Account, launcherAccount2);
				Populate(launcherAccount2, skin.Id);
				await accountList.PersistAccountOrderAsync();
				profile.SetMessage(Strings.Status_SkinUpdated, showFloating: true);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Account skin apply failed. AccountId={AccountId} AccountKind={AccountKind} SkinId={SkinId}", account.Id, account.Kind, skin.Id);
			profile.SetError(exception, Strings.Status_SkinUpdateFailed, showFloating: true);
		}
		finally
		{
			profile.Complete(account, operation);
		}
	}

	[RelayCommand(CanExecute = "CanEditSelectedSkin")]
	public void ChangeSelectedSkinModel()
	{
		LauncherSkinRecord launcherSkinRecord = SelectedSkin;
		if (launcherSkinRecord != null && CanEditSelectedSkin)
		{
			skinPendingModelChange = launcherSkinRecord;
			dialogService.ShowSkinModelDialog(launcherSkinRecord.SkinModel);
		}
	}

	[RelayCommand(CanExecute = "CanChangeSkinModel")]
	public void ChangeSkinModel(LauncherSkinRecord? skin)
	{
		if (CanChangeSkinModel(skin))
		{
			SelectedSkin = skin;
			ChangeSelectedSkinModel();
		}
	}

	public bool CanChangeSkinModel(LauncherSkinRecord? skin)
	{
		LauncherAccount selectedAccount = accountList.SelectedAccount;
		if (selectedAccount != null && !selectedAccount.IsThirdParty && !profile.IsBusy && skin != null)
		{
			return !IsActiveForSharedAccount(skin);
		}
		return false;
	}

	[RelayCommand(CanExecute = "CanDeleteSelectedSkin")]
	public async Task DeleteSelectedSkinAsync()
	{
		LauncherAccount account = accountList.SelectedAccount;
		LauncherSkinRecord skin = SelectedSkin;
		if (account == null || skin == null || !CanDeleteSelectedSkin)
		{
			return;
		}
		AccountAppearanceOperation operation = profile.BeginOperation(account, string.Empty);
		try
		{
			await skinLibraryService.DeleteSkinAsync(account, skin);
			if (profile.IsCurrent(account, operation))
			{
				string preferredSkinIdAfterDelete = GetPreferredSkinIdAfterDelete(skin);
				Populate(account, preferredSkinIdAfterDelete);
				profile.SetMessage(Strings.Status_SkinDeleted);
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Account skin delete failed. AccountId={AccountId} AccountKind={AccountKind} SkinId={SkinId}", account.Id, account.Kind, skin.Id);
			profile.SetError(exception, Strings.Status_SkinDeleteFailed);
		}
		finally
		{
			profile.Complete(account, operation);
		}
	}

	[RelayCommand(CanExecute = "CanDeleteSkin")]
	public async Task DeleteSkinAsync(LauncherSkinRecord? skin)
	{
		if (CanDeleteSkin(skin))
		{
			SelectedSkin = skin;
			await DeleteSelectedSkinAsync();
		}
	}

	public bool CanDeleteSkin(LauncherSkinRecord? skin)
	{
		LauncherAccount selectedAccount = accountList.SelectedAccount;
		if (selectedAccount != null && !selectedAccount.IsThirdParty && !profile.IsBusy && skin != null)
		{
			return !IsActiveForSharedAccount(skin);
		}
		return false;
	}

	[RelayCommand]
	public void RequestCancelModelDialog()
	{
		skinPendingModelChange = null;
		dialogService.CancelSkinModelDialog();
	}

	[RelayCommand]
	public Task RequestConfirmModelDialogAsync()
	{
		return dialogService.ConfirmSkinModelDialogAsync();
	}

	[RelayCommand(CanExecute = "CanSelectPrevious")]
	public void SelectPrevious()
	{
		LauncherSkinRecord previousSkin = PreviousSkin;
		if (previousSkin != null)
		{
			SelectedSkin = previousSkin;
		}
	}

	public bool CanSelectPrevious()
	{
		return PreviousSkin != null;
	}

	[RelayCommand(CanExecute = "CanSelectNext")]
	public void SelectNext()
	{
		LauncherSkinRecord nextSkin = NextSkin;
		if (nextSkin != null)
		{
			SelectedSkin = nextSkin;
		}
	}

	public bool CanSelectNext()
	{
		return NextSkin != null;
	}

	private async Task AddSkinAsync(string path, MinecraftSkinModel model)
	{
		LauncherAccount account = accountList.SelectedAccount;
		if (account == null || account.IsThirdParty)
		{
			return;
		}
		AccountAppearanceOperation operation = profile.BeginOperation(account, Strings.Status_AddingSkin);
		try
		{
			LauncherSkinRecord launcherSkinRecord = await skinLibraryService.ImportSkinAsync(account, path, model);
			if (profile.IsCurrent(account, operation))
			{
				Populate(account, launcherSkinRecord.Id);
				profile.SetMessage(Strings.Status_SkinAdded);
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Account skin import failed. AccountId={AccountId} AccountKind={AccountKind}", account.Id, account.Kind);
			profile.SetError(exception, Strings.Status_SkinUpdateFailed);
		}
		finally
		{
			profile.Complete(account, operation);
		}
	}

	private async Task ChangeSkinModelAsync(LauncherSkinRecord skin, MinecraftSkinModel model)
	{
		LauncherAccount account = accountList.SelectedAccount;
		if (account != null && skin.SkinModel != model && !IsActiveForSharedAccount(skin))
		{
			LauncherSkinRecord updatedSkin = await skinLibraryService.ImportSkinAsync(account, ResolveLocalPath(skin.Source), model);
			await skinLibraryService.DeleteSkinAsync(account, skin);
			Populate(account, updatedSkin.Id);
			profile.SetMessage(Strings.Status_SkinModelChanged);
		}
	}

	private void Populate(LauncherAccount account, string? preferredId)
	{
		if (account.IsThirdParty)
		{
			Skins.Clear();
			LauncherSkinRecord launcherSkinRecord = account.SkinLibrary.FirstOrDefault((LauncherSkinRecord skin) => string.Equals(skin.Id, account.ActiveSkinId, StringComparison.Ordinal)) ?? account.SkinLibrary.FirstOrDefault((LauncherSkinRecord skin) => string.Equals(skin.Source, account.SkinSource, StringComparison.Ordinal));
			if (launcherSkinRecord != null)
			{
				Skins.Add(launcherSkinRecord);
			}
			SelectedSkin = launcherSkinRecord;
			NotifyState();
			return;
		}
		List<LauncherSkinRecord> list = DistinctSkins(skinLibraryService.GetSharedSkins()).ToList();
		foreach (LauncherSkinRecord activeAccountSkin in from skin in (from item in accountList.Accounts
				select item.Account into candidate
				where !candidate.IsThirdParty
				select candidate).Select(FindActiveSkinReference)
			where skin != null
			select (skin))
		{
			if (list.All((LauncherSkinRecord skin) => !SameContent(skin, activeAccountSkin)))
			{
				list.Add(activeAccountSkin);
			}
		}
		if (!SkinSequencesEqual(Skins, list))
		{
			Skins.Clear();
			foreach (LauncherSkinRecord item in list)
			{
				Skins.Add(item);
			}
		}
		SelectedSkin = Skins.FirstOrDefault((LauncherSkinRecord skin) => string.Equals(skin.Id, preferredId, StringComparison.Ordinal)) ?? Skins.FirstOrDefault((LauncherSkinRecord skin) => string.Equals(skin.Id, account.ActiveSkinId, StringComparison.Ordinal)) ?? Skins.FirstOrDefault();
		NotifyState();
	}

	private LauncherSkinRecord? GetAdjacent(int offset)
	{
		if (IsThirdParty || SelectedSkin == null || Skins.Count < 2)
		{
			return null;
		}
		int num = Skins.IndexOf(SelectedSkin) + offset;
		if (num < 0 || num >= Skins.Count)
		{
			return null;
		}
		return Skins[num];
	}

	private void NotifyState()
	{
		OnPropertyChanged("PreviousSkin");
		OnPropertyChanged("NextSkin");
		OnPropertyChanged("HasSkins");
		OnPropertyChanged("IsOffline");
		OnPropertyChanged("IsThirdParty");
		OnPropertyChanged("CanShowStandardActions");
		OnPropertyChanged("CanShowThirdPartyRefresh");
		OnPropertyChanged("HasPreview");
		OnPropertyChanged("CanShowPreviewEmptyState");
		OnPropertyChanged("CanChangeSkin");
		OnPropertyChanged("CanManageSkins");
		OnPropertyChanged("CanApplySkin");
		OnPropertyChanged("CanEditSelectedSkin");
		OnPropertyChanged("CanDeleteSelectedSkin");
		OnPropertyChanged("CanShowManagerEmptyState");
		OnPropertyChanged("ActiveSkinId");
		NotifyCommandState();
	}

	private void NotifyCommandState()
	{
		RequestOpenManagerDialogCommand.NotifyCanExecuteChanged();
		ApplySkinCommand.NotifyCanExecuteChanged();
		ChangeSelectedSkinModelCommand.NotifyCanExecuteChanged();
		DeleteSelectedSkinCommand.NotifyCanExecuteChanged();
		ChangeSkinModelCommand.NotifyCanExecuteChanged();
		DeleteSkinCommand.NotifyCanExecuteChanged();
		SelectPreviousCommand.NotifyCanExecuteChanged();
		SelectNextCommand.NotifyCanExecuteChanged();
	}

	private string? GetPreferredSkinIdAfterDelete(LauncherSkinRecord deleted)
	{
		int num = Skins.ToList().FindIndex((LauncherSkinRecord skin) => string.Equals(skin.Id, deleted.Id, StringComparison.Ordinal));
		if (num + 1 < Skins.Count)
		{
			return Skins[num + 1].Id;
		}
		if (num <= 0)
		{
			return null;
		}
		return Skins[num - 1].Id;
	}

	private bool IsActiveForSharedAccount(LauncherSkinRecord skin)
	{
		return accountList.Accounts.Any((AccountItemViewModel item) => !item.Account.IsThirdParty && IsAlreadyApplied(item.Account, skin));
	}

	/// <summary>
	/// 离线账户是否需要补头像。判断依据只剩"有没有头像来源"——
	/// 原来还会比对 minotar 的默认 Steve 图，但那是 Minecraft 皮肤服务，
	/// StartRide 的账户已经只剩 Steam，不再依赖任何第三方皮肤站。
	/// </summary>
	private static bool NeedsOfflineAvatarRefresh(LauncherAccount account)
	{
		return account.IsOffline && string.IsNullOrWhiteSpace(account.AvatarSource);
	}

	private static string ResolveLocalPath(string source)
	{
		if (!Uri.TryCreate(source, UriKind.Absolute, out Uri result) || !result.IsFile)
		{
			return source;
		}
		return result.LocalPath;
	}

	private static bool IsAlreadyApplied(LauncherAccount account, LauncherSkinRecord skin)
	{
		if (string.Equals(account.ActiveSkinId, skin.Id, StringComparison.Ordinal))
		{
			return account.SkinModel == skin.SkinModel;
		}
		LauncherSkinRecord launcherSkinRecord = account.SkinLibrary.FirstOrDefault((LauncherSkinRecord value) => string.Equals(value.Id, account.ActiveSkinId, StringComparison.Ordinal));
		if (launcherSkinRecord != null && SameContent(launcherSkinRecord, skin))
		{
			return true;
		}
		if (account.SkinModel == skin.SkinModel)
		{
			return string.Equals(account.SkinSource, skin.Source, StringComparison.Ordinal);
		}
		return false;
	}

	private static LauncherSkinRecord? FindActiveSkinReference(LauncherAccount account)
	{
		return account.SkinLibrary.FirstOrDefault((LauncherSkinRecord skin) => string.Equals(skin.Id, account.ActiveSkinId, StringComparison.Ordinal)) ?? account.SkinLibrary.FirstOrDefault((LauncherSkinRecord skin) => account.SkinModel == skin.SkinModel && string.Equals(skin.Source, account.SkinSource, StringComparison.Ordinal));
	}

	private static IEnumerable<LauncherSkinRecord> DistinctSkins(IEnumerable<LauncherSkinRecord> skins)
	{
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (LauncherSkinRecord skin in skins)
		{
			string item = (string.IsNullOrWhiteSpace(skin.ContentHash) ? $"{skin.Source}|{skin.SkinModel}" : $"{skin.ContentHash}|{skin.SkinModel}");
			if (seen.Add(item))
			{
				yield return skin;
			}
		}
	}

	private static bool SameContent(LauncherSkinRecord left, LauncherSkinRecord right)
	{
		if (left.SkinModel == right.SkinModel)
		{
			if (string.IsNullOrWhiteSpace(left.ContentHash) || string.IsNullOrWhiteSpace(right.ContentHash))
			{
				return string.Equals(left.Source, right.Source, StringComparison.Ordinal);
			}
			return string.Equals(left.ContentHash, right.ContentHash, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static bool SkinSequencesEqual(IReadOnlyList<LauncherSkinRecord> left, IReadOnlyList<LauncherSkinRecord> right)
	{
		if (left.Count != right.Count)
		{
			return false;
		}
		for (int i = 0; i < left.Count; i++)
		{
			if (!string.Equals(left[i].Id, right[i].Id, StringComparison.Ordinal) || !string.Equals(left[i].Source, right[i].Source, StringComparison.Ordinal) || left[i].SkinModel != right[i].SkinModel || !string.Equals(left[i].ContentHash, right[i].ContentHash, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}
		return true;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedSkinChanged(LauncherSkinRecord? value)
	{
		NotifyState();
	}
}
