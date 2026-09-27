using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.GameSettings;

internal static class GameInstanceStateCopier
{
	public static void Copy(GameInstance source, GameInstance destination)
	{
		destination.Id = source.Id;
		destination.Name = source.Name;
		destination.MinecraftVersion = source.MinecraftVersion;
		destination.Loader = source.Loader;
		destination.LoaderVersion = source.LoaderVersion;
		destination.VersionName = source.VersionName;
		destination.VersionType = source.VersionType;
		destination.Description = source.Description;
		destination.IconSource = source.IconSource;
		destination.InstanceDirectory = source.InstanceDirectory;
		destination.BackupDirectory = source.BackupDirectory;
		destination.MemorySettingsMode = source.MemorySettingsMode;
		destination.MemoryMb = source.MemoryMb;
		destination.WindowWidth = source.WindowWidth;
		destination.WindowHeight = source.WindowHeight;
		destination.PreLaunchCommand = source.PreLaunchCommand;
		destination.WaitForPreLaunchCommand = source.WaitForPreLaunchCommand;
		destination.PostExitCommand = source.PostExitCommand;
		destination.JvmArguments = source.JvmArguments;
		destination.GameArguments = source.GameArguments;
		destination.LaunchSettingsMode = source.LaunchSettingsMode;
		destination.JavaSettingsMode = source.JavaSettingsMode;
		destination.JavaSelectionMode = source.JavaSelectionMode;
		destination.SelectedJavaExecutablePath = source.SelectedJavaExecutablePath;
		destination.CheckFilesBeforeLaunch = source.CheckFilesBeforeLaunch;
		destination.AutoRepairMissingFiles = source.AutoRepairMissingFiles;
		destination.MinimizeLauncherAfterLaunch = source.MinimizeLauncherAfterLaunch;
		destination.LaunchFullScreen = source.LaunchFullScreen;
		destination.AutoJoinServerAddress = source.AutoJoinServerAddress;
		destination.CreatedAt = source.CreatedAt;
		destination.UpdatedAt = source.UpdatedAt;
	}
}
