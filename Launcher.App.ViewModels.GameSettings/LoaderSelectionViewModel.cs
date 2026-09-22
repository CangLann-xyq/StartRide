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
using Launcher.App.Models;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.Utilities;
using Launcher.App.ViewModels.Shared;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.GameSettings;

public sealed class LoaderSelectionViewModel : ObservableObject
{
	private readonly IGameVersionService gameVersionService;

	private readonly IStatusService statusService;

	private readonly IReadOnlyDictionary<LoaderKind, ILoaderProvider> loaderProviders;

	private DownloadSourcePreference downloadSourcePreference = DownloadSourcePreference.Official;

	private int downloadSpeedLimitMbPerSecond;

	[ObservableProperty]
	private MinecraftVersionInfo? selectedMinecraftVersion;

	[ObservableProperty]
	private LoaderKind selectedLoader;

	[ObservableProperty]
	private LoaderVersionInfo? selectedLoaderVersion;

	public ObservableCollection<MinecraftVersionInfo> MinecraftVersions { get; } = new ObservableCollection<MinecraftVersionInfo>();

	public ObservableCollection<NavigationItem> LoaderItems { get; } = new ObservableCollection<NavigationItem>();

	public ObservableCollection<LoaderVersionInfo> LoaderVersions { get; } = new ObservableCollection<LoaderVersionInfo>();

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public MinecraftVersionInfo? SelectedMinecraftVersion
	{
		get
		{
			return selectedMinecraftVersion;
		}
		set
		{
			if (!EqualityComparer<MinecraftVersionInfo>.Default.Equals(selectedMinecraftVersion, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedMinecraftVersion);
				selectedMinecraftVersion = value;
				OnSelectedMinecraftVersionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedMinecraftVersion);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public LoaderKind SelectedLoader
	{
		get
		{
			return selectedLoader;
		}
		set
		{
			if (!EqualityComparer<LoaderKind>.Default.Equals(selectedLoader, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedLoader);
				selectedLoader = value;
				OnSelectedLoaderChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedLoader);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public LoaderVersionInfo? SelectedLoaderVersion
	{
		get
		{
			return selectedLoaderVersion;
		}
		set
		{
			if (!EqualityComparer<LoaderVersionInfo>.Default.Equals(selectedLoaderVersion, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedLoaderVersion);
				selectedLoaderVersion = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedLoaderVersion);
			}
		}
	}

	public LoaderSelectionViewModel(IGameVersionService gameVersionService, IEnumerable<ILoaderProvider> loaderProviders, IStatusService statusService)
	{
		this.gameVersionService = gameVersionService;
		this.statusService = statusService;
		this.loaderProviders = loaderProviders.ToDictionary((ILoaderProvider provider) => provider.Kind);
		foreach (ILoaderProvider value in this.loaderProviders.Values)
		{
			LoaderItems.Add(NavigationCatalog.CreateLoaderItem(value));
		}
	}

	public void PrimeFromSettings(LauncherSettings settings)
	{
		downloadSourcePreference = settings.DownloadSourcePreference;
		downloadSpeedLimitMbPerSecond = settings.DownloadSpeedLimitMbPerSecond;
	}

	public void ApplyDownloadSourcePreference(DownloadSourcePreference preference)
	{
		downloadSourcePreference = preference;
	}

	public void ApplyDownloadSpeedLimit(int downloadSpeedLimitMbPerSecond)
	{
		this.downloadSpeedLimitMbPerSecond = Math.Max(downloadSpeedLimitMbPerSecond, 0);
	}

	public void SelectLoader(LoaderKind loader)
	{
		SelectedLoader = loader;
	}

	public async Task LoadMinecraftVersionsAsync()
	{
		ReportStatus(Strings.Status_LoadingVersions);
		IGameVersionService obj = gameVersionService;
		DownloadSourcePreference num = downloadSourcePreference;
		int num2 = downloadSpeedLimitMbPerSecond;
		IReadOnlyList<MinecraftVersionInfo> source = await obj.GetVersionsAsync(num, default(CancellationToken), num2);
		MinecraftVersions.ReplaceWith(source.Where(IsSelectableMinecraftVersion));
		if ((object)SelectedMinecraftVersion == null)
		{
			SelectedMinecraftVersion = MinecraftVersions.FirstOrDefault();
		}
		ReportStatus(string.Format(Strings.Status_VersionsLoadedFormat, MinecraftVersions.Count));
	}

	public async Task LoadLoaderVersionsAsync()
	{
		LoaderVersions.Clear();
		SelectedLoaderVersion = null;
		if ((object)SelectedMinecraftVersion == null || !loaderProviders.TryGetValue(SelectedLoader, out ILoaderProvider provider))
		{
			return;
		}
		if (!provider.IsImplemented)
		{
			ReportStatus(string.Format(Strings.Status_LoaderVersionsPendingFormat, LoaderDisplayNameProvider.GetDisplayName(provider.Kind)));
			return;
		}
		ReportStatus(string.Format(Strings.Status_LoadingLoaderVersionsFormat, LoaderDisplayNameProvider.GetDisplayName(provider.Kind)));
		ObservableCollection<LoaderVersionInfo> loaderVersions = LoaderVersions;
		ILoaderProvider loaderProvider = provider;
		string name = SelectedMinecraftVersion.Name;
		DownloadSourcePreference num = downloadSourcePreference;
		int num2 = downloadSpeedLimitMbPerSecond;
		loaderVersions.ReplaceWith(await loaderProvider.GetLoaderVersionsAsync(name, num, default(CancellationToken), num2));
		SelectedLoaderVersion = LoaderVersions.FirstOrDefault((LoaderVersionInfo v) => v.IsStable) ?? LoaderVersions.FirstOrDefault();
		ReportStatus(string.Format(Strings.Status_LoaderVersionsLoadedFormat, LoaderDisplayNameProvider.GetDisplayName(provider.Kind)));
	}

	private void ReportStatus(string message)
	{
		statusService.Report(message);
	}

	private static bool IsSelectableMinecraftVersion(MinecraftVersionInfo version)
	{
		if (!version.Type.Equals("Release", StringComparison.OrdinalIgnoreCase))
		{
			return version.Name.StartsWith("1.");
		}
		return true;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedMinecraftVersionChanged(MinecraftVersionInfo? value)
	{
		LoadLoaderVersionsAsync();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedLoaderChanged(LoaderKind value)
	{
		LoadLoaderVersionsAsync();
	}
}
