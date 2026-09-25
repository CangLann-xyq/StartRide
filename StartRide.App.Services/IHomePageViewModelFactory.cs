using System;
using System.Threading.Tasks;
using StartRide.App.ViewModels.Account;
using StartRide.App.ViewModels.Home;
using Launcher.Domain.Models;

namespace StartRide.App.Services;

public interface IHomePageViewModelFactory
{
	HomePageViewModel Create(AccountPageViewModel accountPage, Action<double> reportProgressPercent, Func<GameInstance, Task<bool>> selectLaunchInstance, Func<bool, Task<bool>> setLaunchMenuPinned, Func<GameInstance?, Task> openGameSettingsForInstance);
}
