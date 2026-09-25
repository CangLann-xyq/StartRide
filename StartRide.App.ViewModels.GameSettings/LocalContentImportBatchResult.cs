namespace StartRide.App.ViewModels.GameSettings;

internal sealed record LocalContentImportBatchResult<TResult>(int SuccessCount, string? FailedPath, TResult? Failure) where TResult : class;
