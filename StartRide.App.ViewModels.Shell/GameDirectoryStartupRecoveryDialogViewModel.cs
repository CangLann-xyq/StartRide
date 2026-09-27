using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using Launcher.Application.Services;

namespace StartRide.App.ViewModels.Shell;

public sealed class GameDirectoryStartupRecoveryDialogViewModel : ObservableObject
{
	private MinecraftDirectoryStartupRecoveryResult? pendingRecovery;

	[ObservableProperty]
	private bool isOpen;

	[ObservableProperty]
	private string message = string.Empty;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? closeCommand;

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

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CloseCommand => closeCommand ?? (closeCommand = new RelayCommand(Close));

	public void Prime(MinecraftDirectoryStartupRecoveryResult? recovery)
	{
		pendingRecovery = recovery;
		IsOpen = false;
		Message = string.Empty;
	}

	public void ShowPending()
	{
		MinecraftDirectoryStartupRecoveryResult minecraftDirectoryStartupRecoveryResult = pendingRecovery;
		if ((object)minecraftDirectoryStartupRecoveryResult != null)
		{
			pendingRecovery = null;
			Message = string.Format(minecraftDirectoryStartupRecoveryResult.UsedDefaultDirectory ? Strings.Dialog_MinecraftDirectoryStartupDefaultMessageFormat : Strings.Dialog_MinecraftDirectoryStartupSwitchedMessageFormat, minecraftDirectoryStartupRecoveryResult.InvalidDirectory, minecraftDirectoryStartupRecoveryResult.SelectedDirectory);
			IsOpen = true;
		}
	}

	[RelayCommand]
	private void Close()
	{
		IsOpen = false;
	}
}
