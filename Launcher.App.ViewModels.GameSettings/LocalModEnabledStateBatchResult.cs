namespace Launcher.App.ViewModels.GameSettings;

public sealed record LocalModEnabledStateBatchResult(int FailedCount, string? FirstConflictTargetPath);
