using System.Threading;

namespace StartRide.App.ViewModels.Account;

internal readonly record struct AccountAppearanceOperation(string AccountId, long Generation, CancellationTokenSource Source, CancellationToken Token);
