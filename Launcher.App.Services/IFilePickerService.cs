using Launcher.Application.Services;

namespace Launcher.App.Services;

public interface IFilePickerService
{
	string? PickMinecraftSkin();

	string? PickJavaExecutable();

	string? PickLocalImportFile();

	string? PickModFile();

	string? PickSaveArchive();

	string? PickResourcePackArchive();

	string? PickShaderPackArchive();

	string? PickModpackExportArchive(string defaultFileName, ModpackExportKind kind);

	string? PickLaunchDiagnosticExportArchive(string instanceName);

	string? PickCustomDownloadDestination(string defaultFileName);

	string? PickResourceProjectDestination(string title, string defaultFileName, string? initialDirectory = null)
	{
		return PickCustomDownloadDestination(defaultFileName);
	}

	string? PickFolder(string title, string? initialDirectory = null);
}
