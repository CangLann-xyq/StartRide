using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Models;
using StartRide.App.Services;
using StartRide.App.Utilities;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class GameManagementViewModel : ObservableObject
{
	private readonly IStatusService statusService;

	[ObservableProperty]
	private double progressPercent;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? loadMinecraftVersionsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? refreshInstancesCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? loadLoaderVersionsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? createInstanceCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? searchModsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? installSelectedModCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? saveSettingsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? saveInstanceCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? setDefaultInstanceCommand;

	public InstanceManagementViewModel InstancesViewModel { get; }

	public LoaderSelectionViewModel LoaderSelection { get; }

	public ModrinthSearchViewModel ModrinthSearch { get; }

	public ObservableCollection<GameInstance> Instances => InstancesViewModel.Instances;

	public long InstanceCatalogRevision => InstancesViewModel.CatalogRevision;

	public ObservableCollection<MinecraftVersionInfo> MinecraftVersions => LoaderSelection.MinecraftVersions;

	public ObservableCollection<NavigationItem> LoaderItems => LoaderSelection.LoaderItems;

	public ObservableCollection<LoaderVersionInfo> LoaderVersions => LoaderSelection.LoaderVersions;

	public ObservableCollection<ModrinthProject> ModrinthProjects => ModrinthSearch.ModrinthProjects;

	public GameInstance? SelectedInstance
	{
		get
		{
			return InstancesViewModel.SelectedInstance;
		}
		set
		{
			InstancesViewModel.SelectedInstance = value;
		}
	}

	public MinecraftVersionInfo? SelectedMinecraftVersion
	{
		get
		{
			return LoaderSelection.SelectedMinecraftVersion;
		}
		set
		{
			LoaderSelection.SelectedMinecraftVersion = value;
		}
	}

	public LoaderKind SelectedLoader
	{
		get
		{
			return LoaderSelection.SelectedLoader;
		}
		set
		{
			LoaderSelection.SelectedLoader = value;
		}
	}

	public LoaderVersionInfo? SelectedLoaderVersion
	{
		get
		{
			return LoaderSelection.SelectedLoaderVersion;
		}
		set
		{
			LoaderSelection.SelectedLoaderVersion = value;
		}
	}

	public string NewInstanceName
	{
		get
		{
			return InstancesViewModel.NewInstanceName;
		}
		set
		{
			InstancesViewModel.NewInstanceName = value;
		}
	}

	public string ModSearchQuery
	{
		get
		{
			return ModrinthSearch.ModSearchQuery;
		}
		set
		{
			ModrinthSearch.ModSearchQuery = value;
		}
	}

	public ModrinthProject? SelectedModrinthProject
	{
		get
		{
			return ModrinthSearch.SelectedModrinthProject;
		}
		set
		{
			ModrinthSearch.SelectedModrinthProject = value;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double ProgressPercent
	{
		get
		{
			return progressPercent;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(progressPercent, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ProgressPercent);
				progressPercent = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ProgressPercent);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand LoadMinecraftVersionsCommand => loadMinecraftVersionsCommand ?? (loadMinecraftVersionsCommand = new AsyncRelayCommand(LoadMinecraftVersionsAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RefreshInstancesCommand => refreshInstancesCommand ?? (refreshInstancesCommand = new AsyncRelayCommand(RefreshInstancesAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand LoadLoaderVersionsCommand => loadLoaderVersionsCommand ?? (loadLoaderVersionsCommand = new AsyncRelayCommand(LoadLoaderVersionsAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand CreateInstanceCommand => createInstanceCommand ?? (createInstanceCommand = new AsyncRelayCommand(CreateInstanceAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand SearchModsCommand => searchModsCommand ?? (searchModsCommand = new AsyncRelayCommand(SearchModsAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand InstallSelectedModCommand => installSelectedModCommand ?? (installSelectedModCommand = new AsyncRelayCommand(InstallSelectedModAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand SaveSettingsCommand => saveSettingsCommand ?? (saveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand SaveInstanceCommand => saveInstanceCommand ?? (saveInstanceCommand = new AsyncRelayCommand(SaveInstanceAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand SetDefaultInstanceCommand => setDefaultInstanceCommand ?? (setDefaultInstanceCommand = new AsyncRelayCommand(SetDefaultInstanceAsync));

	public GameManagementViewModel(InstanceManagementViewModel instances, LoaderSelectionViewModel loaderSelection, ModrinthSearchViewModel modrinthSearch, IStatusService statusService)
	{
		InstancesViewModel = instances;
		LoaderSelection = loaderSelection;
		ModrinthSearch = modrinthSearch;
		this.statusService = statusService;
		InstancesViewModel.PropertyChanged += InstancesViewModel_PropertyChanged;
		LoaderSelection.PropertyChanged += ForwardChildPropertyChanged;
		ModrinthSearch.PropertyChanged += ForwardChildPropertyChanged;
	}

	public IReadOnlyList<GameInstance> GetInstanceCatalogSnapshot()
	{
		return Instances.ToArray();
	}

	public async Task InitializeAsync(LauncherSettings launcherSettings)
	{
		LoaderSelection.PrimeFromSettings(launcherSettings);
		await InstancesViewModel.InitializeAsync(launcherSettings);
	}

	public Task PrimeInstancesAsync(LauncherSettings launcherSettings)
	{
		return InstancesViewModel.PrimeInstancesAsync(launcherSettings);
	}

	public void ApplyDownloadSourcePreference(DownloadSourcePreference preference)
	{
		LoaderSelection.ApplyDownloadSourcePreference(preference);
	}

	public void ApplyDownloadSpeedLimit(int downloadSpeedLimitMbPerSecond)
	{
		LoaderSelection.ApplyDownloadSpeedLimit(downloadSpeedLimitMbPerSecond);
	}

	public async Task EnsureInstancesLoadedAsync()
	{
		if (!InstancesViewModel.HasLoadedInstances)
		{
			await InstancesViewModel.EnsureInstancesLoadedAsync();
		}
	}

	public void SelectLoader(LoaderKind loader)
	{
		LoaderSelection.SelectLoader(loader);
	}

	public Task<bool> SelectLaunchInstanceAsync(GameInstance instance)
	{
		return InstancesViewModel.SelectLaunchInstanceAsync(instance);
	}

	public Task ApplyUpdatedInstanceAsync(GameInstance instance)
	{
		return InstancesViewModel.ApplyUpdatedInstanceAsync(instance);
	}

	public Task<bool> RemoveInstanceAsync(string instanceId)
	{
		return InstancesViewModel.RemoveInstanceAsync(instanceId);
	}

	[RelayCommand]
	private Task LoadMinecraftVersionsAsync()
	{
		return LoaderSelection.LoadMinecraftVersionsAsync();
	}

	[RelayCommand]
	public Task RefreshInstancesAsync()
	{
		return InstancesViewModel.RefreshInstancesAsync();
	}

	[RelayCommand]
	private Task LoadLoaderVersionsAsync()
	{
		return LoaderSelection.LoadLoaderVersionsAsync();
	}

	[RelayCommand]
	private Task CreateInstanceAsync()
	{
		return InstancesViewModel.CreateInstanceAsync(SelectedMinecraftVersion, SelectedLoader, SelectedLoaderVersion, CreateProgress());
	}

	[RelayCommand]
	private Task SearchModsAsync()
	{
		return ModrinthSearch.SearchModsAsync(SelectedInstance);
	}

	[RelayCommand]
	private async Task InstallSelectedModAsync()
	{
		await ModrinthSearch.InstallSelectedModAsync(SelectedInstance, CreateProgress());
	}

	[RelayCommand]
	private Task SaveSettingsAsync()
	{
		return InstancesViewModel.SaveSettingsAsync();
	}

	[RelayCommand]
	private Task SaveInstanceAsync()
	{
		return InstancesViewModel.SaveInstanceAsync();
	}

	[RelayCommand]
	private Task SetDefaultInstanceAsync()
	{
		return InstancesViewModel.SetDefaultInstanceAsync();
	}

	private IProgress<LauncherProgress> CreateProgress()
	{
		return new Progress<LauncherProgress>(delegate(LauncherProgress progress)
		{
			ReportStatus(LauncherProgressTextFormatter.Format(progress));
			ProgressPercent = progress.Percent.GetValueOrDefault();
		});
	}

	private void InstancesViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		ForwardChildPropertyChanged(sender, e);
	}

	private void ForwardChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (!string.IsNullOrWhiteSpace(e.PropertyName))
		{
			OnPropertyChanged(e.PropertyName);
		}
	}

	private void ReportStatus(string message)
	{
		statusService.Report(message);
	}
}
