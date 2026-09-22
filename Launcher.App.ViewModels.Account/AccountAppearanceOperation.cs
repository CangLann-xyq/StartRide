using System.Threading;

namespace Launcher.App.ViewModels.Account;

internal readonly record struct AccountAppearanceOperation(string AccountId, long Generation, CancellationTokenSource Source, CancellationToken Token);
