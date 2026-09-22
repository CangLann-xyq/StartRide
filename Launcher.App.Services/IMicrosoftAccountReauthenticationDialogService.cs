using System.Threading.Tasks;
using Launcher.Application.Accounts;

namespace Launcher.App.Services;

public interface IMicrosoftAccountReauthenticationDialogService
{
	Task<bool> ShowMicrosoftReauthenticationDialogAsync(LauncherAccount account);
}
