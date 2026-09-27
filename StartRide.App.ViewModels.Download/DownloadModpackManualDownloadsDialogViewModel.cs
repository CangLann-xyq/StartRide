using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Download;

public sealed class DownloadModpackManualDownloadsDialogViewModel : ObservableObject
{
	private readonly IInstanceFolderService instanceFolderService;

	private readonly IFloatingMessageService floatingMessageService;

	private string? manualDownloadsFilePath;

	private string? instanceDirectory;

	[ObservableProperty]
	private bool isOpen;

	[ObservableProperty]
	private string title = string.Empty;

	[ObservableProperty]
	private string message = string.Empty;

	[ObservableProperty]
	private string hint = string.Empty;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? closeCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openFileCommand;

	public ObservableCollection<DownloadModpackManualDownloadItemViewModel> Files { get; } = new ObservableCollection<DownloadModpackManualDownloadItemViewModel>();

	public bool CanOpenFile
	{
		get
		{
			if (IsOpen)
			{
				if (string.IsNullOrWhiteSpace(manualDownloadsFilePath))
				{
					return !string.IsNullOrWhiteSpace(instanceDirectory);
				}
				return true;
			}
			return false;
		}
	}

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
				OnIsOpenChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Title
	{
		get
		{
			return title;
		}
		[MemberNotNull("title")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(title, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Title);
				title = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Title);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Message
	{
		get
		{
			return message;
		}
		[MemberNotNull("message")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(message, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Message);
				message = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Message);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Hint
	{
		get
		{
			return hint;
		}
		[MemberNotNull("hint")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(hint, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Hint);
				hint = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Hint);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CloseCommand => closeCommand ?? (closeCommand = new RelayCommand(Close));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenFileCommand => openFileCommand ?? (openFileCommand = new RelayCommand(OpenFile, () => CanOpenFile));

	public DownloadModpackManualDownloadsDialogViewModel(IInstanceFolderService instanceFolderService, IFloatingMessageService floatingMessageService)
	{
		this.instanceFolderService = instanceFolderService;
		this.floatingMessageService = floatingMessageService;
	}

	public void Show(GameInstance instance, IReadOnlyList<ManualModpackDownload> manualDownloads)
	{
		ArgumentNullException.ThrowIfNull(instance, "instance");
		ArgumentNullException.ThrowIfNull(manualDownloads, "manualDownloads");
		instanceDirectory = instance.InstanceDirectory;
		manualDownloadsFilePath = Path.Combine(instance.InstanceDirectory, "manual-downloads.txt");
		Title = Strings.Dialog_ModpackManualDownloadsTitle;
		Message = string.Format(Strings.Dialog_ModpackManualDownloadsMessageFormat, instance.Name, manualDownloads.Count);
		Hint = Strings.Dialog_ModpackManualDownloadsHint;
		Files.Clear();
		foreach (ManualModpackDownload manualDownload in manualDownloads)
		{
			Files.Add(new DownloadModpackManualDownloadItemViewModel(string.IsNullOrWhiteSpace(manualDownload.DisplayName) ? manualDownload.FileName : manualDownload.DisplayName, manualDownload.FileName, manualDownload.FailureSummary));
		}
		IsOpen = true;
		OpenFileCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand]
	private void Close()
	{
		IsOpen = false;
		OpenFileCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand(CanExecute = "CanOpenFile")]
	private void OpenFile()
	{
		if ((string.IsNullOrWhiteSpace(manualDownloadsFilePath) || !instanceFolderService.TryRevealFile(manualDownloadsFilePath)) && (string.IsNullOrWhiteSpace(instanceDirectory) || !instanceFolderService.TryOpen(instanceDirectory)))
		{
			floatingMessageService.Show(Strings.Status_OpenModpackManualDownloadsFileFailed);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsOpenChanged(bool value)
	{
		OpenFileCommand.NotifyCanExecuteChanged();
	}
}
