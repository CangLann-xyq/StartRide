using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class InstanceGeneralSettingsViewModel : GameSettingsDetailsSectionViewModelBase, IDisposable
{
	private static readonly TimeSpan DescriptionSaveDelay = TimeSpan.FromMilliseconds(450.0);

	private readonly GameSettingsEditDialogViewModel editDialog;

	private readonly IInstanceFolderService instanceFolderService;

	private readonly IStatusService statusService;

	private readonly InstanceSettingsPersistenceCoordinator persistence;

	private INotifyPropertyChanged? selectedInstanceNotifier;

	private GameSettingsInstanceItem? selectedInstance;

	private bool suppressAutoSave;

	[ObservableProperty]
	private string descriptionText = string.Empty;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestEditInstanceCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openInstanceDirectoryCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? requestDeleteInstanceCommand;

	public string InstanceName => selectedInstance?.Name ?? string.Empty;

	public string InstanceIconSource => selectedInstance?.IconSource ?? string.Empty;

	public string InstanceSubtitle => selectedInstance?.Subtitle ?? string.Empty;

	public string InstanceCreatedAtText => selectedInstance?.Instance.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string DescriptionText
	{
		get
		{
			return descriptionText;
		}
		[MemberNotNull("descriptionText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(descriptionText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DescriptionText);
				descriptionText = value;
				OnDescriptionTextChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DescriptionText);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestEditInstanceCommand => requestEditInstanceCommand ?? (requestEditInstanceCommand = new RelayCommand(RequestEditInstance));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenInstanceDirectoryCommand => openInstanceDirectoryCommand ?? (openInstanceDirectoryCommand = new RelayCommand(OpenInstanceDirectory));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RequestDeleteInstanceCommand => requestDeleteInstanceCommand ?? (requestDeleteInstanceCommand = new RelayCommand(RequestDeleteInstance));

	public event Action<GameSettingsInstanceItem>? DeleteInstanceRequested;

	internal InstanceGeneralSettingsViewModel(GameSettingsEditDialogViewModel editDialog, IInstanceFolderService instanceFolderService, IStatusService statusService, InstanceSettingsPersistenceCoordinator persistence)
	{
		this.editDialog = editDialog;
		this.instanceFolderService = instanceFolderService;
		this.statusService = statusService;
		this.persistence = persistence;
	}

	public void SetSelectedInstance(GameSettingsInstanceItem? value)
	{
		if (selectedInstanceNotifier != null)
		{
			selectedInstanceNotifier.PropertyChanged -= SelectedInstance_PropertyChanged;
		}
		selectedInstance = value;
		selectedInstanceNotifier = value;
		if (selectedInstanceNotifier != null)
		{
			selectedInstanceNotifier.PropertyChanged += SelectedInstance_PropertyChanged;
		}
		NotifyInstanceDisplayChanged();
		LoadDescriptionFromInstance();
	}

	public void Dispose()
	{
		if (selectedInstanceNotifier != null)
		{
			selectedInstanceNotifier.PropertyChanged -= SelectedInstance_PropertyChanged;
		}
		selectedInstanceNotifier = null;
	}

	[RelayCommand]
	private void RequestEditInstance()
	{
		if (selectedInstance != null)
		{
			editDialog.Open(selectedInstance);
		}
	}

	[RelayCommand]
	private void OpenInstanceDirectory()
	{
		if (selectedInstance != null)
		{
			string instanceDirectory = selectedInstance.Instance.InstanceDirectory;
			if (!instanceFolderService.DirectoryExists(instanceDirectory))
			{
				statusService.Report(Strings.Status_InstanceFolderNotFound);
			}
			else if (!instanceFolderService.TryOpen(instanceDirectory))
			{
				statusService.Report(Strings.Status_OpenInstanceFolderFailed);
			}
		}
	}

	[RelayCommand]
	private void RequestDeleteInstance()
	{
		if (selectedInstance != null)
		{
			DeleteInstanceRequested?.Invoke(selectedInstance);
		}
	}

	private void SelectedInstance_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		NotifyInstanceDisplayChanged();
	}

	private void NotifyInstanceDisplayChanged()
	{
		OnPropertyChanged("InstanceName");
		OnPropertyChanged("InstanceIconSource");
		OnPropertyChanged("InstanceSubtitle");
		OnPropertyChanged("InstanceCreatedAtText");
	}

	private void LoadDescriptionFromInstance()
	{
		suppressAutoSave = true;
		try
		{
			DescriptionText = selectedInstance?.Instance.Description ?? string.Empty;
		}
		finally
		{
			suppressAutoSave = false;
		}
	}

	private static string NormalizeDescription(string? value)
	{
		return value?.Trim() ?? string.Empty;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnDescriptionTextChanged(string value)
	{
		if (suppressAutoSave || selectedInstance == null)
		{
			return;
		}
		GameInstance instance = selectedInstance.Instance;
		string normalizedDescription = NormalizeDescription(value);
		persistence.Schedule("description", instance, delegate(GameInstance target)
		{
			string originalDescription = target.Description;
			if (string.Equals(originalDescription, normalizedDescription, StringComparison.Ordinal))
			{
				return (Action?)null;
			}
			target.Description = normalizedDescription;
			return delegate
			{
				target.Description = originalDescription;
			};
		}, LoadDescriptionFromInstance, DescriptionSaveDelay);
	}
}
