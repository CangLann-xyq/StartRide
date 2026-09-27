using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Models;
using StartRide.App.Resources;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Account;

public sealed class AccountSkinModelDialogViewModel : ObservableObject
{
	private string pendingSkinFilePath = string.Empty;

	private bool isChangingExistingSkinModel;

	[ObservableProperty]
	private bool isSkinModelDialogOpen;

	[ObservableProperty]
	private bool isSkinFormatError;

	[ObservableProperty]
	private AccountSkinModelOption? selectedSkinModelOption;

	public ObservableCollection<AccountSkinModelOption> SkinModelOptions { get; } = new ObservableCollection<AccountSkinModelOption>(AccountSkinModelOptionFactory.Create());

	public bool CanConfirmSkinModelDialog
	{
		get
		{
			if (IsSkinModelDialogOpen)
			{
				if (!IsSkinFormatError)
				{
					if (isChangingExistingSkinModel || !string.IsNullOrWhiteSpace(pendingSkinFilePath))
					{
						return SelectedSkinModelOption != null;
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public bool IsSkinModelSelectionStep
	{
		get
		{
			if (IsSkinModelDialogOpen)
			{
				return !IsSkinFormatError;
			}
			return false;
		}
	}

	public bool CanShowSkinModelDialogCancelButton => IsSkinModelSelectionStep;

	public string SkinModelDialogTitle
	{
		get
		{
			if (!IsSkinFormatError)
			{
				return Strings.Dialog_SkinModelTitle;
			}
			return Strings.Dialog_SkinFormatErrorTitle;
		}
	}

	public string SkinModelDialogSubtitle
	{
		get
		{
			if (!IsSkinFormatError)
			{
				return Strings.Dialog_SkinModelSubtitle;
			}
			return Strings.Dialog_SkinFormatErrorSubtitle;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsSkinModelDialogOpen
	{
		get
		{
			return isSkinModelDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isSkinModelDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsSkinModelDialogOpen);
				isSkinModelDialogOpen = value;
				OnIsSkinModelDialogOpenChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsSkinModelDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsSkinFormatError
	{
		get
		{
			return isSkinFormatError;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isSkinFormatError, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsSkinFormatError);
				isSkinFormatError = value;
				OnIsSkinFormatErrorChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsSkinFormatError);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public AccountSkinModelOption? SelectedSkinModelOption
	{
		get
		{
			return selectedSkinModelOption;
		}
		set
		{
			if (!EqualityComparer<AccountSkinModelOption>.Default.Equals(selectedSkinModelOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedSkinModelOption);
				selectedSkinModelOption = value;
				OnSelectedSkinModelOptionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedSkinModelOption);
			}
		}
	}

	public void Open(string skinFilePath)
	{
		pendingSkinFilePath = skinFilePath;
		isChangingExistingSkinModel = false;
		IsSkinFormatError = false;
		SelectedSkinModelOption = null;
		IsSkinModelDialogOpen = true;
		NotifyDialogStateChanged();
	}

	public void OpenForExistingSkin(MinecraftSkinModel skinModel)
	{
		pendingSkinFilePath = string.Empty;
		isChangingExistingSkinModel = true;
		IsSkinFormatError = false;
		SelectedSkinModelOption = SkinModelOptions.FirstOrDefault((AccountSkinModelOption option) => option.Model == skinModel);
		IsSkinModelDialogOpen = true;
		NotifyDialogStateChanged();
	}

	public void OpenFormatError()
	{
		pendingSkinFilePath = string.Empty;
		isChangingExistingSkinModel = false;
		SelectedSkinModelOption = null;
		IsSkinFormatError = true;
		IsSkinModelDialogOpen = true;
		NotifyDialogStateChanged();
	}

	public void Cancel()
	{
		IsSkinModelDialogOpen = false;
		Reset();
	}

	public void Reset()
	{
		pendingSkinFilePath = string.Empty;
		isChangingExistingSkinModel = false;
		IsSkinFormatError = false;
		SelectedSkinModelOption = null;
		NotifyDialogStateChanged();
	}

	public bool TryConsumeSelection(out string skinFilePath, out MinecraftSkinModel skinModel)
	{
		skinFilePath = string.Empty;
		skinModel = MinecraftSkinModel.Classic;
		if (!CanConfirmSkinModelDialog || SelectedSkinModelOption == null)
		{
			return false;
		}
		skinFilePath = pendingSkinFilePath;
		skinModel = SelectedSkinModelOption.Model;
		IsSkinModelDialogOpen = false;
		Reset();
		return true;
	}

	private void NotifyDialogStateChanged()
	{
		OnPropertyChanged("CanConfirmSkinModelDialog");
		OnPropertyChanged("IsSkinModelSelectionStep");
		OnPropertyChanged("CanShowSkinModelDialogCancelButton");
		OnPropertyChanged("SkinModelDialogTitle");
		OnPropertyChanged("SkinModelDialogSubtitle");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsSkinModelDialogOpenChanged(bool value)
	{
		NotifyDialogStateChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsSkinFormatErrorChanged(bool value)
	{
		NotifyDialogStateChanged();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedSkinModelOptionChanged(AccountSkinModelOption? value)
	{
		OnPropertyChanged("CanConfirmSkinModelDialog");
	}
}
