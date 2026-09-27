using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.GameSettings;

public abstract class GameSettingsDetailsSectionViewModelBase : ObservableObject
{
	private readonly GameSettingsDetailsViewModel? parent;

	public GameSettingsDetailsViewModel Parent => parent ?? throw new InvalidOperationException("This settings section does not use a parent view model.");

	public virtual bool UsesFullViewportLayout => false;

	protected GameSettingsDetailsSectionViewModelBase()
	{
	}

	protected GameSettingsDetailsSectionViewModelBase(GameSettingsDetailsViewModel parent)
	{
		this.parent = parent;
	}

	public virtual void OnSelectedInstanceChanged(GameInstance? instance)
	{
	}

	public virtual void OnSectionDeactivated()
	{
	}

	public virtual Task OnSectionActivatedAsync()
	{
		return Task.CompletedTask;
	}
}
