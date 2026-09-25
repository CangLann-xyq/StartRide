using System.Diagnostics;
using System.IO;

namespace StartRide.App.Services;

public sealed class InstanceFolderService : IInstanceFolderService
{
	public bool DirectoryExists(string folderPath)
	{
		if (!string.IsNullOrWhiteSpace(folderPath))
		{
			return Directory.Exists(folderPath);
		}
		return false;
	}

	public string EnsureDirectoryExists(string folderPath)
	{
		string fullPath = Path.GetFullPath(folderPath);
		Directory.CreateDirectory(fullPath);
		return fullPath;
	}

	public bool TryOpen(string folderPath)
	{
		if (!DirectoryExists(folderPath))
		{
			return false;
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = "\"" + folderPath + "\"",
				UseShellExecute = true
			});
			return true;
		}
		catch
		{
			return false;
		}
	}

	public bool TryOpenFile(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
		{
			return false;
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = filePath,
				UseShellExecute = true
			});
			return true;
		}
		catch
		{
			return false;
		}
	}

	public bool TryRevealFile(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
		{
			return false;
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = "/select,\"" + filePath + "\"",
				UseShellExecute = true
			});
			return true;
		}
		catch
		{
			return false;
		}
	}
}
