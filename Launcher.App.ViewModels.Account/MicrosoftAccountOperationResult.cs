using Launcher.Application.Accounts;

namespace Launcher.App.ViewModels.Account;

internal readonly record struct MicrosoftAccountOperationResult<T>(LauncherAccount Account, T Value);
