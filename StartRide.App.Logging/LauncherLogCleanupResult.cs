namespace StartRide.App.Logging;

internal readonly record struct LauncherLogCleanupResult(int DeletedFileCount, int RetainedFileCount)
{
	public static LauncherLogCleanupResult Empty => new LauncherLogCleanupResult(0, 0);
}
