using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.ViewModels.Settings;

public sealed class CustomFileDownloadViewModel : ObservableObject
{
	private readonly ICustomFileDownloadService downloadService;

	private readonly IFilePickerService filePickerService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly DownloadTasksPageViewModel downloadTasksPage;

	private readonly ILogger<CustomFileDownloadViewModel> logger;

	[ObservableProperty]
	private bool isDialogOpen;

	[ObservableProperty]
	private string address = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor("HasAddressValidationError")]
	private string addressValidationMessage = string.Empty;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? downloadCommand;

	public bool HasAddressValidationError => !string.IsNullOrWhiteSpace(AddressValidationMessage);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsDialogOpen
	{
		get
		{
			return isDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsDialogOpen);
				isDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Address
	{
		get
		{
			return address;
		}
		[MemberNotNull("address")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(address, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Address);
				address = value;
				OnAddressChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Address);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string AddressValidationMessage
	{
		get
		{
			return addressValidationMessage;
		}
		[MemberNotNull("addressValidationMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(addressValidationMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AddressValidationMessage);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasAddressValidationError);
				addressValidationMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AddressValidationMessage);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasAddressValidationError);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenDialogCommand => openDialogCommand ?? (openDialogCommand = new RelayCommand(OpenDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelCommand => cancelCommand ?? (cancelCommand = new RelayCommand(Cancel));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand DownloadCommand => downloadCommand ?? (downloadCommand = new RelayCommand(Download));

	public CustomFileDownloadViewModel(ICustomFileDownloadService downloadService, IFilePickerService filePickerService, IFloatingMessageService floatingMessageService, DownloadTasksPageViewModel downloadTasksPage, ILogger<CustomFileDownloadViewModel>? logger = null)
	{
		this.downloadService = downloadService;
		this.filePickerService = filePickerService;
		this.floatingMessageService = floatingMessageService;
		this.downloadTasksPage = downloadTasksPage;
		this.logger = logger ?? NullLogger<CustomFileDownloadViewModel>.Instance;
	}

	[RelayCommand]
	private void OpenDialog()
	{
		Address = string.Empty;
		AddressValidationMessage = string.Empty;
		IsDialogOpen = true;
	}

	[RelayCommand]
	private void Cancel()
	{
		IsDialogOpen = false;
		Address = string.Empty;
		AddressValidationMessage = string.Empty;
	}

	[RelayCommand]
	private void Download()
	{
		if (!TryNormalizeHttpAddress(Address, out string normalizedAddress, out Uri uri))
		{
			AddressValidationMessage = Strings.Dialog_CustomFileDownloadAddressValidation;
			return;
		}
		string defaultFileName = ResolveDefaultFileName(uri);
		string text = filePickerService.PickCustomDownloadDestination(defaultFileName);
		if (!string.IsNullOrWhiteSpace(text))
		{
			string fullPath = Path.GetFullPath(text);
			string fileName = Path.GetFileName(fullPath);
			string subtitle = Path.GetDirectoryName(fullPath) ?? string.Empty;
			IsDialogOpen = false;
			Address = string.Empty;
			AddressValidationMessage = string.Empty;
			DownloadTaskItem taskItem = downloadTasksPage.BeginTask(fileName, subtitle);
			floatingMessageService.Show(string.Format(Strings.Status_CustomFileDownloadStartedFormat, fileName));
			taskItem.Report(new LauncherProgress("CustomFileDownload", Strings.Status_CustomFileDownloadPreparing, 0.0));
			IProgress<LauncherProgress> progress = taskItem.CreateProgress(delegate(LauncherProgress value)
			{
				taskItem.Report(value with
				{
					Message = Strings.Status_CustomFileDownloading
				});
			});
			Task task = RunDownloadAsync(normalizedAddress, fullPath, fileName, taskItem, progress);
			downloadTasksPage.TrackBackgroundTask(task);
		}
	}

	private async Task RunDownloadAsync(string sourceUrl, string destinationPath, string fileName, DownloadTaskItem taskItem, IProgress<LauncherProgress> progress)
	{
		try
		{
			await downloadService.DownloadAsync(sourceUrl, destinationPath, progress, taskItem.CancellationToken);
			taskItem.CancellationToken.ThrowIfCancellationRequested();
			taskItem.Complete(Strings.Status_CustomFileDownloadCompleted);
		}
		catch (OperationCanceledException) when (taskItem.CancellationToken.IsCancellationRequested)
		{
			logger.LogInformation("Custom file download canceled. FileName={FileName} DestinationPath={DestinationPath}", fileName, destinationPath);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Custom file download task failed. FileName={FileName} DestinationPath={DestinationPath}", fileName, destinationPath);
			taskItem.Fail(Strings.Status_CustomFileDownloadFailed);
		}
	}

	internal static bool TryNormalizeHttpAddress(string? value, out string normalizedAddress, out Uri uri)
	{
		normalizedAddress = string.Empty;
		uri = null;
		if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri result) || string.IsNullOrWhiteSpace(result.Host) || (!result.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !result.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
		{
			return false;
		}
		uri = result;
		normalizedAddress = result.AbsoluteUri;
		return true;
	}

	internal static string ResolveDefaultFileName(Uri sourceUri)
	{
		string source;
		try
		{
			source = Uri.UnescapeDataString(sourceUri.Segments.LastOrDefault()?.TrimEnd('/') ?? string.Empty);
		}
		catch (UriFormatException)
		{
			source = string.Empty;
		}
		HashSet<char> invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
		string text = new string(source.Select((char character) => (!invalidCharacters.Contains(character)) ? character : '_').ToArray()).Trim().TrimEnd('.', ' ');
		bool flag = string.IsNullOrWhiteSpace(text);
		if (!flag)
		{
			bool flag2 = ((text == "." || text == "..") ? true : false);
			flag = flag2;
		}
		if (!flag)
		{
			return text;
		}
		return "download";
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAddressChanged(string value)
	{
		if (HasAddressValidationError)
		{
			AddressValidationMessage = string.Empty;
		}
	}
}
