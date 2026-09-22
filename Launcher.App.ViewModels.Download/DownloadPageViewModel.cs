using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Download;

public sealed class DownloadPageViewModel : ObservableObject, IDisposable
{
	private sealed class NullFilePickerService : IFilePickerService
	{
		public static NullFilePickerService Instance { get; } = new NullFilePickerService();

		private NullFilePickerService()
		{
		}

		public string? PickMinecraftSkin()
		{
			return null;
		}

		public string? PickJavaExecutable()
		{
			return null;
		}

		public string? PickLocalImportFile()
		{
			return null;
		}

		public string? PickModFile()
		{
			return null;
		}

		public string? PickSaveArchive()
		{
			return null;
		}

		public string? PickResourcePackArchive()
		{
			return null;
		}

		public string? PickShaderPackArchive()
		{
			return null;
		}

		public string? PickModpackExportArchive(string defaultFileName, ModpackExportKind kind)
		{
			return null;
		}

		public string? PickLaunchDiagnosticExportArchive(string instanceName)
		{
			return null;
		}

		public string? PickCustomDownloadDestination(string defaultFileName)
		{
			return null;
		}

		public string? PickFolder(string title, string? initialDirectory = null)
		{
			return null;
		}
	}

	private sealed class NullInstanceFolderService : IInstanceFolderService
	{
		public static NullInstanceFolderService Instance { get; } = new NullInstanceFolderService();

		private NullInstanceFolderService()
		{
		}

		public bool DirectoryExists(string folderPath)
		{
			return false;
		}

		public string EnsureDirectoryExists(string folderPath)
		{
			return folderPath;
		}

		public bool TryOpen(string folderPath)
		{
			return false;
		}

		public bool TryOpenFile(string filePath)
		{
			return false;
		}

		public bool TryRevealFile(string filePath)
		{
			return false;
		}
	}

	private sealed class NullLocalModpackImportService : ILocalModpackImportService
	{
		public static NullLocalModpackImportService Instance { get; } = new NullLocalModpackImportService();

		private NullLocalModpackImportService()
		{
		}

		public Task<ModpackRecognitionResult> RecognizeArchiveAsync(string archivePath, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.FromResult(ModpackRecognitionResult.Failure(ModpackRecognitionFailureReason.UnsupportedArchive));
		}

		public Task<ModpackImportResult> ImportFromArchiveAsync(string archivePath, IProgress<LauncherProgress>? progress, CancellationToken cancellationToken = default(CancellationToken), DownloadSourcePreference downloadSourcePreference = DownloadSourcePreference.Official, int downloadSpeedLimitMbPerSecond = 0)
		{
			return Task.FromResult(ModpackImportResult.Failure(ModpackImportFailureReason.UnsupportedArchive));
		}
	}

	private sealed class NullFloatingMessageService : IFloatingMessageService
	{
		public static NullFloatingMessageService Instance { get; } = new NullFloatingMessageService();

		public event Action<FloatingMessageRequest>? MessageRequested
		{
			add
			{
			}
			remove
			{
			}
		}

		private NullFloatingMessageService()
		{
		}

		public void Show(string message)
		{
		}

		public void ShowDragHint(object source, string message)
		{
		}

		public void ClearDragHint(object source)
		{
		}

		public void ClearDragHint()
		{
		}
	}

	private sealed class RejectingExistingFilePathValidator : IExistingFilePathValidator
	{
		public static RejectingExistingFilePathValidator Instance { get; } = new RejectingExistingFilePathValidator();

		public bool TryNormalize(string? path, out string normalizedPath)
		{
			normalizedPath = string.Empty;
			return false;
		}
	}

	private readonly IFloatingMessageService floatingMessageService;

	private readonly DownloadTasksPageViewModel downloadTasksPage;

	private readonly ILogger<DownloadPageViewModel> logger;

	private CancellationTokenSource? optionsNavigationCancellation;

	[ObservableProperty]
	private DownloadPageStep currentStep;

	[ObservableProperty]
	private int contentRefreshToken;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? backToVersionListCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? installCommand;

	public DownloadVersionListViewModel VersionList { get; }

	public DownloadInstanceOptionsViewModel InstanceOptions { get; }

	public DownloadInstallViewModel InstallState { get; }

	public DownloadModpackManualDownloadsDialogViewModel ModpackManualDownloadsDialog { get; }

	public DownloadLocalImportDialogViewModel LocalImportDialog { get; }

	public bool IsVersionListStep => CurrentStep == DownloadPageStep.VersionList;

	public bool IsInstanceOptionsStep => CurrentStep == DownloadPageStep.InstanceOptions;

	public bool IsDownloadContentVisible
	{
		get
		{
			if (!IsInstanceOptionsStep)
			{
				return VersionList.HasVisibleVersions;
			}
			return true;
		}
	}

	public bool CanInstallSelectedVersion => InstanceOptions.CanInstall;

	public string InstallButtonText => Strings.Download_InstallButton;

	public string PageTitle
	{
		get
		{
			object obj;
			if (IsInstanceOptionsStep)
			{
				obj = VersionList.SelectedMinecraftVersion?.Name ?? string.Empty;
			}
			else
			{
				obj = VersionList.SelectedVersionCategory?.Title;
				if (obj == null)
				{
					return string.Empty;
				}
			}
			return (string)obj;
		}
	}

	public string? PageTitleIconSource
	{
		get
		{
			if (!IsInstanceOptionsStep)
			{
				return null;
			}
			return VersionList.SelectedMinecraftVersion?.IconSource;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public DownloadPageStep CurrentStep
	{
		get
		{
			return currentStep;
		}
		set
		{
			if (!EqualityComparer<DownloadPageStep>.Default.Equals(currentStep, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentStep);
				currentStep = value;
				OnCurrentStepChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentStep);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int ContentRefreshToken
	{
		get
		{
			return contentRefreshToken;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(contentRefreshToken, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ContentRefreshToken);
				contentRefreshToken = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ContentRefreshToken);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand BackToVersionListCommand => backToVersionListCommand ?? (backToVersionListCommand = new RelayCommand(BackToVersionList));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand InstallCommand => installCommand ?? (installCommand = new AsyncRelayCommand(InstallAsync, () => CanInstallSelectedVersion, AsyncRelayCommandOptions.AllowConcurrentExecutions));

	public event EventHandler<GameInstance>? InstanceInstalled;

	[RelayCommand]
	private void BackToVersionList()
	{
		ShowVersionList();
	}

	[RelayCommand(CanExecute = "CanInstallSelectedVersion", AllowConcurrentExecutions = true)]
	private async Task InstallAsync()
	{
		DownloadInstallRequest downloadInstallRequest = InstanceOptions.CreateInstallRequest();
		if ((object)downloadInstallRequest != null)
		{
			CurrentStep = DownloadPageStep.VersionList;
			ContentRefreshToken++;
			Task task = InstallState.InstallAsync(downloadInstallRequest);
			downloadTasksPage.TrackBackgroundTask(task);
			await task;
		}
	}

	public DownloadPageViewModel(IGameVersionService gameVersionService, IGameInstanceService instanceService, DownloadTasksPageViewModel downloadTasksPage, IEnumerable<ILoaderProvider> loaderProviders)
		: this(gameVersionService, instanceService, downloadTasksPage, loaderProviders, ImmediateUiDispatcher.Instance, NullFloatingMessageService.Instance, NullInstanceFolderService.Instance, NullFilePickerService.Instance, NullLocalModpackImportService.Instance, RejectingExistingFilePathValidator.Instance)
	{
	}

	public DownloadPageViewModel(IGameVersionService gameVersionService, IGameInstanceService instanceService, DownloadTasksPageViewModel downloadTasksPage, IEnumerable<ILoaderProvider> loaderProviders, IUiDispatcher uiDispatcher)
		: this(gameVersionService, instanceService, downloadTasksPage, loaderProviders, uiDispatcher, NullFloatingMessageService.Instance, NullInstanceFolderService.Instance, NullFilePickerService.Instance, NullLocalModpackImportService.Instance, RejectingExistingFilePathValidator.Instance)
	{
	}

	public DownloadPageViewModel(IGameVersionService gameVersionService, IGameInstanceService instanceService, DownloadTasksPageViewModel downloadTasksPage, IEnumerable<ILoaderProvider> loaderProviders, IUiDispatcher uiDispatcher, IFloatingMessageService floatingMessageService, IInstanceFolderService instanceFolderService, IFilePickerService filePickerService, ILocalModpackImportService localModpackImportService, IExistingFilePathValidator existingFilePathValidator, IInstanceInstallNameAvailabilityService? instanceInstallNameAvailabilityService = null, IModrinthService? modrinthService = null, ILogger<DownloadLocalImportDialogViewModel>? localImportLogger = null, ILogger<DownloadInstallViewModel>? installLogger = null, ILogger<DownloadPageViewModel>? logger = null)
	{
		this.floatingMessageService = floatingMessageService;
		this.downloadTasksPage = downloadTasksPage;
		this.logger = logger ?? NullLogger<DownloadPageViewModel>.Instance;
		DownloadInstanceNameTracker instanceNameTracker = new DownloadInstanceNameTracker();
		VersionList = new DownloadVersionListViewModel(gameVersionService, uiDispatcher);
		InstanceOptions = new DownloadInstanceOptionsViewModel(instanceService, loaderProviders, instanceNameTracker, instanceInstallNameAvailabilityService, modrinthService, this.logger);
		InstallState = new DownloadInstallViewModel(instanceService, downloadTasksPage, instanceNameTracker, uiDispatcher, floatingMessageService, installLogger);
		ModpackManualDownloadsDialog = new DownloadModpackManualDownloadsDialogViewModel(instanceFolderService, floatingMessageService);
		LocalImportDialog = new DownloadLocalImportDialogViewModel(filePickerService, localModpackImportService, downloadTasksPage, uiDispatcher, floatingMessageService, ModpackManualDownloadsDialog, existingFilePathValidator, localImportLogger);
		VersionList.VersionSelected += VersionList_VersionSelected;
		VersionList.LocalImportRequested += VersionList_LocalImportRequested;
		VersionList.CategoryContentRefreshRequested += VersionList_CategoryContentRefreshRequested;
		VersionList.PropertyChanged += VersionList_PropertyChanged;
		InstanceOptions.InstallAvailabilityChanged += InstanceOptions_InstallAvailabilityChanged;
		InstallState.InstanceInstalled += InstallState_InstanceInstalled;
		InstallState.NameAvailabilityChanged += InstallState_NameAvailabilityChanged;
		LocalImportDialog.ModpackImported += LocalImportDialog_ModpackImported;
	}

	private bool CanHandleLocalImportDropCore(IReadOnlyList<string> paths)
	{
		if (!LocalImportDialog.CanAcceptDroppedFiles(paths))
		{
			return false;
		}
		string extension = Path.GetExtension(paths[0]);
		if (!string.Equals(extension, ".mrpack", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private void CancelOptionsNavigation()
	{
		Interlocked.Exchange(ref optionsNavigationCancellation, null)?.Cancel();
	}

	private void ShowVersionList()
	{
		CancelOptionsNavigation();
		CurrentStep = DownloadPageStep.VersionList;
		InstanceOptions.Deactivate();
		VersionList.ClearSelectedVersion();
	}

	private void VersionList_VersionSelected(DownloadMinecraftVersionItem version)
	{
		OpenInstanceOptionsAsync(version);
	}

	private async Task OpenInstanceOptionsAsync(DownloadMinecraftVersionItem version)
	{
		CancelOptionsNavigation();
		CancellationTokenSource cancellation = (optionsNavigationCancellation = new CancellationTokenSource());
		try
		{
			Task task = InstanceOptions.PrepareAsync(version, cancellation.Token);
			if (optionsNavigationCancellation != cancellation || VersionList.SelectedMinecraftVersion != version)
			{
				await task;
				return;
			}
			CurrentStep = DownloadPageStep.InstanceOptions;
			await task;
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to prepare instance installation options. MinecraftVersion={MinecraftVersion}", version.Name);
			if (optionsNavigationCancellation == cancellation)
			{
				CurrentStep = DownloadPageStep.InstanceOptions;
			}
		}
		finally
		{
			Interlocked.CompareExchange(ref optionsNavigationCancellation, null, cancellation);
			cancellation.Dispose();
		}
	}

	private void VersionList_LocalImportRequested()
	{
		LocalImportDialog.Open();
	}

	private void VersionList_CategoryContentRefreshRequested()
	{
		ContentRefreshToken++;
	}

	private void VersionList_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
		case "SelectedVersionCategory":
			if (IsInstanceOptionsStep)
			{
				ShowVersionList();
			}
			OnPropertyChanged("PageTitle");
			break;
		case "SelectedMinecraftVersion":
			OnPropertyChanged("PageTitle");
			OnPropertyChanged("PageTitleIconSource");
			break;
		case "VisibleVersions":
		case "HasVisibleVersions":
			OnPropertyChanged("IsDownloadContentVisible");
			break;
		}
	}

	private void InstanceOptions_InstallAvailabilityChanged()
	{
		OnPropertyChanged("CanInstallSelectedVersion");
		InstallCommand.NotifyCanExecuteChanged();
	}

	private void InstallState_NameAvailabilityChanged()
	{
		InstanceOptions.NotifyNameAvailabilityChanged();
	}

	private void InstallState_InstanceInstalled(object? sender, GameInstance instance)
	{
		InstanceInstalled?.Invoke(this, instance);
	}

	private void LocalImportDialog_ModpackImported(object? sender, GameInstance instance)
	{
		InstanceInstalled?.Invoke(this, instance);
	}

	public void PrimeFromSettings(LauncherSettings settings)
	{
		ApplyDownloadSourcePreference(settings.DownloadSourcePreference);
		ApplyDownloadSpeedLimit(settings.DownloadSpeedLimitMbPerSecond);
		ApplyMinecraftDirectory(settings.MinecraftDirectory);
	}

	public void ApplyMinecraftDirectory(string? minecraftDirectory)
	{
		InstanceOptions.ApplyMinecraftDirectory(minecraftDirectory);
	}

	public void ApplyDownloadSourcePreference(DownloadSourcePreference preference)
	{
		LocalImportDialog.ApplyDownloadSourcePreference(preference);
		VersionList.ApplyDownloadSourcePreference(preference);
		InstanceOptions.ApplyDownloadSourcePreference(preference);
		if (IsInstanceOptionsStep && VersionList.SelectedMinecraftVersion == null)
		{
			BackToVersionList();
		}
	}

	public void ApplyDownloadSpeedLimit(int downloadSpeedLimitMbPerSecond)
	{
		int value = Math.Max(downloadSpeedLimitMbPerSecond, 0);
		LocalImportDialog.ApplyDownloadSpeedLimit(value);
		VersionList.ApplyDownloadSpeedLimit(value);
		InstanceOptions.ApplyDownloadSpeedLimit(value);
	}

	public bool CanHandleLocalImportDrop(IReadOnlyList<string> paths)
	{
		return CanHandleLocalImportDropCore(paths);
	}

	public bool UpdateLocalImportDropState(IReadOnlyList<string> paths)
	{
		bool flag = CanHandleLocalImportDropCore(paths);
		floatingMessageService.ShowDragHint(this, flag ? Strings.GameSettings_DropReleaseToImportMessage : Strings.GameSettings_DropUnsupportedFileMessage);
		return flag;
	}

	public void ClearLocalImportDropState()
	{
		floatingMessageService.ClearDragHint(this);
	}

	public async Task<bool> HandleLocalImportDropAsync(IReadOnlyList<string> paths)
	{
		if (!CanHandleLocalImportDropCore(paths))
		{
			return false;
		}
		try
		{
			return await LocalImportDialog.ImportDroppedFilesAsync(paths);
		}
		finally
		{
			ClearLocalImportDropState();
		}
	}

	public Task EnsureVersionsLoadedAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return VersionList.EnsureVersionsLoadedAsync(cancellationToken);
	}

	public void Dispose()
	{
		CancelOptionsNavigation();
		VersionList.VersionSelected -= VersionList_VersionSelected;
		VersionList.LocalImportRequested -= VersionList_LocalImportRequested;
		VersionList.CategoryContentRefreshRequested -= VersionList_CategoryContentRefreshRequested;
		VersionList.PropertyChanged -= VersionList_PropertyChanged;
		InstanceOptions.InstallAvailabilityChanged -= InstanceOptions_InstallAvailabilityChanged;
		InstallState.InstanceInstalled -= InstallState_InstanceInstalled;
		InstallState.NameAvailabilityChanged -= InstallState_NameAvailabilityChanged;
		LocalImportDialog.ModpackImported -= LocalImportDialog_ModpackImported;
		VersionList.Dispose();
		InstanceOptions.Dispose();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnCurrentStepChanged(DownloadPageStep value)
	{
		OnPropertyChanged("IsVersionListStep");
		OnPropertyChanged("IsInstanceOptionsStep");
		OnPropertyChanged("IsDownloadContentVisible");
		OnPropertyChanged("PageTitle");
		OnPropertyChanged("PageTitleIconSource");
		OnPropertyChanged("CanInstallSelectedVersion");
		InstallCommand.NotifyCanExecuteChanged();
	}
}
