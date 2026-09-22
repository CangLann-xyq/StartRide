using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Settings;

public abstract class SettingsSectionViewModelBase : ObservableObject
{
	private readonly SettingsPersistenceCoordinator persistence;

	private bool isLoading;

	private protected LauncherSettings Settings => persistence.Settings;

	private protected bool CanPersist
	{
		get
		{
			if (persistence.IsPrimed)
			{
				return !isLoading;
			}
			return false;
		}
	}

	private protected SettingsSectionViewModelBase(SettingsPersistenceCoordinator persistence)
	{
		this.persistence = persistence;
	}

	private protected void LoadState(Action load)
	{
		isLoading = true;
		try
		{
			load();
		}
		finally
		{
			isLoading = false;
		}
	}

	private protected void Persist(Action<LauncherSettings> update)
	{
		if (CanPersist)
		{
			persistence.Update(update);
		}
	}

	private protected Task PersistImmediatelyAsync(Action<LauncherSettings> update, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!CanPersist)
		{
			return Task.CompletedTask;
		}
		return persistence.SaveImmediatelyAsync(update, cancellationToken);
	}
}
