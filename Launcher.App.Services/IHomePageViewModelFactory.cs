using System;
using System.Threading.Tasks;
using Launcher.App.ViewModels.Account;
using Launcher.App.ViewModels.Home;
using Launcher.Domain.Models;

namespace Launcher.App.Services;

public interface IHomePageViewModelFactory
{
	HomePageViewModel Create(AccountPageViewModel accountPage, Action<double> reportProgressPercent, Func<GameInstance, Task<bool>> selectLaunchInstance, Func<bool, Task<bool>> setLaunchMenuPinned, Func<GameInstance?, Task> openGameSettingsForInstance);
}
