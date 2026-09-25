using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Services;

namespace StartRide.App.ViewModels.Settings;

public sealed class MinecraftDirectorySwitchDialogViewModel : ObservableObject
{
	private readonly Func<string> getCurrentDirectory;

	private readonly Func<bool> canChangeDirectory;

	private readonly Func<bool> isChangeBlockedByActiveTasks;

	private readonly Func<string, Task<bool>> switchDirectory;

	private bool suppressSelectionChanged;

	private SettingsMinecraftDirectoryItem? acceptedSelection;

	[ObservableProperty]
	[NotifyPropertyChangedFor("CanConfirm")]
	[NotifyPropertyChangedFor("CanCancel")]
	[NotifyCanExecuteChangedFor("ConfirmCommand")]
	[NotifyCanExecuteChangedFor("CancelCommand")]
	private bool isOpen;

	[ObservableProperty]
	[NotifyPropertyChangedFor("CanConfirm")]
	[NotifyCanExecuteChangedFor("ConfirmCommand")]
	private SettingsMinecraftDirectoryItem? selectedDirectory;

	[ObservableProperty]
	[NotifyPropertyChangedFor("CanConfirm")]
	[NotifyPropertyChangedFor("CanCancel")]
	[NotifyCanExecuteChangedFor("ConfirmCommand")]
	[NotifyCanExecuteChangedFor("CancelCommand")]
	private bool isBusy;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? confirmCommand;

	public ObservableCollection<SettingsMinecraftDirectoryItem> Directories { get; }

	public bool IsChangeBlockedByActiveTasks => isChangeBlockedByActiveTasks();

	public bool CanConfirm
	{
		get
		{
			if (IsOpen && !IsBusy && canChangeDirectory())
			{
				SettingsMinecraftDirectoryItem settingsMinecraftDirectoryItem = SelectedDirectory;
				if (settingsMinecraftDirectoryItem != null && settingsMinecraftDirectoryItem.IsAvailable)
				{
					return !MinecraftDirectoryPath.Equals(settingsMinecraftDirectoryItem.DirectoryPath, getCurrentDirectory());
				}
			}
			return false;
		}
	}

	public bool CanCancel
	{
		get
		{
			if (IsOpen)
			{
				return !IsBusy;
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
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CanConfirm);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CanCancel);
				isOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsOpen);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CanConfirm);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CanCancel);
				ConfirmCommand.NotifyCanExecuteChanged();
				CancelCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsMinecraftDirectoryItem? SelectedDirectory
	{
		get
		{
			return selectedDirectory;
		}
		set
		{
			if (!EqualityComparer<SettingsMinecraftDirectoryItem>.Default.Equals(selectedDirectory, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedDirectory);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CanConfirm);
				selectedDirectory = value;
				OnSelectedDirectoryChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedDirectory);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CanConfirm);
				ConfirmCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsBusy
	{
		get
		{
			return isBusy;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isBusy, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsBusy);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CanConfirm);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CanCancel);
				isBusy = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsBusy);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CanConfirm);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CanCancel);
				ConfirmCommand.NotifyCanExecuteChanged();
				CancelCommand.NotifyCanExecuteChanged();
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelCommand => cancelCommand ?? (cancelCommand = new RelayCommand(Cancel, () => CanCancel));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand ConfirmCommand => confirmCommand ?? (confirmCommand = new AsyncRelayCommand(ConfirmAsync, () => CanConfirm));

	internal MinecraftDirectorySwitchDialogViewModel(ObservableCollection<SettingsMinecraftDirectoryItem> directories, Func<string> getCurrentDirectory, Func<bool> canChangeDirectory, Func<bool> isChangeBlockedByActiveTasks, Func<string, Task<bool>> switchDirectory)
	{
		Directories = directories;
		this.getCurrentDirectory = getCurrentDirectory;
		this.canChangeDirectory = canChangeDirectory;
		this.isChangeBlockedByActiveTasks = isChangeBlockedByActiveTasks;
		this.switchDirectory = switchDirectory;
	}

	public void Open()
	{
		IsOpen = true;
		RestoreCurrentSelection();
		NotifyDirectoryChangeStateChanged();
	}

	public void SynchronizeWithCurrentDirectory()
	{
		RestoreCurrentSelection();
		NotifyDirectoryChangeStateChanged();
	}

	public void NotifyDirectoryChangeStateChanged()
	{
		if (IsChangeBlockedByActiveTasks)
		{
			RestoreCurrentSelection();
		}
		OnPropertyChanged("IsChangeBlockedByActiveTasks");
		OnPropertyChanged("CanConfirm");
		ConfirmCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand(CanExecute = "CanCancel")]
	private void Cancel()
	{
		IsOpen = false;
		RestoreCurrentSelection();
	}

	[RelayCommand(CanExecute = "CanConfirm")]
	private async Task ConfirmAsync()
	{
		SettingsMinecraftDirectoryItem settingsMinecraftDirectoryItem = SelectedDirectory;
		if (settingsMinecraftDirectoryItem == null || !CanConfirm)
		{
			return;
		}
		IsBusy = true;
		try
		{
			if (await switchDirectory(settingsMinecraftDirectoryItem.DirectoryPath))
			{
				IsOpen = false;
			}
		}
		finally
		{
			IsBusy = false;
			RestoreCurrentSelection();
			NotifyDirectoryChangeStateChanged();
		}
	}

	private void RestoreCurrentSelection()
	{
		SetSelectedDirectory(acceptedSelection = FindCurrentDirectory());
	}

	private SettingsMinecraftDirectoryItem? FindCurrentDirectory()
	{
		string currentDirectory = getCurrentDirectory();
		return Directories.FirstOrDefault((SettingsMinecraftDirectoryItem item) => MinecraftDirectoryPath.Equals(item.DirectoryPath, currentDirectory));
	}

	private void SetSelectedDirectory(SettingsMinecraftDirectoryItem? item)
	{
		suppressSelectionChanged = true;
		try
		{
			SelectedDirectory = item;
		}
		finally
		{
			suppressSelectionChanged = false;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedDirectoryChanged(SettingsMinecraftDirectoryItem? value)
	{
		if (!suppressSelectionChanged)
		{
			if (value != null && (IsChangeBlockedByActiveTasks || !value.IsAvailable))
			{
				SetSelectedDirectory(acceptedSelection ?? FindCurrentDirectory());
			}
			else
			{
				acceptedSelection = value;
			}
		}
	}
}
