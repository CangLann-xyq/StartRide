using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Download;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace StartRide.App.ViewModels.Resources;

public sealed class ResourcesProjectInstallViewModel : ObservableObject
{
	private sealed class InstallOperationContext(ResourcesModVersionItemViewModel item, ResourcesModInstallTargetItemViewModel target, ResourcesModProjectItemViewModel? project)
	{
		public ResourcesModVersionItemViewModel Item { get; } = item;

		public ResourcesModInstallTargetItemViewModel Target { get; } = target;

		public ResourcesModProjectItemViewModel? Project { get; } = project;

		public string? ActiveInstallKey { get; set; }

		public ResourceInstallTaskSession? Session { get; set; }
	}

	private readonly ResourcesOnlineProjectPageOptions options;

	private readonly IResourceProjectInstallationService? installationService;

	private readonly ResourcesRequiredDependencyPlanner dependencyPlanner;

	private readonly IFilePickerService? filePickerService;

	private readonly IFloatingMessageService? floatingMessageService;

	private readonly DownloadTasksPageViewModel? downloadTasksPage;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger? logger;

	private readonly Action<string> reportStatus;

	private readonly object installStateLock = new object();

	private readonly SemaphoreSlim dependencyDialogGate = new SemaphoreSlim(1, 1);

	private readonly HashSet<string> activeInstallKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private int activeInstallCount;

	private TaskCompletionSource<RequiredDependenciesDialogChoice>? pendingDependenciesChoice;

	[ObservableProperty]
	private bool isInstalling;

	[ObservableProperty]
	private bool isFileExistsDialogOpen;

	[ObservableProperty]
	private string fileExistsDialogMessage = string.Empty;

	[ObservableProperty]
	private bool isRequiredDependenciesDialogOpen;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? closeFileExistsDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelRequiredDependenciesDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? continueWithoutRequiredDependenciesCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? autoInstallRequiredDependenciesCommand;

	public ObservableCollection<ResourcesModDependencyRequirementItemViewModel> RequiredDependencyDialogItems { get; } = new ObservableCollection<ResourcesModDependencyRequirementItemViewModel>();

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsInstalling
	{
		get
		{
			return isInstalling;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isInstalling, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsInstalling);
				isInstalling = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsInstalling);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsFileExistsDialogOpen
	{
		get
		{
			return isFileExistsDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isFileExistsDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsFileExistsDialogOpen);
				isFileExistsDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsFileExistsDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string FileExistsDialogMessage
	{
		get
		{
			return fileExistsDialogMessage;
		}
		[MemberNotNull("fileExistsDialogMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(fileExistsDialogMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FileExistsDialogMessage);
				fileExistsDialogMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FileExistsDialogMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsRequiredDependenciesDialogOpen
	{
		get
		{
			return isRequiredDependenciesDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isRequiredDependenciesDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsRequiredDependenciesDialogOpen);
				isRequiredDependenciesDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsRequiredDependenciesDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CloseFileExistsDialogCommand => closeFileExistsDialogCommand ?? (closeFileExistsDialogCommand = new RelayCommand(CloseFileExistsDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelRequiredDependenciesDialogCommand => cancelRequiredDependenciesDialogCommand ?? (cancelRequiredDependenciesDialogCommand = new RelayCommand(CancelRequiredDependenciesDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ContinueWithoutRequiredDependenciesCommand => continueWithoutRequiredDependenciesCommand ?? (continueWithoutRequiredDependenciesCommand = new RelayCommand(ContinueWithoutRequiredDependencies));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand AutoInstallRequiredDependenciesCommand => autoInstallRequiredDependenciesCommand ?? (autoInstallRequiredDependenciesCommand = new RelayCommand(AutoInstallRequiredDependencies));

	public event EventHandler<GameInstance>? ModpackImported;

	public event EventHandler<ResourcesModpackManualDownloadsRequestedEventArgs>? ModpackManualDownloadsRequested;

	internal ResourcesProjectInstallViewModel(ResourcesOnlineProjectPageOptions options, IResourceProjectInstallationService? installationService, ResourcesRequiredDependencyPlanner dependencyPlanner, IFilePickerService? filePickerService, IFloatingMessageService? floatingMessageService, DownloadTasksPageViewModel? downloadTasksPage, IUiDispatcher uiDispatcher, ILogger? logger, Action<string> reportStatus)
	{
		this.options = options;
		this.installationService = installationService;
		this.dependencyPlanner = dependencyPlanner;
		this.filePickerService = filePickerService;
		this.floatingMessageService = floatingMessageService;
		this.downloadTasksPage = downloadTasksPage;
		this.uiDispatcher = uiDispatcher;
		this.logger = logger;
		this.reportStatus = reportStatus;
	}

	[RelayCommand]
	private void CloseFileExistsDialog()
	{
		IsFileExistsDialogOpen = false;
		FileExistsDialogMessage = string.Empty;
	}

	[RelayCommand]
	private void CancelRequiredDependenciesDialog()
	{
		ResolveDependenciesDialog(RequiredDependenciesDialogChoice.Cancel);
	}

	[RelayCommand]
	private void ContinueWithoutRequiredDependencies()
	{
		ResolveDependenciesDialog(RequiredDependenciesDialogChoice.ContinueWithoutDependencies);
	}

	[RelayCommand]
	private void AutoInstallRequiredDependencies()
	{
		ResolveDependenciesDialog(RequiredDependenciesDialogChoice.AutoInstallDependencies);
	}

	private async Task<bool> InstallDependenciesAsync(InstallOperationContext context, RequiredDependencyInstallPlan dependencyPlan, GameInstance instance)
	{
		try
		{
			context.Session.BeginDependencies(dependencyPlan.MissingDependencies.Count);
			await dependencyPlanner.InstallRequiredDependenciesAsync(dependencyPlan.MissingDependencies, instance, context.Project?.Project.ProjectId, context.Session.Progress, delegate(LauncherProgress progress)
			{
				context.Session.ReportDependencyStarted(progress);
			}, context.Session.CancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			context.Session.CompleteDependencies();
			return true;
		}
		catch (ResourceDependencyInstallException ex)
		{
			string message = string.Format(Strings.Status_ModRequiredDependenciesAutoInstallFailedFormat, ex.DependencyTitle);
			PresentFailure(context, message);
			logger?.LogWarning(ex, "Failed to auto-install required resource dependency. ProjectId={ProjectId} DependencyProjectId={DependencyProjectId} InstanceId={InstanceId}", context.Project?.Project.ProjectId, ex.DependencyProjectId, instance.Id);
			return false;
		}
	}

	private void ShowFileExists(ResourcesModVersionItemViewModel item)
	{
		uiDispatcher.Invoke(delegate
		{
			string arg = (string.IsNullOrWhiteSpace(item.Version.FileName) ? item.Title : item.Version.FileName);
			FileExistsDialogMessage = string.Format(options.FileExistsMessageFormat, arg);
			IsFileExistsDialogOpen = true;
		});
	}

	private void ShowServerDirectoryExists(string directory)
	{
		uiDispatcher.Invoke(delegate
		{
			FileExistsDialogMessage = string.Format(Strings.Status_ServerDirectoryExistsFormat, Path.GetFileName(directory));
			IsFileExistsDialogOpen = true;
		});
	}

	private async Task<RequiredDependenciesDialogChoice> RequestDependenciesDialogAsync(IReadOnlyList<ResourcesModDependencyRequirementItemViewModel> items, CancellationToken cancellationToken)
	{
		await dependencyDialogGate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		TaskCompletionSource<RequiredDependenciesDialogChoice> completion = new TaskCompletionSource<RequiredDependenciesDialogChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
		try
		{
			uiDispatcher.Invoke(delegate
			{
				pendingDependenciesChoice = completion;
				RequiredDependencyDialogItems.Clear();
				foreach (ResourcesModDependencyRequirementItemViewModel item in items)
				{
					RequiredDependencyDialogItems.Add(item);
				}
				IsRequiredDependenciesDialogOpen = true;
			});
			return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			uiDispatcher.Invoke(delegate
			{
				if (pendingDependenciesChoice == completion)
				{
					pendingDependenciesChoice = null;
					IsRequiredDependenciesDialogOpen = false;
					RequiredDependencyDialogItems.Clear();
				}
			});
			dependencyDialogGate.Release();
		}
	}

	private void ResolveDependenciesDialog(RequiredDependenciesDialogChoice choice)
	{
		TaskCompletionSource<RequiredDependenciesDialogChoice> completion = null;
		uiDispatcher.Invoke(delegate
		{
			IsRequiredDependenciesDialogOpen = false;
			completion = pendingDependenciesChoice;
		});
		completion?.TrySetResult(choice);
	}

	private void CompleteModpackImport(InstallOperationContext context, ModpackImportResult result)
	{
		GameInstance instance = result.ImportedInstance;
		string text = (result.HasManualDownloads ? string.Format(Strings.Status_ModpackImportedWithManualDownloadsFormat, instance.Name) : string.Format(options.InstalledFormat, instance.Name));
		context.Session?.Complete(text);
		reportStatus(text);
		uiDispatcher.Invoke(delegate
		{
			ModpackImported?.Invoke(this, instance);
			if (result.HasManualDownloads)
			{
				ModpackManualDownloadsRequested?.Invoke(this, new ResourcesModpackManualDownloadsRequestedEventArgs(instance, result.ManualDownloads));
			}
		});
		logger?.LogInformation("Resource modpack imported as new instance. ProjectId={ProjectId} VersionId={VersionId} InstanceId={InstanceId} HasManualDownloads={HasManualDownloads}", context.Project?.Project.ProjectId, context.Item.Version.VersionId, instance.Id, result.HasManualDownloads);
	}

	private void BeginUserFeedback(ResourcesModVersionItemViewModel item)
	{
		floatingMessageService?.Show(options.DownloadingText);
		string obj = string.Format(options.DownloadingFormat, item.Title);
		reportStatus(obj);
	}

	private void BeginSession(InstallOperationContext context, string subtitle)
	{
		context.Session = ResourceInstallTaskSession.Begin(downloadTasksPage, context.Item.Title, subtitle, options.DownloadingText);
	}

	private void PresentFailure(InstallOperationContext context, string message)
	{
		floatingMessageService?.Show(message);
		reportStatus(message);
		context.Session?.Fail(message);
	}

	private string MapModpackImportFailureMessage(ModpackImportFailureReason failureReason)
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
			return options.InstallFailedText;
		}
	}

	private static string ResolveFileName(ResourcesModVersionItemViewModel item)
	{
		if (!string.IsNullOrWhiteSpace(item.Version.FileName))
		{
			return item.Version.FileName;
		}
		return item.Title;
	}

	internal async Task InstallAsync(ResourcesModVersionItemViewModel? item, ResourcesModInstallTargetItemViewModel? target, ResourcesModProjectItemViewModel? selectedProject)
	{
		if (item == null || target == null || installationService == null)
		{
			return;
		}
		InstallOperationContext context = new InstallOperationContext(item, target, selectedProject);
		try
		{
			if (target.IsLocalDownload)
			{
				await DownloadToDirectoryAsync(context).ConfigureAwait(continueOnCapturedContext: false);
			}
			else if (target.IsNewInstanceInstall)
			{
				await InstallModpackAsNewInstanceAsync(context).ConfigureAwait(continueOnCapturedContext: false);
			}
			else if (target.IsServerInstall)
			{
				await InstallModpackAsServerAsync(context).ConfigureAwait(continueOnCapturedContext: false);
			}
			else
			{
				await InstallIntoExistingInstanceAsync(context).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (OperationCanceledException) when (context.Session?.IsCancellationRequested ?? false)
		{
			context.Session.CompleteCancellation();
			logger?.LogInformation("Resource project installation canceled. ProjectId={ProjectId} VersionId={VersionId}", selectedProject?.Project.ProjectId, item.Version.VersionId);
		}
		catch (ResourceProjectIntegrityException ex2)
		{
			PresentFailure(context, Strings.Status_ResourceProjectIntegrityFailed);
			logger?.LogWarning(ex2, "Resource project integrity verification prevented installation. Kind={Kind} ProjectId={ProjectId} VersionId={VersionId} Reason={Reason} Algorithm={Algorithm}", options.Kind, selectedProject?.Project.ProjectId, item.Version.VersionId, ex2.Reason, ex2.Algorithm);
		}
		catch (ResourceProjectDestinationConflictException ex3)
		{
			ResourceProjectDestinationConflictReason reason = ex3.Reason;
			bool flag = (uint)(reason - 2) <= 1u;
			string message = (flag ? Strings.Status_ResourceProjectInstanceDestinationInvalid : string.Format(Strings.Status_ResourceProjectDestinationConflictFormat, Path.GetFileName(ex3.DestinationPath)));
			PresentFailure(context, message);
			logger?.LogWarning(ex3, "Resource project destination changed or conflicts with existing content. Kind={Kind} ProjectId={ProjectId} VersionId={VersionId} Reason={Reason} FileName={FileName}", options.Kind, selectedProject?.Project.ProjectId, item.Version.VersionId, ex3.Reason, Path.GetFileName(ex3.DestinationPath));
		}
		catch (ServerDeploymentDirectoryExistsException ex4)
		{
			ShowServerDirectoryExists(ex4.Directory);
			logger?.LogInformation("Server modpack deployment target already exists. ProjectId={ProjectId} VersionId={VersionId}", selectedProject?.Project.ProjectId, item.Version.VersionId);
		}
		catch (ResourceProjectDistributionRestrictedException exception)
		{
			PresentFailure(context, Strings.Status_ServerDistributionRestricted);
			logger?.LogWarning(exception, "Resource server pack distribution is restricted. ProjectId={ProjectId} VersionId={VersionId}", selectedProject?.Project.ProjectId, item.Version.VersionId);
		}
		catch (Exception exception2)
		{
			string message2 = (target.IsServerInstall ? Strings.Status_ServerDeployFailed : (target.IsLocalDownload ? options.DownloadFailedText : options.InstallFailedText));
			PresentFailure(context, message2);
			logger?.LogError(exception2, "Resource project installation failed. Kind={Kind} ProjectId={ProjectId} VersionId={VersionId} InstanceId={InstanceId}", options.Kind, selectedProject?.Project.ProjectId, item.Version.VersionId, target.Instance?.Id);
		}
		finally
		{
			EndInstall(context);
		}
	}

	private bool TryBeginInstall(InstallOperationContext context, string targetKind, string targetIdentity)
	{
		ResourceProjectVersion version = context.Item.Version;
		ResourceProject resourceProject = context.Project?.Project;
		string text = (string.IsNullOrWhiteSpace(version.VersionId) ? (version.FileName + "\u001f" + version.PrimaryDownloadUrl) : version.VersionId);
		string text2 = string.Join('\u001f', options.Kind, resourceProject?.Source, resourceProject?.ProjectId, text, targetKind, targetIdentity);
		lock (installStateLock)
		{
			if (!activeInstallKeys.Add(text2))
			{
				return false;
			}
			context.ActiveInstallKey = text2;
			activeInstallCount++;
			IsInstalling = true;
			return true;
		}
	}

	private void EndInstall(InstallOperationContext context)
	{
		string activeInstallKey = context.ActiveInstallKey;
		if (activeInstallKey == null)
		{
			return;
		}
		lock (installStateLock)
		{
			if (activeInstallKeys.Remove(activeInstallKey))
			{
				context.ActiveInstallKey = null;
				activeInstallCount = Math.Max(0, activeInstallCount - 1);
				IsInstalling = activeInstallCount > 0;
			}
		}
	}

	private void ReportDuplicateInstall(InstallOperationContext context)
	{
		floatingMessageService?.Show(Strings.Status_ResourceProjectDownloadAlreadyRunning);
		reportStatus(Strings.Status_ResourceProjectDownloadAlreadyRunning);
		logger?.LogInformation("Duplicate resource project installation request ignored. Kind={Kind} ProjectId={ProjectId} VersionId={VersionId} InstanceId={InstanceId}", options.Kind, context.Project?.Project.ProjectId, context.Item.Version.VersionId, context.Target.Instance?.Id);
	}

	private static string NormalizeTargetPath(string path)
	{
		try
		{
			return Path.GetFullPath(path);
		}
		catch (Exception ex) when (((ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException) ? 1 : 0) != 0)
		{
			return path.Trim();
		}
	}

	private async Task DownloadToDirectoryAsync(InstallOperationContext context)
	{
		string text = filePickerService?.PickResourceProjectDestination(options.DownloadDirectoryPickerTitle, ResolveFileName(context.Item));
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		text = NormalizeTargetPath(text);
		string directoryName = Path.GetDirectoryName(text);
		if (string.IsNullOrWhiteSpace(directoryName))
		{
			return;
		}
		if (!TryBeginInstall(context, "LocalDirectory", text))
		{
			ReportDuplicateInstall(context);
			return;
		}
		BeginSession(context, directoryName);
		ResourceProjectInstallationRequest request = new ResourceProjectInstallationRequest(context.Item.Version, ResourceProjectInstallationTargetKind.LocalDirectory, directoryName, null, null, text);
		ResourceProjectInstallationPreparationResult resourceProjectInstallationPreparationResult = await installationService.PrepareAsync(request, context.Session.CancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		request = request with
		{
			ExpectedDestinationState = resourceProjectInstallationPreparationResult.DestinationState
		};
		BeginUserFeedback(context.Item);
		context.Session.BeginPrimaryDownload(hasDependencies: false);
		await installationService.ExecuteAsync(request, context.Session.Progress, context.Session.CancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!context.Session.CompleteCancellation())
		{
			string text2 = string.Format(options.DownloadedFormat, ResolveFileName(context.Item));
			context.Session.Complete(text2);
			reportStatus(text2);
		}
	}

	private async Task InstallModpackAsNewInstanceAsync(InstallOperationContext context)
	{
		if (!TryBeginInstall(context, "NewModpackInstance", string.Empty))
		{
			ReportDuplicateInstall(context);
			return;
		}
		BeginSession(context, context.Target.Title);
		BeginUserFeedback(context.Item);
		context.Session.BeginModpackImport();
		ModpackImportResult modpackImportResult = (await installationService.ExecuteAsync(new ResourceProjectInstallationRequest(context.Item.Version, ResourceProjectInstallationTargetKind.NewModpackInstance, null, null, context.Project?.Project), context.Session.Progress, context.Session.CancellationToken).ConfigureAwait(continueOnCapturedContext: false)).ModpackImportResult ?? ModpackImportResult.Failure(ModpackImportFailureReason.UnexpectedError);
		if (!context.Session.CompleteCancellation())
		{
			if (modpackImportResult.IsSuccess && modpackImportResult.ImportedInstance != null)
			{
				CompleteModpackImport(context, modpackImportResult);
			}
			else
			{
				PresentFailure(context, MapModpackImportFailureMessage(modpackImportResult.FailureReason));
			}
		}
	}

	private async Task InstallModpackAsServerAsync(InstallOperationContext context)
	{
		string parentDirectory = filePickerService?.PickFolder(Strings.FilePicker_ServerInstallDirectoryTitle);
		if (string.IsNullOrWhiteSpace(parentDirectory))
		{
			return;
		}
		if (!TryBeginInstall(context, "NewServerDirectory", NormalizeTargetPath(parentDirectory)))
		{
			ReportDuplicateInstall(context);
			return;
		}
		BeginSession(context, parentDirectory);
		ResourceProjectInstallationRequest request = new ResourceProjectInstallationRequest(context.Item.Version, ResourceProjectInstallationTargetKind.NewServerDirectory, parentDirectory, null, context.Project?.Project);
		ResourceProjectInstallationPreparationResult resourceProjectInstallationPreparationResult = await installationService.PrepareAsync(request, context.Session.CancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (resourceProjectInstallationPreparationResult.TargetExists)
		{
			context.Session.Dismiss();
			ShowServerDirectoryExists(resourceProjectInstallationPreparationResult.TargetPath ?? parentDirectory);
			return;
		}
		floatingMessageService?.Show(Strings.Status_ServerDeploying);
		reportStatus(Strings.Status_ServerDeploying);
		context.Session.BeginModpackImport();
		ResourceProjectInstallationResult resourceProjectInstallationResult = await installationService.ExecuteAsync(request, context.Session.Progress, context.Session.CancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!context.Session.CompleteCancellation())
		{
			string arg = resourceProjectInstallationResult.InstalledPath ?? parentDirectory;
			string text = string.Format(Strings.Status_ServerDeployedFormat, arg);
			context.Session.Complete(text);
			reportStatus(text);
			floatingMessageService?.Show(text);
			logger?.LogInformation("Resource modpack deployed as server. ProjectId={ProjectId} VersionId={VersionId}", context.Project?.Project.ProjectId, context.Item.Version.VersionId);
		}
	}

	private async Task InstallIntoExistingInstanceAsync(InstallOperationContext context)
	{
		GameInstance instance = context.Target.Instance;
		if (instance == null)
		{
			return;
		}
		string text = await installationService.EnsureInstanceContentDirectoryAsync(options.Kind, instance);
		string text2 = null;
		if (options.Kind != ResourceProjectKind.World)
		{
			text2 = filePickerService?.PickResourceProjectDestination(options.DownloadDirectoryPickerTitle, ResolveFileName(context.Item), text);
			if (string.IsNullOrWhiteSpace(text2))
			{
				return;
			}
			text2 = NormalizeTargetPath(text2);
			if (!IsDirectChildPath(text2, text))
			{
				floatingMessageService?.Show(Strings.Status_ResourceProjectInstanceDestinationInvalid);
				reportStatus(Strings.Status_ResourceProjectInstanceDestinationInvalid);
				return;
			}
		}
		string text3 = (string.IsNullOrWhiteSpace(instance.Id) ? NormalizeTargetPath(instance.InstanceDirectory) : instance.Id);
		if (!TryBeginInstall(context, "ExistingInstance", text2 ?? text3))
		{
			ReportDuplicateInstall(context);
			return;
		}
		BeginSession(context, context.Target.Title);
		ResourceProjectInstallationRequest request = new ResourceProjectInstallationRequest(context.Item.Version, ResourceProjectInstallationTargetKind.ExistingInstance, null, instance, null, text2);
		ResourceProjectInstallationPreparationResult resourceProjectInstallationPreparationResult = await installationService.PrepareAsync(request, context.Session.CancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		request = request with
		{
			ExpectedDestinationState = resourceProjectInstallationPreparationResult.DestinationState
		};
		RequiredDependencyInstallPlan dependencyPlan = await dependencyPlanner.ResolveInstallPlanAsync(context.Item, instance, context.Project?.Project.ProjectId, (IReadOnlyList<ResourcesModDependencyRequirementItemViewModel> items) => RequestDependenciesDialogAsync(items, context.Session.CancellationToken), context.Session.CancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (dependencyPlan.Choice == RequiredDependenciesDialogChoice.Cancel)
		{
			context.Session.Dismiss();
			return;
		}
		floatingMessageService?.Show(options.DownloadingText);
		bool flag = dependencyPlan.Choice == RequiredDependenciesDialogChoice.AutoInstallDependencies;
		if (flag)
		{
			flag = !(await InstallDependenciesAsync(context, dependencyPlan, instance).ConfigureAwait(continueOnCapturedContext: false));
		}
		if (!flag)
		{
			BeginUserFeedback(context.Item);
			context.Session.BeginPrimaryDownload(dependencyPlan.Choice == RequiredDependenciesDialogChoice.AutoInstallDependencies && dependencyPlan.MissingDependencies.Count > 0);
			await installationService.ExecuteAsync(request, context.Session.Progress, context.Session.CancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!context.Session.CompleteCancellation())
			{
				string text4 = string.Format(options.InstalledFormat, context.Project?.Title ?? context.Item.Title);
				context.Session.Complete(text4);
				reportStatus(text4);
			}
		}
	}

	private static bool IsDirectChildPath(string path, string expectedDirectory)
	{
		try
		{
			return string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(expectedDirectory), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(Path.GetFileName(path));
		}
		catch (Exception ex) when (((ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException) ? 1 : 0) != 0)
		{
			return false;
		}
	}
}
