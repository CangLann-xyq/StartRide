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
using StartRide.App.Utilities;
using Launcher.Application.Accounts;
using Microsoft.Extensions.Logging;

namespace StartRide.App.ViewModels.Account;

public sealed class AccountCapeViewModel : ObservableObject
{
	private readonly AccountListViewModel accountList;

	private readonly IMicrosoftAccountService microsoftAccountService;

	private readonly AccountProfileViewModel profile;

	private readonly MicrosoftAccountOperationRetryHandler microsoftOperationRetryHandler;

	private readonly ILogger logger;

	[ObservableProperty]
	private AccountCapeOption? selectedOption;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? refreshCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? applyCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? selectPreviousCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? selectNextCommand;

	public ObservableCollection<AccountCapeOption> Options { get; } = new ObservableCollection<AccountCapeOption>();

	public AccountCapeOption? PreviousOption => GetAdjacent(-1);

	public AccountCapeOption? NextOption => GetAdjacent(1);

	public bool HasOptions => Options.Count > 0;

	public bool IsOffline => accountList.SelectedAccount?.IsOffline ?? false;

	public bool IsThirdParty => accountList.SelectedAccount?.IsThirdParty ?? false;

	public bool CanShowApplyButton => !IsThirdParty;

	public bool HasPreview
	{
		get
		{
			if (!IsOffline)
			{
				return SelectedOption != null;
			}
			return false;
		}
	}

	public bool CanShowPreviewEmptyState
	{
		get
		{
			if (accountList.SelectedAccount != null && !IsOffline)
			{
				return !HasPreview;
			}
			return false;
		}
	}

	public bool CanApply
	{
		get
		{
			LauncherAccount selectedAccount = accountList.SelectedAccount;
			if (selectedAccount != null && selectedAccount.IsMicrosoft && !profile.IsBusy)
			{
				AccountCapeOption accountCapeOption = SelectedOption;
				if (accountCapeOption != null)
				{
					return !accountCapeOption.IsActive;
				}
				return false;
			}
			return false;
		}
	}

	public bool CanRefresh
	{
		get
		{
			LauncherAccount selectedAccount = accountList.SelectedAccount;
			if (selectedAccount != null && !selectedAccount.IsOffline)
			{
				return !profile.IsBusy;
			}
			return false;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public AccountCapeOption? SelectedOption
	{
		get
		{
			return selectedOption;
		}
		set
		{
			if (!EqualityComparer<AccountCapeOption>.Default.Equals(selectedOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedOption);
				selectedOption = value;
				OnSelectedOptionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RefreshCommand => refreshCommand ?? (refreshCommand = new AsyncRelayCommand(RefreshAsync, () => CanRefresh));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ApplyCommand => applyCommand ?? (applyCommand = new AsyncRelayCommand(ApplyAsync, () => CanApply));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand SelectPreviousCommand => selectPreviousCommand ?? (selectPreviousCommand = new RelayCommand(SelectPrevious, CanSelectPrevious));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand SelectNextCommand => selectNextCommand ?? (selectNextCommand = new RelayCommand(SelectNext, CanSelectNext));

	internal AccountCapeViewModel(AccountListViewModel accountList, IMicrosoftAccountService microsoftAccountService, AccountProfileViewModel profile, MicrosoftAccountOperationRetryHandler microsoftOperationRetryHandler, ILogger logger)
	{
		this.accountList = accountList;
		this.microsoftAccountService = microsoftAccountService;
		this.profile = profile;
		this.microsoftOperationRetryHandler = microsoftOperationRetryHandler;
		this.logger = logger;
		profile.PropertyChanged += delegate
		{
			NotifyState();
		};
	}

	public void SetAccount(LauncherAccount? account)
	{
		if (account == null || account.IsOffline)
		{
			Options.Clear();
			SelectedOption = null;
		}
		else if (account.IsThirdParty)
		{
			PopulateThirdParty(account.CachedCapeOptions);
		}
		else
		{
			Populate(account.CachedCapeOptions);
		}
		NotifyState();
	}

	[RelayCommand(CanExecute = "CanRefresh")]
	public async Task RefreshAsync()
	{
		LauncherAccount account = accountList.SelectedAccount;
		if (account == null)
		{
			return;
		}
		if (account.IsOffline)
		{
			profile.SetMessage(Strings.Account_ProfileOfflineUnsupported);
			return;
		}
		if (!account.IsThirdParty)
		{
			AccountAppearanceOperation operation = profile.BeginOperation(account, Strings.Status_LoadingAccountProfile);
			try
			{
				MicrosoftAccountOperationResult<IReadOnlyList<AccountCapeOption>> microsoftAccountOperationResult = await microsoftOperationRetryHandler.ExecuteAsync(account, (LauncherAccount current) => microsoftAccountService.GetCapesAsync(current, operation.Token));
				if (profile.IsCurrent(microsoftAccountOperationResult.Account, operation))
				{
					bool hasCapes = microsoftAccountOperationResult.Value.Any((AccountCapeOption cape) => !cape.IsNone);
					Populate(microsoftAccountOperationResult.Value);
					await StoreCacheAsync(microsoftAccountOperationResult.Account);
					profile.SetMessage(hasCapes ? Strings.Account_ProfileLoaded : Strings.Account_ProfileNoCapes);
				}
				return;
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception exception)
			{
				logger.LogWarning(exception, "Microsoft account cape load failed. AccountId={AccountId}", account.Id);
				profile.SetError(exception, AccountProfileViewModel.GetRefreshFailureMessage(exception, Strings.Status_LoadAccountProfileFailed), showFloating: true);
				return;
			}
			finally
			{
				profile.Complete(account, operation);
			}
		}
		await profile.RefreshInfoAsync();
	}

	[RelayCommand(CanExecute = "CanApply")]
	public async Task ApplyAsync()
	{
		LauncherAccount account = accountList.SelectedAccount;
		AccountCapeOption cape = SelectedOption;
		if (account == null || cape == null || !CanApply)
		{
			return;
		}
		AccountAppearanceOperation operation = profile.BeginOperation(account, Strings.Status_ChangingCape);
		try
		{
			LauncherAccount launcherAccount = await microsoftOperationRetryHandler.ExecuteAsync(account, (LauncherAccount current) => microsoftAccountService.SetActiveCapeAsync(current, cape.Id, operation.Token));
			if (profile.IsCurrent(launcherAccount, operation))
			{
				string message = (cape.IsNone ? Strings.Status_CapeRemoved : string.Format(Strings.Status_CapeChangedFormat, AccountCapeTextProvider.GetDisplayName(cape)));
				MarkActive(cape);
				await StoreCacheAsync(launcherAccount);
				profile.SetMessage(message, showFloating: true);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Microsoft account cape change failed. AccountId={AccountId}", account.Id);
			profile.SetMessage(Strings.Status_CapeChangeFailed, showFloating: true);
		}
		finally
		{
			profile.Complete(account, operation);
		}
	}

	[RelayCommand(CanExecute = "CanSelectPrevious")]
	public void SelectPrevious()
	{
		AccountCapeOption previousOption = PreviousOption;
		if (previousOption != null)
		{
			SelectedOption = previousOption;
		}
	}

	public bool CanSelectPrevious()
	{
		return PreviousOption != null;
	}

	[RelayCommand(CanExecute = "CanSelectNext")]
	public void SelectNext()
	{
		AccountCapeOption nextOption = NextOption;
		if (nextOption != null)
		{
			SelectedOption = nextOption;
		}
	}

	public bool CanSelectNext()
	{
		return NextOption != null;
	}

	private void Populate(IEnumerable<AccountCapeOption> capes)
	{
		List<AccountCapeOption> list = Normalize(capes).ToList();
		Options.Clear();
		foreach (AccountCapeOption item in list)
		{
			Options.Add(item);
		}
		SelectedOption = Options.FirstOrDefault((AccountCapeOption cape) => cape.IsActive) ?? Options.FirstOrDefault();
		NotifyState();
	}

	private void PopulateThirdParty(IEnumerable<AccountCapeOption> capes)
	{
		AccountCapeOption item = capes.FirstOrDefault((AccountCapeOption cape) => !cape.IsNone && cape.IsActive) ?? capes.FirstOrDefault((AccountCapeOption cape) => !cape.IsNone) ?? new AccountCapeOption
		{
			DisplayName = string.Empty,
			IsActive = true,
			IsNone = true
		};
		Options.Clear();
		Options.Add(item);
		SelectedOption = item;
		NotifyState();
	}

	private void MarkActive(AccountCapeOption active)
	{
		Populate(Options.Select((AccountCapeOption cape) => new AccountCapeOption
		{
			Id = cape.Id,
			DisplayName = cape.DisplayName,
			ImageUrl = cape.ImageUrl,
			IsNone = cape.IsNone,
			IsActive = (cape.IsNone == active.IsNone && string.Equals(cape.Id, active.Id, StringComparison.OrdinalIgnoreCase))
		}));
	}

	private async Task StoreCacheAsync(LauncherAccount expectedAccount)
	{
		LauncherAccount selectedAccount = accountList.SelectedAccount;
		if (selectedAccount != null && string.Equals(selectedAccount.Id, expectedAccount.Id, StringComparison.Ordinal))
		{
			accountList.ReplaceSelectedAccount(selectedAccount, AccountMapper.WithCapeCache(selectedAccount, Options.ToList()));
			await accountList.PersistAccountOrderAsync();
		}
	}

	private AccountCapeOption? GetAdjacent(int offset)
	{
		if (SelectedOption == null || Options.Count < 2)
		{
			return null;
		}
		int num = Options.IndexOf(SelectedOption) + offset;
		if (num < 0 || num >= Options.Count)
		{
			return null;
		}
		return Options[num];
	}

	private void NotifyState()
	{
		OnPropertyChanged("PreviousOption");
		OnPropertyChanged("NextOption");
		OnPropertyChanged("HasOptions");
		OnPropertyChanged("IsOffline");
		OnPropertyChanged("IsThirdParty");
		OnPropertyChanged("CanShowApplyButton");
		OnPropertyChanged("HasPreview");
		OnPropertyChanged("CanShowPreviewEmptyState");
		OnPropertyChanged("CanApply");
		OnPropertyChanged("CanRefresh");
		ApplyCommand.NotifyCanExecuteChanged();
		RefreshCommand.NotifyCanExecuteChanged();
		SelectPreviousCommand.NotifyCanExecuteChanged();
		SelectNextCommand.NotifyCanExecuteChanged();
	}

	private static IEnumerable<AccountCapeOption> Normalize(IEnumerable<AccountCapeOption> capes)
	{
		List<AccountCapeOption> source = capes.ToList();
		bool flag = source.Any((AccountCapeOption cape) => !cape.IsNone && cape.IsActive);
		AccountCapeOption accountCapeOption = source.FirstOrDefault((AccountCapeOption cape) => cape.IsNone);
		yield return (accountCapeOption == null) ? new AccountCapeOption
		{
			DisplayName = string.Empty,
			IsNone = true,
			IsActive = !flag
		} : new AccountCapeOption
		{
			Id = null,
			DisplayName = string.Empty,
			ImageUrl = accountCapeOption.ImageUrl,
			IsActive = (!flag && accountCapeOption.IsActive),
			IsNone = true
		};
		foreach (AccountCapeOption item in source.Where((AccountCapeOption cape) => !cape.IsNone))
		{
			yield return item;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedOptionChanged(AccountCapeOption? value)
	{
		NotifyState();
	}
}
