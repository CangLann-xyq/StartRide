using Launcher.App.Resources;
using Launcher.Domain.Models;

namespace Launcher.App.Utilities;

internal static class LauncherProgressTextFormatter
{
	public static string FormatDownloadSpeed(DownloadSpeedTelemetry telemetry)
	{
		long? bytesPerSecond = telemetry.BytesPerSecond;
		if (bytesPerSecond.HasValue)
		{
			long valueOrDefault = bytesPerSecond.GetValueOrDefault();
			if (valueOrDefault > 0)
			{
				if (valueOrDefault < 1048576)
				{
					if (valueOrDefault >= 1024)
					{
						return string.Format(Strings.DownloadSpeed_KilobytesPerSecondFormat, (double)valueOrDefault / 1024.0);
					}
					return string.Format(Strings.DownloadSpeed_BytesPerSecondFormat, valueOrDefault);
				}
				return string.Format(Strings.DownloadSpeed_MegabytesPerSecondFormat, (double)valueOrDefault / 1024.0 / 1024.0);
			}
		}
		return string.Empty;
	}

	public static string Format(LauncherProgress progress)
	{
		switch (progress.Stage)
		{
		case "Install.Queue":
			return Strings.Status_InstallQueued;
		case "Install.Preparing":
			return Strings.Status_InstallPreparing;
		case "Install.DownloadingLoaderInstaller":
			return Strings.Status_InstallDownloadingLoaderInstaller;
		case "Install.CheckingJava":
			return Strings.Status_LaunchCheckingJava;
		case "Install.DownloadingJava":
			return Strings.Status_InstallDownloadingJava;
		case "Install.RunningLoaderInstaller":
			return Strings.Status_InstallRunningLoaderInstaller;
		case "Install.FinalizingVersion":
			return Strings.Status_InstallFinalizingVersion;
		case "Install.CompletingFiles":
			return Strings.Status_InstallCompletingFiles;
		case "Import.PreparingArchive":
			return Strings.Status_ModpackPreparingArchive;
		case "Import.ParsingManifest":
			return Strings.Status_ModpackParsingManifest;
		case "Import.ResolvingPackFiles":
			if (!string.IsNullOrWhiteSpace(progress.Message))
			{
				return string.Format(Strings.Status_ModpackResolvingFilesFormat, progress.Message);
			}
			return Strings.Status_ModpackResolvingFiles;
		case "Import.CreatingInstance":
			return Strings.Status_ModpackCreatingInstance;
		case "Import.InstallingMinecraftBase":
			return Strings.Status_ModpackInstallingMinecraftBase;
		case "Import.InstallingLoader":
			return Strings.Status_ModpackInstallingLoader;
		case "Import.DownloadingPackFiles":
			if (!string.IsNullOrWhiteSpace(progress.Message))
			{
				return string.Format(Strings.Status_ModpackDownloadingFileFormat, progress.Message);
			}
			return Strings.Status_ModpackDownloadingFiles;
		case "Import.ProcessingPackFiles":
			return Strings.Status_ModpackProcessingFiles;
		case "Import.CopyingOverrides":
			return Strings.Status_ModpackCopyingOverrides;
		case "Import.CleaningUp":
			return Strings.Status_ModpackCleaningUp;
		case "Launch.CheckingJava":
			return Strings.Status_LaunchCheckingJava;
		case "Launch.DownloadingJava":
			return Strings.Status_InstallDownloadingJava;
		case "Files":
			return Strings.Status_InstallCheckingFiles;
		case "Bytes":
			return Strings.Status_InstallDownloadingFiles;
		case "Mod.DownloadingFile":
			if (!string.IsNullOrWhiteSpace(progress.Message))
			{
				return string.Format(Strings.Status_ModDownloadingFormat, progress.Message);
			}
			return Strings.Status_ModDownloading;
		default:
			if (!string.IsNullOrWhiteSpace(progress.Message))
			{
				return progress.Message;
			}
			return Strings.DownloadTask_Preparing;
		}
	}
}
