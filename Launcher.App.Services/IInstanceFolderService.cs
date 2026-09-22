namespace Launcher.App.Services;

public interface IInstanceFolderService
{
	bool DirectoryExists(string folderPath);

	string EnsureDirectoryExists(string folderPath);

	bool TryOpen(string folderPath);

	bool TryOpenFile(string filePath);

	bool TryRevealFile(string filePath);
}
