using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.Application.Accounts;

namespace Launcher.App.ViewModels.Account;

internal sealed class AccountAppearanceOperationCoordinator : ObservableObject, IDisposable
{
	private CancellationTokenSource accountLifetime = new CancellationTokenSource();

	private CancellationTokenSource? currentOperation;

	private string? accountId;

	private long generation;

	[ObservableProperty]
	private bool isBusy;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsBusy
	{
		get
		{
			return isBusy;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isBusy, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsBusy);
				isBusy = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsBusy);
			}
		}
	}

	public void SetAccount(LauncherAccount? account)
	{
		string b = account?.Id;
		if (!string.Equals(accountId, b, StringComparison.Ordinal))
		{
			accountId = b;
			Interlocked.Increment(ref generation);
			CancellationTokenSource cancellationTokenSource = Interlocked.Exchange(ref accountLifetime, new CancellationTokenSource());
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
			CancelCurrentOperation();
			IsBusy = false;
		}
	}

	public AccountAppearanceOperation Begin(LauncherAccount account)
	{
		if (!string.Equals(account.Id, accountId, StringComparison.Ordinal))
		{
			SetAccount(account);
		}
		CancelCurrentOperation();
		CancellationTokenSource cancellationTokenSource = (currentOperation = CancellationTokenSource.CreateLinkedTokenSource(accountLifetime.Token));
		IsBusy = true;
		return new AccountAppearanceOperation(account.Id, generation, cancellationTokenSource, cancellationTokenSource.Token);
	}

	public bool IsCurrent(LauncherAccount account, AccountAppearanceOperation operation)
	{
		if (!operation.Token.IsCancellationRequested && operation.Generation == generation && string.Equals(account.Id, accountId, StringComparison.Ordinal))
		{
			return operation.Source == currentOperation;
		}
		return false;
	}

	public void Complete(LauncherAccount account, AccountAppearanceOperation operation)
	{
		if (IsCurrent(account, operation))
		{
			currentOperation = null;
			operation.Source.Dispose();
			IsBusy = false;
		}
	}

	public void Dispose()
	{
		CancelCurrentOperation();
		accountLifetime.Cancel();
		accountLifetime.Dispose();
	}

	private void CancelCurrentOperation()
	{
		CancellationTokenSource? cancellationTokenSource = Interlocked.Exchange(ref currentOperation, null);
		cancellationTokenSource?.Cancel();
		cancellationTokenSource?.Dispose();
	}
}
