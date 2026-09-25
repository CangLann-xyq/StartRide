using System;
using System.Threading.Tasks;
using StartRide.App.Services;
using Launcher.Application.Accounts;

namespace StartRide.App.ViewModels.Account;

internal sealed class MicrosoftAccountOperationRetryHandler
{
	private readonly AccountListViewModel accountList;

	private readonly IMicrosoftAccountReauthenticationDialogService dialogService;

	public MicrosoftAccountOperationRetryHandler(AccountListViewModel accountList, IMicrosoftAccountReauthenticationDialogService dialogService)
	{
		this.accountList = accountList;
		this.dialogService = dialogService;
	}

	public async Task<MicrosoftAccountOperationResult<T>> ExecuteAsync<T>(LauncherAccount account, Func<LauncherAccount, Task<T>> operation)
	{
		try
		{
			LauncherAccount account2 = account;
			return new MicrosoftAccountOperationResult<T>(account2, await operation(account));
		}
		catch (MicrosoftAccountSessionExpiredException innerException)
		{
			if (!(await dialogService.ShowMicrosoftReauthenticationDialogAsync(account)))
			{
				throw new OperationCanceledException("Microsoft account reauthentication was canceled.", innerException);
			}
			LauncherAccount launcherAccount = accountList.FindAccount(account.Id);
			if (launcherAccount == null || !launcherAccount.IsMicrosoft)
			{
				throw new MicrosoftAccountSessionExpiredException("The reauthenticated Microsoft account is no longer available.", innerException);
			}
			LauncherAccount account2 = launcherAccount;
			return new MicrosoftAccountOperationResult<T>(account2, await operation(launcherAccount));
		}
	}

	public async Task<LauncherAccount> ExecuteAsync(LauncherAccount account, Func<LauncherAccount, Task> operation)
	{
		return (await ExecuteAsync(account, async delegate(LauncherAccount current)
		{
			await operation(current);
			return true;
		})).Account;
	}
}
