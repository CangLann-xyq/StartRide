using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Logging;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.ViewModels.Download;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Launcher.App.ViewModels.Settings;

public sealed partial class GeneralSettingsViewModel : SettingsSectionViewModelBase, IDisposable
{
	private readonly IStatusService statusService;

	private readonly IFilePickerService filePickerService;

	private readonly IInstanceFolderService instanceFolderService;

	private readonly IMinecraftDirectoryFileSystem minecraftDirectoryFileSystem;

	private readonly MinecraftDirectoryManagementService minecraftDirectoryManagementService;

	private readonly DownloadTasksPageViewModel? downloadTasksPage;

	private readonly ILauncherLogLevelController? logLevelController;

	private readonly ILogger logger;

	private bool suppressMinecraftDirectorySelectionChanged;

	private bool isChangingMinecraftDirectory;

	private bool isGameLaunchInProgress;

	private string? minecraftDirectoryPendingNamePath;

	private bool isMinecraftDirectoryNameDialogForAdd;

	private int minecraftDirectoryAvailabilityGeneration;

	[ObservableProperty]
	private string minecraftDirectory = string.Empty;

	[ObservableProperty]
	private string launcherLogDirectory = string.Empty;

	[ObservableProperty]
	private bool diagnosticLoggingEnabled;

	[ObservableProperty]
	private SettingsMinecraftDirectoryItem? selectedMinecraftDirectory;

	[ObservableProperty]
	private bool isRemoveMinecraftDirectoryDialogOpen;

	[ObservableProperty]
	private bool isClearLauncherLogsDialogOpen;

	[ObservableProperty]
	[NotifyPropertyChangedFor("MinecraftDirectoryNameDialogTitle")]
	[NotifyPropertyChangedFor("MinecraftDirectoryNameDialogConfirmButtonText")]
	[NotifyPropertyChangedFor("CanConfirmMinecraftDirectoryName")]
	[NotifyCanExecuteChangedFor("ConfirmMinecraftDirectoryNameCommand")]
	private bool isMinecraftDirectoryNameDialogOpen;

	[ObservableProperty]
	[NotifyPropertyChangedFor("IsMinecraftDirectoryNameInvalid")]
	[NotifyPropertyChangedFor("CanConfirmMinecraftDirectoryName")]
	[NotifyCanExecuteChangedFor("ConfirmMinecraftDirectoryNameCommand")]
	private string minecraftDirectoryName = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor("CanConfirmMinecraftDirectoryName")]
	[NotifyCanExecuteChangedFor("ConfirmMinecraftDirectoryNameCommand")]
	[NotifyCanExecuteChangedFor("RequestRenameMinecraftDirectoryCommand")]
	private bool isMinecraftDirectoryNameDialogBusy;

	[ObservableProperty]
	[NotifyPropertyChangedFor("RemoveMinecraftDirectoryDialogMessage")]
	[NotifyCanExecuteChangedFor("ConfirmRemoveMinecraftDirectoryCommand")]
	private SettingsMinecraftDirectoryItem? minecraftDirectoryPendingRemoval;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<SettingsMinecraftDirectoryItem?>? openMinecraftDirectoryCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<SettingsMinecraftDirectoryItem?>? requestRenameMinecraftDirectoryCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelMinecraftDirectoryNameCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmMinecraftDirectoryNameCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<SettingsMinecraftDirectoryItem?>? requestRemoveMinecraftDirectoryCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelRemoveMinecraftDirectoryCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmRemoveMinecraftDirectoryCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openLauncherLogDirectoryCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestClearLauncherLogsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelClearLauncherLogsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmClearLauncherLogsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? addMinecraftDirectoryCommand;

	public bool CanChangeMinecraftDirectory
	{
		get
		{
			if (!isChangingMinecraftDirectory)
			{
				return !HasMinecraftDirectoryBlockingActivity;
			}
			return false;
		}
	}

	public bool CanAddMinecraftDirectory => !HasMinecraftDirectoryBlockingActivity;

	public bool IsMinecraftDirectoryChangeBlocked => HasMinecraftDirectoryBlockingActivity;

	private bool HasMinecraftDirectoryBlockingActivity
	{
		get
		{
			DownloadTasksPageViewModel? downloadTasksPageViewModel = downloadTasksPage;
			if (downloadTasksPageViewModel == null || !downloadTasksPageViewModel.HasActiveOperations)
			{
				return isGameLaunchInProgress;
			}
			return true;
		}
	}

	internal Task PendingMinecraftDirectoryChange { get; private set; } = Task.CompletedTask;

	public ObservableCollection<SettingsMinecraftDirectoryItem> MinecraftDirectories { get; } = new ObservableCollection<SettingsMinecraftDirectoryItem>();

	public MinecraftDirectorySwitchDialogViewModel MinecraftDirectorySwitchDialog { get; }

	public string MinecraftDirectoryNameDialogTitle
	{
		get
		{
			if (!isMinecraftDirectoryNameDialogForAdd)
			{
				return Strings.Dialog_RenameMinecraftDirectoryNameTitle;
			}
			return Strings.Dialog_AddMinecraftDirectoryNameTitle;
		}
	}

	public string MinecraftDirectoryNameDialogConfirmButtonText
	{
		get
		{
			if (!isMinecraftDirectoryNameDialogForAdd)
			{
				return Strings.Settings_RenameMinecraftDirectoryConfirmButton;
			}
			return Strings.Settings_AddMinecraftDirectoryConfirmButton;
		}
	}

	public bool IsMinecraftDirectoryNameInvalid
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(MinecraftDirectoryName))
			{
				return MinecraftDirectoryName.Trim().Length > 64;
			}
			return true;
		}
	}

	public bool CanConfirmMinecraftDirectoryName
	{
		get
		{
			if (IsMinecraftDirectoryNameDialogOpen && !IsMinecraftDirectoryNameDialogBusy && !IsMinecraftDirectoryNameInvalid)
			{
				if (isMinecraftDirectoryNameDialogForAdd)
				{
					return CanChangeMinecraftDirectory;
				}
				return true;
			}
			return false;
		}
	}

	public string RemoveMinecraftDirectoryDialogMessage
	{
		get
		{
			if (MinecraftDirectoryPendingRemoval != null)
			{
				return string.Format(Strings.Dialog_RemoveMinecraftDirectoryMessageFormat, MinecraftDirectoryPendingRemoval.DirectoryPath);
			}
			return string.Empty;
		}
	}

	private bool CanConfirmRemoveMinecraftDirectory
	{
		get
		{
			if (!isChangingMinecraftDirectory)
			{
				return MinecraftDirectoryPendingRemoval?.CanRemove ?? false;
			}
			return false;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string MinecraftDirectory
	{
		get
		{
			return minecraftDirectory;
		}
		[MemberNotNull("minecraftDirectory")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(minecraftDirectory, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MinecraftDirectory);
				minecraftDirectory = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MinecraftDirectory);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LauncherLogDirectory
	{
		get
		{
			return launcherLogDirectory;
		}
		[MemberNotNull("launcherLogDirectory")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(launcherLogDirectory, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LauncherLogDirectory);
				launcherLogDirectory = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LauncherLogDirectory);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool DiagnosticLoggingEnabled
	{
		get
		{
			return diagnosticLoggingEnabled;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(diagnosticLoggingEnabled, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DiagnosticLoggingEnabled);
				diagnosticLoggingEnabled = value;
				OnDiagnosticLoggingEnabledChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DiagnosticLoggingEnabled);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsMinecraftDirectoryItem? SelectedMinecraftDirectory
	{
		get
		{
			return selectedMinecraftDirectory;
		}
		set
		{
			if (!EqualityComparer<SettingsMinecraftDirectoryItem>.Default.Equals(selectedMinecraftDirectory, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedMinecraftDirectory);
				selectedMinecraftDirectory = value;
				OnSelectedMinecraftDirectoryChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedMinecraftDirectory);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsRemoveMinecraftDirectoryDialogOpen
	{
		get
		{
			return isRemoveMinecraftDirectoryDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isRemoveMinecraftDirectoryDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsRemoveMinecraftDirectoryDialogOpen);
				isRemoveMinecraftDirectoryDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsRemoveMinecraftDirectoryDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsClearLauncherLogsDialogOpen
	{
		get
		{
			return isClearLauncherLogsDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isClearLauncherLogsDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsClearLauncherLogsDialogOpen);
				isClearLauncherLogsDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsClearLauncherLogsDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsMinecraftDirectoryNameDialogOpen
	{
		get
		{
			return isMinecraftDirectoryNameDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isMinecraftDirectoryNameDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsMinecraftDirectoryNameDialogOpen);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MinecraftDirectoryNameDialogTitle);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MinecraftDirectoryNameDialogConfirmButtonText);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CanConfirmMinecraftDirectoryName);
				isMinecraftDirectoryNameDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsMinecraftDirectoryNameDialogOpen);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MinecraftDirectoryNameDialogTitle);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MinecraftDirectoryNameDialogConfirmButtonText);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CanConfirmMinecraftDirectoryName);
				ConfirmMinecraftDirectoryNameCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string MinecraftDirectoryName
	{
		get
		{
			return minecraftDirectoryName;
		}
		[MemberNotNull("minecraftDirectoryName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(minecraftDirectoryName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MinecraftDirectoryName);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsMinecraftDirectoryNameInvalid);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CanConfirmMinecraftDirectoryName);
				minecraftDirectoryName = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MinecraftDirectoryName);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsMinecraftDirectoryNameInvalid);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CanConfirmMinecraftDirectoryName);
				ConfirmMinecraftDirectoryNameCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsMinecraftDirectoryNameDialogBusy
	{
		get
		{
			return isMinecraftDirectoryNameDialogBusy;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isMinecraftDirectoryNameDialogBusy, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsMinecraftDirectoryNameDialogBusy);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CanConfirmMinecraftDirectoryName);
				isMinecraftDirectoryNameDialogBusy = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsMinecraftDirectoryNameDialogBusy);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CanConfirmMinecraftDirectoryName);
				ConfirmMinecraftDirectoryNameCommand.NotifyCanExecuteChanged();
				RequestRenameMinecraftDirectoryCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsMinecraftDirectoryItem? MinecraftDirectoryPendingRemoval
	{
		get
		{
			return minecraftDirectoryPendingRemoval;
		}
		set
		{
			if (!EqualityComparer<SettingsMinecraftDirectoryItem>.Default.Equals(minecraftDirectoryPendingRemoval, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.MinecraftDirectoryPendingRemoval);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RemoveMinecraftDirectoryDialogMessage);
				minecraftDirectoryPendingRemoval = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.MinecraftDirectoryPendingRemoval);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RemoveMinecraftDirectoryDialogMessage);
				ConfirmRemoveMinecraftDirectoryCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<SettingsMinecraftDirectoryItem?> OpenMinecraftDirectoryCommand => openMinecraftDirectoryCommand ?? (openMinecraftDirectoryCommand = new RelayCommand<SettingsMinecraftDirectoryItem>(OpenMinecraftDirectory));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<SettingsMinecraftDirectoryItem?> RequestRenameMinecraftDirectoryCommand => requestRenameMinecraftDirectoryCommand ?? (requestRenameMinecraftDirectoryCommand = new RelayCommand<SettingsMinecraftDirectoryItem>(RequestRenameMinecraftDirectory, CanRequestRenameMinecraftDirectory));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelMinecraftDirectoryNameCommand => cancelMinecraftDirectoryNameCommand ?? (cancelMinecraftDirectoryNameCommand = new RelayCommand(CancelMinecraftDirectoryName));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmMinecraftDirectoryNameCommand => confirmMinecraftDirectoryNameCommand ?? (confirmMinecraftDirectoryNameCommand = new AsyncRelayCommand(ConfirmMinecraftDirectoryNameAsync, () => CanConfirmMinecraftDirectoryName));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<SettingsMinecraftDirectoryItem?> RequestRemoveMinecraftDirectoryCommand => requestRemoveMinecraftDirectoryCommand ?? (requestRemoveMinecraftDirectoryCommand = new RelayCommand<SettingsMinecraftDirectoryItem>(RequestRemoveMinecraftDirectory, CanRequestRemoveMinecraftDirectory));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelRemoveMinecraftDirectoryCommand => cancelRemoveMinecraftDirectoryCommand ?? (cancelRemoveMinecraftDirectoryCommand = new RelayCommand(CancelRemoveMinecraftDirectory));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmRemoveMinecraftDirectoryCommand => confirmRemoveMinecraftDirectoryCommand ?? (confirmRemoveMinecraftDirectoryCommand = new AsyncRelayCommand(ConfirmRemoveMinecraftDirectoryAsync, () => CanConfirmRemoveMinecraftDirectory));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenLauncherLogDirectoryCommand => openLauncherLogDirectoryCommand ?? (openLauncherLogDirectoryCommand = new RelayCommand(OpenLauncherLogDirectory));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestClearLauncherLogsCommand => requestClearLauncherLogsCommand ?? (requestClearLauncherLogsCommand = new RelayCommand(RequestClearLauncherLogs));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelClearLauncherLogsCommand => cancelClearLauncherLogsCommand ?? (cancelClearLauncherLogsCommand = new RelayCommand(CancelClearLauncherLogs));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmClearLauncherLogsCommand => confirmClearLauncherLogsCommand ?? (confirmClearLauncherLogsCommand = new AsyncRelayCommand(ConfirmClearLauncherLogsAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand AddMinecraftDirectoryCommand => addMinecraftDirectoryCommand ?? (addMinecraftDirectoryCommand = new AsyncRelayCommand(AddMinecraftDirectoryAsync, () => CanAddMinecraftDirectory));

	public event EventHandler<SettingsMinecraftDirectoryChangedEventArgs>? MinecraftDirectoryChanged;

	internal GeneralSettingsViewModel(SettingsPersistenceCoordinator persistence, IStatusService statusService, IFilePickerService filePickerService, IInstanceFolderService instanceFolderService, IMinecraftDirectoryFileSystem minecraftDirectoryFileSystem, MinecraftDirectoryManagementService minecraftDirectoryManagementService, DownloadTasksPageViewModel? downloadTasksPage, ILauncherLogLevelController? logLevelController, ILogger logger)
		: base(persistence)
	{
		this.statusService = statusService;
		this.filePickerService = filePickerService;
		this.instanceFolderService = instanceFolderService;
		this.minecraftDirectoryFileSystem = minecraftDirectoryFileSystem;
		this.minecraftDirectoryManagementService = minecraftDirectoryManagementService;
		this.downloadTasksPage = downloadTasksPage;
		this.logLevelController = logLevelController;
		this.logger = logger;
		// StartRide：对话框改成"切换 BeamNG 游戏目录"用。当前值/确定动作都走分流：
		// BeamNG 模式下读写的 AppSettings.GameDirectory，不再是 .minecraft 目录。
		MinecraftDirectorySwitchDialog = new MinecraftDirectorySwitchDialogViewModel(MinecraftDirectories, ResolveSwitchDialogCurrentDirectory, () => CanChangeMinecraftDirectory, () => IsMinecraftDirectoryChangeBlocked, ApplySwitchDialogDirectoryAsync);
		if (downloadTasksPage != null)
		{
			downloadTasksPage.ActivityChanged += DownloadTasksPage_ActivityChanged;
		}
	}

	public void SetGameLaunchInProgress(bool value)
	{
		if (isGameLaunchInProgress != value)
		{
			isGameLaunchInProgress = value;
			NotifyMinecraftDirectoryCommandStateChanged();
		}
	}

	public async Task RefreshMinecraftDirectoryAvailabilityAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		int generation = Interlocked.Increment(ref minecraftDirectoryAvailabilityGeneration);
		string[] directoryPaths = MinecraftDirectories.Select((SettingsMinecraftDirectoryItem item) => item.DirectoryPath).ToArray();
		if (directoryPaths.Length == 0)
		{
			return;
		}
		Dictionary<string, bool> dictionary = await Task.Run(delegate
		{
			Dictionary<string, bool> dictionary2 = new Dictionary<string, bool>(MinecraftDirectoryPath.Comparer);
			string[] array = directoryPaths;
			foreach (string text in array)
			{
				dictionary2[text] = minecraftDirectoryFileSystem.DirectoryIsAccessible(text);
			}
			return dictionary2;
		}, cancellationToken).ConfigureAwait(continueOnCapturedContext: true);
		if (Volatile.Read(in minecraftDirectoryAvailabilityGeneration) != generation)
		{
			return;
		}
		foreach (SettingsMinecraftDirectoryItem minecraftDirectory in MinecraftDirectories)
		{
			if (dictionary.TryGetValue(minecraftDirectory.DirectoryPath, out var value))
			{
				minecraftDirectory.SetAvailability(value);
			}
		}
	}

	private async Task<bool> IsMinecraftDirectoryAccessibleAsync(string directoryPath)
	{
		return await Task.Run(() => minecraftDirectoryFileSystem.DirectoryIsAccessible(directoryPath)).ConfigureAwait(continueOnCapturedContext: true);
	}

	private void BeginRefreshMinecraftDirectoryAvailability()
	{
		ObserveMinecraftDirectoryAvailabilityRefreshAsync();
	}

	private async Task ObserveMinecraftDirectoryAvailabilityRefreshAsync()
	{
		try
		{
			await RefreshMinecraftDirectoryAvailabilityAsync();
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to refresh Minecraft directory availability.");
		}
	}

	public void Load(LauncherSettings settings)
	{
		LoadState(delegate
		{
			LoadMinecraftDirectories(settings);
			LauncherLogDirectory = LauncherLogConfiguration.ResolveLogDirectory();
			DiagnosticLoggingEnabled = settings.EnableDiagnosticLogging;
			InitializeBeamNgSection();
			// StartRide：运行状态 / 游玩统计 / 诊断包（见 GeneralSettingsViewModel.StartRide.Runtime.cs）
			InitializeRuntimeSection();
		});

		// StartRide：启动行为 / 游戏日志 / 配置备份 / 磁盘占用 / 联机延迟
		// （见 GeneralSettingsViewModel.StartRide.Launch.cs）
		// 放在 LoadState 之外：里面有日志扫描、备份列表、磁盘占用这类后台任务，
		// 塞进 LoadState 会让 CanPersist 长时间为 false，用户在这期间的改动会被丢掉。
		InitializeStartRideLaunchSection();
	}

	public void OpenMinecraftDirectorySwitchDialog()
	{
		LoadState(delegate
		{
			LoadMinecraftDirectories(base.Settings);
		});
		// StartRide：先把列表换成 BeamNG 安装目录候选，再打开（否则会列出 .minecraft）
		PrepareBeamNgDirectorySwitchDialog();
		MinecraftDirectorySwitchDialog.Open();
	}

	[RelayCommand]
	private void OpenMinecraftDirectory(SettingsMinecraftDirectoryItem? item)
	{
		try
		{
			if (item != null && item.IsAvailable && instanceFolderService.TryOpen(item.DirectoryPath))
			{
				return;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to open Minecraft directory.");
		}
		statusService.Report(Strings.Status_OpenMinecraftDirectoryFailed);
	}

	private bool CanRequestRenameMinecraftDirectory(SettingsMinecraftDirectoryItem? item)
	{
		if (!IsMinecraftDirectoryNameDialogBusy)
		{
			return item != null;
		}
		return false;
	}

	[RelayCommand(CanExecute = "CanRequestRenameMinecraftDirectory")]
	private void RequestRenameMinecraftDirectory(SettingsMinecraftDirectoryItem? item)
	{
		if (CanRequestRenameMinecraftDirectory(item))
		{
			OpenMinecraftDirectoryNameDialog(item.DirectoryPath, item.DisplayName, forAdd: false);
		}
	}

	[RelayCommand]
	private void CancelMinecraftDirectoryName()
	{
		if (!IsMinecraftDirectoryNameDialogBusy)
		{
			CloseMinecraftDirectoryNameDialog();
		}
	}

	[RelayCommand(CanExecute = "CanConfirmMinecraftDirectoryName")]
	private async Task ConfirmMinecraftDirectoryNameAsync()
	{
		string directoryPath = minecraftDirectoryPendingNamePath;
		if (directoryPath == null || !CanConfirmMinecraftDirectoryName)
		{
			return;
		}
		string displayName = MinecraftDirectoryDisplayName.Normalize(MinecraftDirectoryName);
		IsMinecraftDirectoryNameDialogBusy = true;
		try
		{
			if (isMinecraftDirectoryNameDialogForAdd)
			{
				if (await ChangeMinecraftDirectoryAsync(directoryPath, addDirectory: true, displayName, Strings.Status_MinecraftDirectoryAdded))
				{
					CloseMinecraftDirectoryNameDialog();
				}
				return;
			}
			Dictionary<string, string> previousDisplayNames = CloneMinecraftDirectoryDisplayNames();
			try
			{
				await PersistImmediatelyAsync(delegate(LauncherSettings settings)
				{
					minecraftDirectoryManagementService.RenameDirectory(settings, directoryPath, displayName);
				});
				LoadState(delegate
				{
					LoadMinecraftDirectories(base.Settings);
				});
				logger.LogInformation("Minecraft directory display name changed. MinecraftDirectory={MinecraftDirectory}", directoryPath);
				statusService.Report(Strings.Status_MinecraftDirectoryRenamed);
				CloseMinecraftDirectoryNameDialog();
			}
			catch (Exception exception)
			{
				base.Settings.MinecraftDirectoryDisplayNames = previousDisplayNames;
				LoadState(delegate
				{
					LoadMinecraftDirectories(base.Settings);
				});
				logger.LogError(exception, "Failed to change Minecraft directory display name. MinecraftDirectory={MinecraftDirectory}", directoryPath);
				statusService.Report(Strings.Status_RenameMinecraftDirectoryFailed);
			}
		}
		finally
		{
			IsMinecraftDirectoryNameDialogBusy = false;
		}
	}

	private bool CanRequestRemoveMinecraftDirectory(SettingsMinecraftDirectoryItem? item)
	{
		if (!isChangingMinecraftDirectory)
		{
			return item?.CanRemove ?? false;
		}
		return false;
	}

	[RelayCommand(CanExecute = "CanRequestRemoveMinecraftDirectory")]
	private void RequestRemoveMinecraftDirectory(SettingsMinecraftDirectoryItem? item)
	{
		if (CanRequestRemoveMinecraftDirectory(item))
		{
			MinecraftDirectoryPendingRemoval = item;
			IsRemoveMinecraftDirectoryDialogOpen = true;
		}
	}

	[RelayCommand]
	private void CancelRemoveMinecraftDirectory()
	{
		IsRemoveMinecraftDirectoryDialogOpen = false;
		MinecraftDirectoryPendingRemoval = null;
	}

	[RelayCommand(CanExecute = "CanConfirmRemoveMinecraftDirectory")]
	private async Task ConfirmRemoveMinecraftDirectoryAsync()
	{
		SettingsMinecraftDirectoryItem pendingRemoval = MinecraftDirectoryPendingRemoval;
		if (pendingRemoval == null || !CanConfirmRemoveMinecraftDirectory)
		{
			return;
		}
		IsRemoveMinecraftDirectoryDialogOpen = false;
		MinecraftDirectoryPendingRemoval = null;
		SetMinecraftDirectoryChangeInProgress(value: true);
		List<string> previousDirectories = base.Settings.MinecraftDirectories.ToList();
		Dictionary<string, string> previousDisplayNames = CloneMinecraftDirectoryDisplayNames();
		List<string> previousExcludedDirectories = base.Settings.ExcludedMinecraftDirectories.ToList();
		try
		{
			await PersistImmediatelyAsync(delegate(LauncherSettings settings)
			{
				minecraftDirectoryManagementService.RemoveDirectoryFromList(settings, pendingRemoval.DirectoryPath);
			});
			LoadState(delegate
			{
				LoadMinecraftDirectories(base.Settings);
			});
			logger.LogInformation("Minecraft directory removed from launcher list. MinecraftDirectory={MinecraftDirectory}", pendingRemoval.DirectoryPath);
			statusService.Report(Strings.Status_MinecraftDirectoryRemovedFromList);
		}
		catch (Exception exception)
		{
			base.Settings.MinecraftDirectories = previousDirectories;
			base.Settings.MinecraftDirectoryDisplayNames = previousDisplayNames;
			base.Settings.ExcludedMinecraftDirectories = previousExcludedDirectories;
			LoadState(delegate
			{
				LoadMinecraftDirectories(base.Settings);
			});
			logger.LogError(exception, "Failed to remove Minecraft directory from launcher list. MinecraftDirectory={MinecraftDirectory}", pendingRemoval.DirectoryPath);
			statusService.Report(Strings.Status_RemoveMinecraftDirectoryFromListFailed);
		}
		finally
		{
			SetMinecraftDirectoryChangeInProgress(value: false);
		}
	}

	[RelayCommand]
	private void OpenLauncherLogDirectory()
	{
		string directory = LauncherLogConfiguration.ResolveLogDirectory();
		if (TryPrepareAndOpenDirectory(directory, Strings.Status_OpenLaunchLogFolderFailed))
		{
			LauncherLogDirectory = directory;
		}
	}

	[RelayCommand]
	private void RequestClearLauncherLogs()
	{
		IsClearLauncherLogsDialogOpen = true;
	}

	[RelayCommand]
	private void CancelClearLauncherLogs()
	{
		IsClearLauncherLogsDialogOpen = false;
	}

	[RelayCommand]
	private async Task ConfirmClearLauncherLogsAsync()
	{
		IsClearLauncherLogsDialogOpen = false;
		string directory = LauncherLogConfiguration.ResolveLogDirectory();
		LauncherLogCleanupResult launcherLogCleanupResult;
		try
		{
			launcherLogCleanupResult = await Task.Run(() => LauncherLogConfiguration.ClearLogFiles(directory)).ConfigureAwait(continueOnCapturedContext: true);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to clear launcher log files. LogDirectory={LogDirectory}", directory);
			statusService.Report(Strings.Status_ClearLauncherLogsFailed);
			return;
		}
		logger.LogInformation("Launcher log files cleared. LogDirectory={LogDirectory} Deleted={Deleted} Retained={Retained}", directory, launcherLogCleanupResult.DeletedFileCount, launcherLogCleanupResult.RetainedFileCount);
		statusService.Report((launcherLogCleanupResult.DeletedFileCount == 0) ? Strings.Status_NoLauncherLogsToClear : string.Format(Strings.Status_LauncherLogsClearedFormat, launcherLogCleanupResult.DeletedFileCount));
	}

	[RelayCommand(CanExecute = "CanAddMinecraftDirectory")]
	private async Task AddMinecraftDirectoryAsync()
	{
		if (isChangingMinecraftDirectory)
		{
			return;
		}
		if (!CanAddMinecraftDirectory)
		{
			statusService.Report(Strings.Settings_MinecraftDirectoryChangeBlockedByActiveTasks);
			return;
		}
		string selectedDirectory = filePickerService.PickFolder(Strings.FilePicker_MinecraftDirectoryTitle, MinecraftDirectory);
		if (string.IsNullOrWhiteSpace(selectedDirectory))
		{
			return;
		}
		if (!(await IsMinecraftDirectoryAccessibleAsync(selectedDirectory)))
		{
			statusService.Report(Strings.Status_AddMinecraftDirectoryFailed);
			return;
		}
		string text;
		try
		{
			text = MinecraftDirectoryPath.Normalize(selectedDirectory);
		}
		catch (Exception ex) when (((ex is ArgumentException || ex is NotSupportedException) ? 1 : 0) != 0)
		{
			logger.LogWarning(ex, "Invalid Minecraft directory selected for registration.");
			statusService.Report(Strings.Status_AddMinecraftDirectoryFailed);
			return;
		}
		if (isChangingMinecraftDirectory)
		{
			return;
		}
		if (!CanAddMinecraftDirectory)
		{
			statusService.Report(Strings.Settings_MinecraftDirectoryChangeBlockedByActiveTasks);
			return;
		}
		bool flag = base.Settings.MinecraftDirectories.Contains<string>(text, MinecraftDirectoryPath.Comparer);
		if (flag && MinecraftDirectoryPath.Equals(text, MinecraftDirectory))
		{
			SetSelectedMinecraftDirectory(FindMinecraftDirectoryItem(MinecraftDirectory));
		}
		else if (!flag)
		{
			OpenMinecraftDirectoryNameDialog(text, MinecraftDirectoryDisplayName.GetDefault(text), forAdd: true);
		}
		else
		{
			await ChangeMinecraftDirectoryAsync(text, addDirectory: false, null, Strings.Status_MinecraftDirectoryChanged);
		}
	}

	public void Dispose()
	{
		if (downloadTasksPage != null)
		{
			downloadTasksPage.ActivityChanged -= DownloadTasksPage_ActivityChanged;
		}
	}

	private Task<bool> SelectMinecraftDirectoryAsync(string directoryPath)
	{
		return ChangeMinecraftDirectoryAsync(directoryPath, addDirectory: false, null, Strings.Status_MinecraftDirectoryChanged);
	}

	private async Task<bool> ChangeMinecraftDirectoryAsync(string directoryPath, bool addDirectory, string? displayName, string successMessage)
	{
		if (!CanChangeMinecraftDirectory)
		{
			statusService.Report(Strings.Settings_MinecraftDirectoryChangeBlockedByActiveTasks);
			return false;
		}
		if (!(await IsMinecraftDirectoryAccessibleAsync(directoryPath)))
		{
			FindMinecraftDirectoryItem(directoryPath)?.SetAvailability(newIsAvailable: false);
			LoadState(delegate
			{
				LoadMinecraftDirectories(base.Settings);
			});
			statusService.Report(addDirectory ? Strings.Status_AddMinecraftDirectoryFailed : Strings.Status_MinecraftDirectoryUnavailable);
			return false;
		}
		SetMinecraftDirectoryChangeInProgress(value: true);
		string previousDirectory = base.Settings.MinecraftDirectory;
		List<string> previousDirectories = base.Settings.MinecraftDirectories.ToList();
		Dictionary<string, string> previousDisplayNames = CloneMinecraftDirectoryDisplayNames();
		List<string> previousExcludedDirectories = base.Settings.ExcludedMinecraftDirectories.ToList();
		try
		{
			await PersistImmediatelyAsync(delegate(LauncherSettings settings)
			{
				if (addDirectory)
				{
					minecraftDirectoryManagementService.AddAndSelectDirectory(settings, directoryPath, displayName);
				}
				else
				{
					minecraftDirectoryManagementService.SelectDirectory(settings, directoryPath);
				}
			});
			bool becameUnavailable = !(await IsMinecraftDirectoryAccessibleAsync(directoryPath));
			if (becameUnavailable)
			{
				FindMinecraftDirectoryItem(directoryPath)?.SetAvailability(newIsAvailable: false);
			}
			if (HasMinecraftDirectoryBlockingActivity | becameUnavailable)
			{
				await PersistImmediatelyAsync(delegate(LauncherSettings settings)
				{
					settings.MinecraftDirectory = previousDirectory;
					settings.MinecraftDirectories = previousDirectories.ToList();
					settings.MinecraftDirectoryDisplayNames = new Dictionary<string, string>(previousDisplayNames, MinecraftDirectoryPath.Comparer);
					settings.ExcludedMinecraftDirectories = previousExcludedDirectories.ToList();
				});
				LoadState(delegate
				{
					LoadMinecraftDirectories(base.Settings);
				});
				statusService.Report((!becameUnavailable) ? Strings.Settings_MinecraftDirectoryChangeBlockedByActiveTasks : (addDirectory ? Strings.Status_AddMinecraftDirectoryFailed : Strings.Status_MinecraftDirectoryUnavailable));
				return false;
			}
			LoadState(delegate
			{
				LoadMinecraftDirectories(base.Settings);
			});
		}
		catch (Exception exception)
		{
			base.Settings.MinecraftDirectory = previousDirectory;
			base.Settings.MinecraftDirectories = previousDirectories;
			base.Settings.MinecraftDirectoryDisplayNames = previousDisplayNames;
			base.Settings.ExcludedMinecraftDirectories = previousExcludedDirectories;
			LoadState(delegate
			{
				LoadMinecraftDirectories(base.Settings);
			});
			logger.LogError(exception, addDirectory ? "Failed to add and select Minecraft directory." : "Failed to save selected Minecraft directory.");
			statusService.Report(addDirectory ? Strings.Status_AddMinecraftDirectoryFailed : Strings.Status_MinecraftDirectorySwitchFailed);
			return false;
		}
		finally
		{
			SetMinecraftDirectoryChangeInProgress(value: false);
		}
		statusService.Report(successMessage);
		MinecraftDirectoryChanged?.Invoke(this, new SettingsMinecraftDirectoryChangedEventArgs(base.Settings.MinecraftDirectory));
		return true;
	}

	private void DownloadTasksPage_ActivityChanged(object? sender, EventArgs e)
	{
		NotifyMinecraftDirectoryCommandStateChanged();
	}

	private void SetMinecraftDirectoryChangeInProgress(bool value)
	{
		if (isChangingMinecraftDirectory != value)
		{
			isChangingMinecraftDirectory = value;
			NotifyMinecraftDirectoryCommandStateChanged();
		}
	}

	private void NotifyMinecraftDirectoryCommandStateChanged()
	{
		OnPropertyChanged("CanChangeMinecraftDirectory");
		OnPropertyChanged("CanAddMinecraftDirectory");
		OnPropertyChanged("IsMinecraftDirectoryChangeBlocked");
		AddMinecraftDirectoryCommand.NotifyCanExecuteChanged();
		RequestRemoveMinecraftDirectoryCommand.NotifyCanExecuteChanged();
		ConfirmRemoveMinecraftDirectoryCommand.NotifyCanExecuteChanged();
		ConfirmMinecraftDirectoryNameCommand.NotifyCanExecuteChanged();
		MinecraftDirectorySwitchDialog.NotifyDirectoryChangeStateChanged();
	}

	private void LoadMinecraftDirectories(LauncherSettings settings)
	{
		MinecraftDirectory = settings.MinecraftDirectory;
		for (int i = 0; i < settings.MinecraftDirectories.Count; i++)
		{
			string text = settings.MinecraftDirectories[i];
			int num = FindMinecraftDirectoryItemIndex(text, i);
			if (num >= 0)
			{
				if (num != i)
				{
					MinecraftDirectories.Move(num, i);
				}
				SettingsMinecraftDirectoryItem settingsMinecraftDirectoryItem = MinecraftDirectories[i];
				settingsMinecraftDirectoryItem.Update(GetMinecraftDirectoryDisplayName(settings, text), settingsMinecraftDirectoryItem.IsAvailable, !MinecraftDirectoryPath.Equals(text, settings.MinecraftDirectory));
			}
			else
			{
				SettingsMinecraftDirectoryItem settingsMinecraftDirectoryItem = new SettingsMinecraftDirectoryItem(GetMinecraftDirectoryDisplayName(settings, text), text, isAvailable: true, !MinecraftDirectoryPath.Equals(text, settings.MinecraftDirectory));
				MinecraftDirectories.Insert(i, settingsMinecraftDirectoryItem);
			}
		}
		while (MinecraftDirectories.Count > settings.MinecraftDirectories.Count)
		{
			MinecraftDirectories.RemoveAt(MinecraftDirectories.Count - 1);
		}
		SetSelectedMinecraftDirectory(FindMinecraftDirectoryItem(settings.MinecraftDirectory));
		MinecraftDirectorySwitchDialog.SynchronizeWithCurrentDirectory();
		BeginRefreshMinecraftDirectoryAvailability();
	}

	private int FindMinecraftDirectoryItemIndex(string directoryPath, int startIndex)
	{
		for (int i = startIndex; i < MinecraftDirectories.Count; i++)
		{
			if (MinecraftDirectoryPath.Equals(MinecraftDirectories[i].DirectoryPath, directoryPath))
			{
				return i;
			}
		}
		return -1;
	}

	private SettingsMinecraftDirectoryItem? FindMinecraftDirectoryItem(string directoryPath)
	{
		return MinecraftDirectories.FirstOrDefault((SettingsMinecraftDirectoryItem item) => MinecraftDirectoryPath.Equals(item.DirectoryPath, directoryPath));
	}

	private void SetSelectedMinecraftDirectory(SettingsMinecraftDirectoryItem? item)
	{
		suppressMinecraftDirectorySelectionChanged = true;
		try
		{
			SelectedMinecraftDirectory = item;
		}
		finally
		{
			suppressMinecraftDirectorySelectionChanged = false;
		}
	}

	private void OpenMinecraftDirectoryNameDialog(string directoryPath, string displayName, bool forAdd)
	{
		minecraftDirectoryPendingNamePath = directoryPath;
		isMinecraftDirectoryNameDialogForAdd = forAdd;
		MinecraftDirectoryName = displayName;
		OnPropertyChanged("MinecraftDirectoryNameDialogTitle");
		OnPropertyChanged("MinecraftDirectoryNameDialogConfirmButtonText");
		OnPropertyChanged("CanConfirmMinecraftDirectoryName");
		IsMinecraftDirectoryNameDialogOpen = true;
	}

	private void CloseMinecraftDirectoryNameDialog()
	{
		IsMinecraftDirectoryNameDialogOpen = false;
		minecraftDirectoryPendingNamePath = null;
		isMinecraftDirectoryNameDialogForAdd = false;
		MinecraftDirectoryName = string.Empty;
		OnPropertyChanged("MinecraftDirectoryNameDialogTitle");
		OnPropertyChanged("MinecraftDirectoryNameDialogConfirmButtonText");
		OnPropertyChanged("CanConfirmMinecraftDirectoryName");
	}

	private Dictionary<string, string> CloneMinecraftDirectoryDisplayNames()
	{
		return new Dictionary<string, string>(base.Settings.MinecraftDirectoryDisplayNames, MinecraftDirectoryPath.Comparer);
	}

	private static string GetMinecraftDirectoryDisplayName(LauncherSettings settings, string directoryPath)
	{
		foreach (KeyValuePair<string, string> minecraftDirectoryDisplayName in settings.MinecraftDirectoryDisplayNames)
		{
			if (MinecraftDirectoryPath.Equals(minecraftDirectoryDisplayName.Key, directoryPath))
			{
				return MinecraftDirectoryDisplayName.NormalizeOrDefault(minecraftDirectoryDisplayName.Value, directoryPath);
			}
		}
		return MinecraftDirectoryDisplayName.GetDefault(directoryPath);
	}

	private bool TryPrepareAndOpenDirectory(string directory, string failureMessage)
	{
		try
		{
			string folderPath = instanceFolderService.EnsureDirectoryExists(directory);
			if (instanceFolderService.TryOpen(folderPath))
			{
				return true;
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to open launcher directory.");
		}
		statusService.Report(failureMessage);
		return false;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDiagnosticLoggingEnabledChanged(bool value)
	{
		if (base.CanPersist)
		{
			logLevelController?.SetDiagnosticLoggingEnabled(value);
			Persist(delegate(LauncherSettings settings)
			{
				settings.EnableDiagnosticLogging = value;
			});
			logger.LogInformation("Diagnostic logging changed. Enabled={Enabled}", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedMinecraftDirectoryChanged(SettingsMinecraftDirectoryItem? value)
	{
		if (suppressMinecraftDirectorySelectionChanged || value == null || !base.CanPersist)
		{
			return;
		}
		SettingsMinecraftDirectoryItem settingsMinecraftDirectoryItem = FindMinecraftDirectoryItem(MinecraftDirectory);
		if (MinecraftDirectoryPath.Equals(value.DirectoryPath, MinecraftDirectory))
		{
			return;
		}
		if (!CanChangeMinecraftDirectory)
		{
			SetSelectedMinecraftDirectory(settingsMinecraftDirectoryItem);
			statusService.Report(Strings.Settings_MinecraftDirectoryChangeBlockedByActiveTasks);
		}
		else if (!value.IsAvailable)
		{
			LoadState(delegate
			{
				LoadMinecraftDirectories(base.Settings);
			});
			statusService.Report(Strings.Status_MinecraftDirectoryUnavailable);
		}
		else
		{
			PendingMinecraftDirectoryChange = SelectMinecraftDirectoryAsync(value.DirectoryPath);
		}
	}
}
