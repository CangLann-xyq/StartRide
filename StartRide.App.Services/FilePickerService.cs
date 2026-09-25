using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using StartRide.App.Resources;
using Launcher.Application.Services;
using Microsoft.Win32;

namespace StartRide.App.Services;

public sealed class FilePickerService : IFilePickerService
{
	public string? PickMinecraftSkin()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = Strings.FilePicker_MinecraftSkinTitle,
			Filter = Strings.FilePicker_MinecraftSkinFilter,
			CheckFileExists = true,
			Multiselect = false
		};
		if (openFileDialog.ShowDialog(System.Windows.Application.Current?.MainWindow) != true)
		{
			return null;
		}
		return openFileDialog.FileName;
	}

	public string? PickJavaExecutable()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = Strings.FilePicker_JavaExecutableTitle,
			Filter = Strings.FilePicker_JavaExecutableFilter,
			CheckFileExists = true,
			Multiselect = false
		};
		if (openFileDialog.ShowDialog(System.Windows.Application.Current?.MainWindow) != true)
		{
			return null;
		}
		return openFileDialog.FileName;
	}

	public string? PickLocalImportFile()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = Strings.FilePicker_LocalImportFileTitle,
			Filter = Strings.FilePicker_LocalImportFileFilter,
			CheckFileExists = true,
			Multiselect = false
		};
		if (openFileDialog.ShowDialog(System.Windows.Application.Current?.MainWindow) != true)
		{
			return null;
		}
		return openFileDialog.FileName;
	}

	public string? PickModFile()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = Strings.FilePicker_ModFileTitle,
			Filter = Strings.FilePicker_ModFileFilter,
			CheckFileExists = true,
			Multiselect = false
		};
		if (openFileDialog.ShowDialog(System.Windows.Application.Current?.MainWindow) != true)
		{
			return null;
		}
		return openFileDialog.FileName;
	}

	public string? PickSaveArchive()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = Strings.FilePicker_SaveArchiveTitle,
			Filter = Strings.FilePicker_SaveArchiveFilter,
			CheckFileExists = true,
			Multiselect = false
		};
		if (openFileDialog.ShowDialog(System.Windows.Application.Current?.MainWindow) != true)
		{
			return null;
		}
		return openFileDialog.FileName;
	}

	public string? PickResourcePackArchive()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = Strings.FilePicker_ResourcePackArchiveTitle,
			Filter = Strings.FilePicker_ResourcePackArchiveFilter,
			CheckFileExists = true,
			Multiselect = false
		};
		if (openFileDialog.ShowDialog(System.Windows.Application.Current?.MainWindow) != true)
		{
			return null;
		}
		return openFileDialog.FileName;
	}

	public string? PickShaderPackArchive()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = Strings.FilePicker_ShaderPackArchiveTitle,
			Filter = Strings.FilePicker_ShaderPackArchiveFilter,
			CheckFileExists = true,
			Multiselect = false
		};
		if (openFileDialog.ShowDialog(System.Windows.Application.Current?.MainWindow) != true)
		{
			return null;
		}
		return openFileDialog.FileName;
	}

	public string? PickModpackExportArchive(string defaultFileName, ModpackExportKind kind)
	{
		bool flag = kind == ModpackExportKind.Modrinth;
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Title = (flag ? Strings.FilePicker_ModrinthModpackExportArchiveTitle : Strings.FilePicker_ModpackExportArchiveTitle),
			Filter = (flag ? Strings.FilePicker_ModrinthModpackExportArchiveFilter : Strings.FilePicker_ModpackExportArchiveFilter),
			FileName = ((!string.IsNullOrWhiteSpace(defaultFileName)) ? defaultFileName : (flag ? "modpack.mrpack" : "modpack.zip")),
			AddExtension = true,
			DefaultExt = (flag ? ".mrpack" : ".zip"),
			OverwritePrompt = true
		};
		if (saveFileDialog.ShowDialog(System.Windows.Application.Current?.MainWindow) != true)
		{
			return null;
		}
		return saveFileDialog.FileName;
	}

	public string? PickLaunchDiagnosticExportArchive(string instanceName)
	{
		HashSet<char> invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
		string value = new string((instanceName ?? string.Empty).Select((char character) => (!invalidCharacters.Contains(character)) ? character : '_').ToArray()).Trim();
		if (string.IsNullOrWhiteSpace(value))
		{
			value = "Minecraft";
		}
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Title = Strings.FilePicker_LaunchDiagnosticExportTitle,
			Filter = Strings.FilePicker_LaunchDiagnosticExportFilter,
			FileName = $"StartRide-{value}-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
			AddExtension = true,
			DefaultExt = ".zip",
			OverwritePrompt = true
		};
		if (saveFileDialog.ShowDialog(System.Windows.Application.Current?.MainWindow) != true)
		{
			return null;
		}
		return saveFileDialog.FileName;
	}

	public string? PickCustomDownloadDestination(string defaultFileName)
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Title = Strings.FilePicker_CustomFileDownloadTitle,
			Filter = Strings.FilePicker_CustomFileDownloadFilter,
			FileName = (string.IsNullOrWhiteSpace(defaultFileName) ? "download" : defaultFileName),
			AddExtension = false,
			OverwritePrompt = true
		};
		if (saveFileDialog.ShowDialog(System.Windows.Application.Current?.MainWindow) != true)
		{
			return null;
		}
		return saveFileDialog.FileName;
	}

	public string? PickResourceProjectDestination(string title, string defaultFileName, string? initialDirectory = null)
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Title = title,
			Filter = Strings.FilePicker_CustomFileDownloadFilter,
			FileName = (string.IsNullOrWhiteSpace(defaultFileName) ? "download" : defaultFileName),
			AddExtension = false,
			OverwritePrompt = true,
			CheckPathExists = true,
			RestoreDirectory = true
		};
		if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
		{
			saveFileDialog.InitialDirectory = initialDirectory;
		}
		if (saveFileDialog.ShowDialog(System.Windows.Application.Current?.MainWindow) != true)
		{
			return null;
		}
		return saveFileDialog.FileName;
	}

	public string? PickFolder(string title, string? initialDirectory = null)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = title,
			Multiselect = false
		};
		if (!string.IsNullOrWhiteSpace(initialDirectory))
		{
			string fullPath = Path.GetFullPath(initialDirectory);
			if (Directory.Exists(fullPath))
			{
				openFolderDialog.InitialDirectory = fullPath;
			}
		}
		if (openFolderDialog.ShowDialog(System.Windows.Application.Current?.MainWindow) != true)
		{
			return null;
		}
		return openFolderDialog.FolderName;
	}
}
