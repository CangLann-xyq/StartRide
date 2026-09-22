using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.Utilities;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Download;

public sealed class DownloadLocalImportDialogViewModel : ObservableObject
{
	private readonly IFilePickerService filePickerService;

	private readonly ILocalModpackImportService modpackImportService;

	private readonly DownloadTasksPageViewModel downloadTasksPage;

	private readonly IUiDispatcher uiDispatcher;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly DownloadModpackManualDownloadsDialogViewModel modpackManualDownloadsDialog;

	private readonly IExistingFilePathValidator existingFilePathValidator;

	private readonly ILogger<DownloadLocalImportDialogViewModel> logger;

	private DownloadSourcePreference downloadSourcePreference = DownloadSourcePreference.Official;

	private int downloadSpeedLimitMbPerSecond;

	[ObservableProperty]
	private bool isOpen;

	[ObservableProperty]
	private string selectedFilePath = string.Empty;

	[ObservableProperty]
	private string selectedFileName = string.Empty;

	[ObservableProperty]
	private bool isDragOver;

	[ObservableProperty]
	private bool isImporting;

	[ObservableProperty]
	private DownloadLocalImportDialogState dialogState;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmImportCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? confirmUnrecognizedCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? selectFileCommand;

	public bool HasSelectedFile => !string.IsNullOrWhiteSpace(SelectedFilePath);

	public bool IsSelectionState => DialogState == DownloadLocalImportDialogState.Selection;

	public bool IsUnrecognizedState => DialogState == DownloadLocalImportDialogState.Unrecognized;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsOpen
	{
		get
		{
			return isOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsOpen);
				isOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string SelectedFilePath
	{
		get
		{
			return selectedFilePath;
		}
		[MemberNotNull("selectedFilePath")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(selectedFilePath, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedFilePath);
				selectedFilePath = value;
				OnSelectedFilePathChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedFilePath);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string SelectedFileName
	{
		get
		{
			return selectedFileName;
		}
		[MemberNotNull("selectedFileName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(selectedFileName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedFileName);
				selectedFileName = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedFileName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsDragOver
	{
		get
		{
			return isDragOver;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isDragOver, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDragOver);
				isDragOver = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDragOver);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsImporting
	{
		get
		{
			return isImporting;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isImporting, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsImporting);
				isImporting = value;
				OnIsImportingChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsImporting);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public DownloadLocalImportDialogState DialogState
	{
		get
		{
			return dialogState;
		}
		set
		{
			if (!EqualityComparer<DownloadLocalImportDialogState>.Default.Equals(dialogState, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DialogState);
				dialogState = value;
				OnDialogStateChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DialogState);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelCommand => cancelCommand ?? (cancelCommand = new RelayCommand(Cancel));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmImportCommand => confirmImportCommand ?? (confirmImportCommand = new AsyncRelayCommand(ConfirmImportAsync, CanConfirmImport));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ConfirmUnrecognizedCommand => confirmUnrecognizedCommand ?? (confirmUnrecognizedCommand = new RelayCommand(ConfirmUnrecognized));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand SelectFileCommand => selectFileCommand ?? (selectFileCommand = new RelayCommand(SelectFile));

	public event EventHandler<GameInstance>? ModpackImported;

	[RelayCommand]
	private void Cancel()
	{
		logger.LogInformation("Canceled local import dialog. DialogState={DialogState} SelectedFileName={SelectedFileName}", DialogState, string.IsNullOrWhiteSpace(SelectedFileName) ? "<none>" : SelectedFileName);
		Close(resetDialogState: true);
	}

	[RelayCommand(CanExecute = "CanConfirmImport")]
	private async Task ConfirmImportAsync()
	{
		if (!CanConfirmImport())
		{
			return;
		}
		string importPath = SelectedFilePath;
		string importFileName = SelectedFileName;
		logger.LogInformation("Confirmed local modpack import. SelectedFileName={SelectedFileName}", importFileName);
		IsImporting = true;
		try
		{
			ModpackRecognitionResult modpackRecognitionResult = await modpackImportService.RecognizeArchiveAsync(importPath);
			if (!modpackRecognitionResult.IsSuccess)
			{
				logger.LogInformation("Selected local import file was not recognized. SelectedFileName={SelectedFileName} FailureReason={FailureReason}", importFileName, modpackRecognitionResult.FailureReason);
				ExecuteOnUiThread(delegate
				{
					DialogState = DownloadLocalImportDialogState.Unrecognized;
				});
				return;
			}
			DownloadTaskItem createdTask = null;
			ExecuteOnUiThread(delegate
			{
				floatingMessageService.Show(Strings.Status_ModpackInstalling);
				createdTask = downloadTasksPage.BeginTask(Strings.Download_LocalImportTaskTitle, importFileName);
				Close(resetDialogState: true);
			});
			Task task = RunImportTaskAsync(importPath, importFileName, createdTask, downloadSourcePreference, downloadSpeedLimitMbPerSecond);
			downloadTasksPage.TrackBackgroundTask(task);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Unexpected local modpack recognition failure. SelectedFileName={SelectedFileName}", importFileName);
		}
		finally
		{
			ExecuteOnUiThread(delegate
			{
				IsImporting = false;
				ConfirmImportCommand.NotifyCanExecuteChanged();
			});
		}
	}

	public DownloadLocalImportDialogViewModel(IFilePickerService filePickerService, ILocalModpackImportService modpackImportService, DownloadTasksPageViewModel downloadTasksPage, IUiDispatcher uiDispatcher, IFloatingMessageService floatingMessageService, DownloadModpackManualDownloadsDialogViewModel modpackManualDownloadsDialog, IExistingFilePathValidator existingFilePathValidator, ILogger<DownloadLocalImportDialogViewModel>? logger = null)
	{
		this.filePickerService = filePickerService;
		this.modpackImportService = modpackImportService;
		this.downloadTasksPage = downloadTasksPage;
		this.uiDispatcher = uiDispatcher;
		this.floatingMessageService = floatingMessageService;
		this.modpackManualDownloadsDialog = modpackManualDownloadsDialog;
		this.existingFilePathValidator = existingFilePathValidator;
		this.logger = logger ?? NullLogger<DownloadLocalImportDialogViewModel>.Instance;
	}

	public bool CanAcceptDroppedFiles(IReadOnlyList<string> paths)
	{
		string resolvedPath;
		if (!IsImporting)
		{
			return TryResolveSingleFile(paths, out resolvedPath);
		}
		return false;
	}

	public async Task<bool> ImportDroppedFilesAsync(IReadOnlyList<string> paths)
	{
		if (!CanAcceptDroppedFiles(paths) || !TryResolveSingleFile(paths, out string resolvedPath))
		{
			return false;
		}
		Reset();
		SetSelectedFile(resolvedPath, "page-dragdrop");
		await ConfirmImportCommand.ExecuteAsync(null);
		if (DialogState == DownloadLocalImportDialogState.Unrecognized)
		{
			IsOpen = true;
		}
		return true;
	}

	public void Open()
	{
		Reset();
		IsOpen = true;
		logger.LogInformation("Opened local import dialog.");
	}

	public void Reset()
	{
		SelectedFilePath = string.Empty;
		SelectedFileName = string.Empty;
		IsDragOver = false;
		DialogState = DownloadLocalImportDialogState.Selection;
	}

	public bool PreviewDroppedFiles(IReadOnlyList<string> paths)
	{
		if (IsImporting || DialogState != DownloadLocalImportDialogState.Selection)
		{
			IsDragOver = false;
			return false;
		}
		string resolvedPath;
		return IsDragOver = TryResolveSingleFile(paths, out resolvedPath);
	}

	public bool ApplyDroppedFiles(IReadOnlyList<string> paths)
	{
		if (IsImporting || DialogState != DownloadLocalImportDialogState.Selection)
		{
			IsDragOver = false;
			return false;
		}
		if (!TryResolveSingleFile(paths, out string resolvedPath))
		{
			IsDragOver = false;
			return false;
		}
		SetSelectedFile(resolvedPath, "dragdrop");
		IsDragOver = false;
		return true;
	}

	public void ClearDropState()
	{
		IsDragOver = false;
	}

	public void ApplyDownloadSourcePreference(DownloadSourcePreference preference)
	{
		downloadSourcePreference = preference;
	}

	public void ApplyDownloadSpeedLimit(int value)
	{
		downloadSpeedLimitMbPerSecond = Math.Max(value, 0);
	}

	private bool TryResolveSingleFile(IReadOnlyList<string> paths, out string resolvedPath)
	{
		resolvedPath = string.Empty;
		if (paths.Count != 1)
		{
			return false;
		}
		return existingFilePathValidator.TryNormalize(paths[0], out resolvedPath);
	}

	private static IProgress<LauncherProgress> CreateProgressReporter(DownloadTaskItem importTask)
	{
		return importTask.CreateProgress(delegate(LauncherProgress progress)
		{
			importTask.Report(progress with
			{
				Message = LauncherProgressTextFormatter.Format(progress)
			});
		});
	}

	private static string MapFailureMessage(ModpackImportFailureReason failureReason)
	{
		switch (failureReason)
		{
		case ModpackImportFailureReason.FileNotFound:
		case ModpackImportFailureReason.UnsupportedArchive:
		case ModpackImportFailureReason.InvalidManifest:
			return Strings.Status_ModpackInvalidArchive;
		case ModpackImportFailureReason.UnsupportedLoader:
			return Strings.Status_ModpackUnsupportedLoader;
		case ModpackImportFailureReason.MissingCurseForgeApiKey:
			return Strings.Status_ModpackMissingCurseForgeApiKey;
		case ModpackImportFailureReason.HashMismatch:
			return Strings.Status_ModpackHashMismatch;
		case ModpackImportFailureReason.JavaRuntimeUnavailable:
			return Strings.Status_JavaSelectionFailed;
		case ModpackImportFailureReason.ArchiveTooLarge:
			return Strings.Status_ModpackArchiveTooLarge;
		case ModpackImportFailureReason.InsufficientDiskSpace:
			return Strings.Status_ModpackInsufficientDiskSpace;
		default:
			return Strings.Status_ModpackImportFailed;
		}
	}

	private async Task RunImportTaskAsync(string importPath, string importFileName, DownloadTaskItem importTask, DownloadSourcePreference taskDownloadSourcePreference, int taskDownloadSpeedLimitMbPerSecond)
	{
		try
		{
			ModpackImportResult result = await modpackImportService.ImportFromArchiveAsync(importPath, CreateProgressReporter(importTask), importTask.CancellationToken, taskDownloadSourcePreference, taskDownloadSpeedLimitMbPerSecond);
			if (result.IsSuccess && result.ImportedInstance != null)
			{
				if (result.HasManualDownloads)
				{
					importTask.Complete(string.Format(Strings.Status_ModpackImportedWithManualDownloadsFormat, result.ImportedInstance.Name));
					ExecuteOnUiThread(delegate
					{
						modpackManualDownloadsDialog.Show(result.ImportedInstance, result.ManualDownloads);
					});
				}
				else
				{
					importTask.Complete(string.Format(Strings.Status_ModpackImportedFormat, result.ImportedInstance.Name));
				}
				ModpackImported?.Invoke(this, result.ImportedInstance);
			}
			else
			{
				importTask.Fail(MapFailureMessage(result.FailureReason));
			}
		}
		catch (OperationCanceledException) when (importTask.IsCancellationRequested)
		{
			downloadTasksPage.CancelTask(importTask);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Unexpected local modpack import failure. SelectedFileName={SelectedFileName}", importFileName);
			importTask.Fail(Strings.Status_ModpackImportFailed);
		}
	}

	[RelayCommand]
	private void ConfirmUnrecognized()
	{
		logger.LogInformation("Acknowledged unrecognized local import file. SelectedFileName={SelectedFileName}", string.IsNullOrWhiteSpace(SelectedFileName) ? "<none>" : SelectedFileName);
		Close(resetDialogState: false);
	}

	[RelayCommand]
	private void SelectFile()
	{
		string text = filePickerService.PickLocalImportFile();
		if (!string.IsNullOrWhiteSpace(text) && TryResolveSingleFile(new global::_003C_003Ez__ReadOnlySingleElementList<string>(text), out string _))
		{
			SetSelectedFile(text, "picker");
		}
	}

	private void SetSelectedFile(string path, string source)
	{
		string path2 = (SelectedFilePath = Path.GetFullPath(path));
		SelectedFileName = Path.GetFileName(path2);
		DialogState = DownloadLocalImportDialogState.Selection;
		logger.LogInformation("Selected local import file. Source={Source} SelectedFileName={SelectedFileName}", source, SelectedFileName);
	}

	private bool CanConfirmImport()
	{
		if (!IsImporting && DialogState == DownloadLocalImportDialogState.Selection)
		{
			return HasSelectedFile;
		}
		return false;
	}

	private void RefreshConfirmImportCanExecute()
	{
		ExecuteOnUiThread(delegate
		{
			ConfirmImportCommand.NotifyCanExecuteChanged();
		});
	}

	private void ExecuteOnUiThread(Action action)
	{
		if (uiDispatcher.HasAccess)
		{
			action();
		}
		else
		{
			uiDispatcher.Invoke(action);
		}
	}

	private void Close(bool resetDialogState)
	{
		IsOpen = false;
		SelectedFilePath = string.Empty;
		SelectedFileName = string.Empty;
		IsDragOver = false;
		if (resetDialogState)
		{
			DialogState = DownloadLocalImportDialogState.Selection;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedFilePathChanged(string value)
	{
		OnPropertyChanged("HasSelectedFile");
		RefreshConfirmImportCanExecute();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsImportingChanged(bool value)
	{
		RefreshConfirmImportCanExecute();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDialogStateChanged(DownloadLocalImportDialogState value)
	{
		OnPropertyChanged("IsSelectionState");
		OnPropertyChanged("IsUnrecognizedState");
		RefreshConfirmImportCanExecute();
	}
}
