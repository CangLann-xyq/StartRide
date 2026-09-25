using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Application.Accounts;
using Microsoft.Extensions.Logging;

namespace StartRide.App.ViewModels.Account;

public sealed class AccountProfileViewModel : ObservableObject
{
	private readonly AccountListViewModel accountList;

	private readonly IMicrosoftAccountService microsoftAccountService;

	private readonly IThirdPartyAccountService thirdPartyAccountService;

	private readonly AccountAppearanceOperationCoordinator operations;

	private readonly MicrosoftAccountOperationRetryHandler microsoftOperationRetryHandler;

	private readonly IFloatingMessageService? floatingMessageService;

	private readonly ILogger logger;

	[ObservableProperty]
	private string message = string.Empty;

	[ObservableProperty]
	private string errorCodeMessage = string.Empty;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? refreshInfoCommand;

	public bool IsBusy => operations.IsBusy;

	public bool HasErrorCode => !string.IsNullOrWhiteSpace(ErrorCodeMessage);

	public bool CanRefresh
	{
		get
		{
			LauncherAccount selectedAccount = accountList.SelectedAccount;
			if (selectedAccount != null && !selectedAccount.IsOffline)
			{
				return !IsBusy;
			}
			return false;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Message
	{
		get
		{
			return message;
		}
		[MemberNotNull("message")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(message, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Message);
				message = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Message);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ErrorCodeMessage
	{
		get
		{
			return errorCodeMessage;
		}
		[MemberNotNull("errorCodeMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(errorCodeMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ErrorCodeMessage);
				errorCodeMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ErrorCodeMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RefreshInfoCommand => refreshInfoCommand ?? (refreshInfoCommand = new AsyncRelayCommand(RefreshInfoAsync, () => CanRefresh));

	internal AccountProfileViewModel(AccountListViewModel accountList, IMicrosoftAccountService microsoftAccountService, IThirdPartyAccountService thirdPartyAccountService, AccountAppearanceOperationCoordinator operations, MicrosoftAccountOperationRetryHandler microsoftOperationRetryHandler, IFloatingMessageService? floatingMessageService, ILogger logger)
	{
		this.accountList = accountList;
		this.microsoftAccountService = microsoftAccountService;
		this.thirdPartyAccountService = thirdPartyAccountService;
		this.operations = operations;
		this.microsoftOperationRetryHandler = microsoftOperationRetryHandler;
		this.floatingMessageService = floatingMessageService;
		this.logger = logger;
		operations.PropertyChanged += delegate(object? _, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "IsBusy")
			{
				OnPropertyChanged("IsBusy");
			}
		};
	}

	public void SetAccount(LauncherAccount? account)
	{
		operations.SetAccount(account);
		ErrorCodeMessage = string.Empty;
		Message = ((account == null) ? string.Empty : (account.IsOffline ? Strings.Account_ProfileOfflineUnsupported : ((account.CachedCapeOptions.Count > 0) ? Strings.Account_ProfileCacheLoaded : Strings.Account_ProfileRefreshHint)));
		RefreshInfoCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand(CanExecute = "CanRefresh")]
	public async Task RefreshInfoAsync()
	{
		LauncherAccount account = accountList.SelectedAccount;
		if (account == null)
		{
			return;
		}
		if (account.IsOffline)
		{
			Message = Strings.Status_AccountProfileRefreshOfflineUnsupported;
			return;
		}
		AccountAppearanceOperation operation = operations.Begin(account);
		SetMessage(Strings.Status_RefreshingAccountProfile);
		try
		{
			MicrosoftAccountOperationResult<LauncherAccount> microsoftAccountOperationResult;
			if (account.IsMicrosoft)
			{
				microsoftAccountOperationResult = await microsoftOperationRetryHandler.ExecuteAsync(account, (LauncherAccount current) => microsoftAccountService.RefreshAccountProfileAsync(current, operation.Token));
			}
			else
			{
				LauncherAccount account2 = account;
				microsoftAccountOperationResult = new MicrosoftAccountOperationResult<LauncherAccount>(account2, await thirdPartyAccountService.RefreshAccountProfileAsync(account, operation.Token));
			}
			MicrosoftAccountOperationResult<LauncherAccount> microsoftAccountOperationResult2 = microsoftAccountOperationResult;
			if (operations.IsCurrent(microsoftAccountOperationResult2.Account, operation))
			{
				LauncherAccount newAccount = (account.IsMicrosoft ? AccountMapper.WithCapeCache(microsoftAccountOperationResult2.Value, microsoftAccountOperationResult2.Account.CachedCapeOptions) : microsoftAccountOperationResult2.Value);
				accountList.ReplaceSelectedAccount(microsoftAccountOperationResult2.Account, newAccount);
				await accountList.PersistAccountOrderAsync();
				SetMessage(Strings.Status_AccountProfileRefreshed);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Account profile refresh failed. AccountId={AccountId} AccountKind={AccountKind}", account.Id, account.Kind);
			SetError(exception, GetRefreshFailureMessage(exception, Strings.Status_AccountProfileRefreshFailed), showFloating: true);
		}
		finally
		{
			operations.Complete(account, operation);
			RefreshInfoCommand.NotifyCanExecuteChanged();
		}
	}

	public async Task RefreshAccountsSilentlyAsync()
	{
		List<LauncherAccount> list = (from item in accountList.Accounts
			where item.Account.IsMicrosoft || item.Account.IsThirdParty
			select item.Account).ToList();
		bool changed = false;
		foreach (LauncherAccount account in list)
		{
			LauncherAccount launcherAccount2;
			try
			{
				LauncherAccount launcherAccount = ((!account.IsMicrosoft) ? (await thirdPartyAccountService.RefreshAccountProfileAsync(account)) : (await microsoftAccountService.RefreshAccountProfileAsync(account)));
				launcherAccount2 = launcherAccount;
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception exception)
			{
				logger.LogDebug(exception, "Silent account refresh failed. AccountId={AccountId} AccountKind={AccountKind}", account.Id, account.Kind);
				continue;
			}
			LauncherAccount launcherAccount3 = accountList.FindAccount(account.Id);
			if (launcherAccount3 != null && (!IsBusy || !string.Equals(launcherAccount3.Id, accountList.SelectedAccount?.Id, StringComparison.Ordinal)))
			{
				LauncherAccount newAccount = (account.IsMicrosoft ? AccountMapper.WithCapeCache(launcherAccount2, launcherAccount3.CachedCapeOptions) : launcherAccount2);
				changed |= accountList.TryReplaceAccount(launcherAccount3.Id, newAccount);
			}
		}
		if (!changed)
		{
			return;
		}
		try
		{
			await accountList.PersistAccountOrderAsync();
		}
		catch (Exception exception2)
		{
			logger.LogWarning(exception2, "Persisting silently refreshed accounts failed.");
		}
	}

	public void SetMessage(string message, bool showFloating = false)
	{
		Message = message;
		ErrorCodeMessage = string.Empty;
		if (showFloating)
		{
			floatingMessageService?.Show(message);
		}
	}

	public void SetError(Exception exception, string message, bool showFloating = false)
	{
		Message = message;
		ErrorCodeMessage = AccountErrorCodeMessageFormatter.Format(exception);
		if (showFloating)
		{
			floatingMessageService?.Show(message);
		}
	}

	internal AccountAppearanceOperation BeginOperation(LauncherAccount account, string message)
	{
		AccountAppearanceOperation result = operations.Begin(account);
		SetMessage(message);
		return result;
	}

	internal bool IsCurrent(LauncherAccount account, AccountAppearanceOperation operation)
	{
		return operations.IsCurrent(account, operation);
	}

	internal void Complete(LauncherAccount account, AccountAppearanceOperation operation)
	{
		operations.Complete(account, operation);
	}

	internal static string GetRefreshFailureMessage(Exception exception, string fallback)
	{
		string text;
		if (exception is MicrosoftAccountProfileRefreshException ex)
		{
			string errorCode = ex.ErrorCode;
			if (errorCode != null && errorCode.Length > 0)
			{
				text = errorCode;
				goto IL_0026;
			}
		}
		text = exception.Message;
		goto IL_0026;
		IL_0026:
		string text2 = text;
		if (string.IsNullOrWhiteSpace(text2) || !text2.Contains("429", StringComparison.OrdinalIgnoreCase) || (!text2.Contains("too many request", StringComparison.OrdinalIgnoreCase) && !text2.Contains("too_many_request", StringComparison.OrdinalIgnoreCase)))
		{
			return fallback;
		}
		return Strings.Status_AccountProfileRefreshTooFrequent;
	}
}
