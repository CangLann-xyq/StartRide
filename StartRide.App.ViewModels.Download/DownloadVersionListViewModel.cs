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
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Download;

public sealed class DownloadVersionListViewModel : ObservableObject, IDisposable
{
	public const string LocalImportCategoryId = "local_import";

	private readonly IGameVersionService gameVersionService;

	private readonly IUiDispatcher uiDispatcher;

	private CancellationTokenSource? loadCancellation;

	private Task? loadTask;

	private bool hasLoadedVersions;

	private int refreshRequestVersion;

	private DownloadSourcePreference downloadSourcePreference = DownloadSourcePreference.Official;

	private int downloadSpeedLimitMbPerSecond;

	[ObservableProperty]
	private DownloadVersionCategory? selectedVersionCategory;

	[ObservableProperty]
	private DownloadMinecraftVersionItem? selectedMinecraftVersion;

	[ObservableProperty]
	private bool isLoadingVersions;

	[ObservableProperty]
	private string versionLoadError = string.Empty;

	[ObservableProperty]
	private string versionEmptyMessage = string.Empty;

	[ObservableProperty]
	private string versionSearchQuery = string.Empty;

	[ObservableProperty]
	private IReadOnlyList<DownloadMinecraftVersionItem> visibleVersions = Array.Empty<DownloadMinecraftVersionItem>();

	[ObservableProperty]
	private int listEntranceAnimationToken;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? refreshVersionsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<DownloadVersionCategory>? selectVersionCategoryCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<DownloadMinecraftVersionItem>? selectMinecraftVersionCommand;

	public ObservableCollection<DownloadVersionCategory> VersionCategories { get; } = new ObservableCollection<DownloadVersionCategory>();

	public List<DownloadMinecraftVersionItem> AllVersions { get; } = new List<DownloadMinecraftVersionItem>();

	public bool HasVisibleVersions => VisibleVersions.Count > 0;

	public bool HasSelectedMinecraftVersion => SelectedMinecraftVersion != null;

	public bool HasVersionLoadError => !string.IsNullOrWhiteSpace(VersionLoadError);

	public bool HasVersionEmptyMessage => !string.IsNullOrWhiteSpace(VersionEmptyMessage);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public DownloadVersionCategory? SelectedVersionCategory
	{
		get
		{
			return selectedVersionCategory;
		}
		set
		{
			if (!EqualityComparer<DownloadVersionCategory>.Default.Equals(selectedVersionCategory, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedVersionCategory);
				selectedVersionCategory = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedVersionCategory);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public DownloadMinecraftVersionItem? SelectedMinecraftVersion
	{
		get
		{
			return selectedMinecraftVersion;
		}
		set
		{
			if (!EqualityComparer<DownloadMinecraftVersionItem>.Default.Equals(selectedMinecraftVersion, value))
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
	public bool IsLoadingVersions
	{
		get
		{
			return isLoadingVersions;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLoadingVersions, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLoadingVersions);
				isLoadingVersions = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLoadingVersions);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string VersionLoadError
	{
		get
		{
			return versionLoadError;
		}
		[MemberNotNull("versionLoadError")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(versionLoadError, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VersionLoadError);
				versionLoadError = value;
				OnVersionLoadErrorChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VersionLoadError);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string VersionEmptyMessage
	{
		get
		{
			return versionEmptyMessage;
		}
		[MemberNotNull("versionEmptyMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(versionEmptyMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VersionEmptyMessage);
				versionEmptyMessage = value;
				OnVersionEmptyMessageChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VersionEmptyMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string VersionSearchQuery
	{
		get
		{
			return versionSearchQuery;
		}
		[MemberNotNull("versionSearchQuery")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(versionSearchQuery, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VersionSearchQuery);
				versionSearchQuery = value;
				OnVersionSearchQueryChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VersionSearchQuery);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<DownloadMinecraftVersionItem> VisibleVersions
	{
		get
		{
			return visibleVersions;
		}
		[MemberNotNull("visibleVersions")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<DownloadMinecraftVersionItem>>.Default.Equals(visibleVersions, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VisibleVersions);
				visibleVersions = value;
				OnVisibleVersionsChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VisibleVersions);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int ListEntranceAnimationToken
	{
		get
		{
			return listEntranceAnimationToken;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(listEntranceAnimationToken, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ListEntranceAnimationToken);
				listEntranceAnimationToken = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ListEntranceAnimationToken);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RefreshVersionsCommand => refreshVersionsCommand ?? (refreshVersionsCommand = new AsyncRelayCommand(RefreshVersionsAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<DownloadVersionCategory> SelectVersionCategoryCommand => selectVersionCategoryCommand ?? (selectVersionCategoryCommand = new RelayCommand<DownloadVersionCategory>(SelectVersionCategory));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<DownloadMinecraftVersionItem> SelectMinecraftVersionCommand => selectMinecraftVersionCommand ?? (selectMinecraftVersionCommand = new RelayCommand<DownloadMinecraftVersionItem>(SelectMinecraftVersion));

	public event Action<DownloadMinecraftVersionItem>? VersionSelected;

	public event Action? LocalImportRequested;

	public event Action? CategoryContentRefreshRequested;

	public DownloadVersionListViewModel(IGameVersionService gameVersionService, IUiDispatcher uiDispatcher)
	{
		this.gameVersionService = gameVersionService;
		this.uiDispatcher = uiDispatcher;
		VersionCategories.Add(new DownloadVersionCategory("release", Strings.Download_ReleaseCategory, string.Empty, "instance_download_page/release"));
		VersionCategories.Add(new DownloadVersionCategory("snapshot", Strings.Download_SnapshotCategory, string.Empty, "instance_download_page/snapshot"));
		VersionCategories.Add(new DownloadVersionCategory("april_fools", Strings.Download_AprilFoolsCategory, string.Empty, "instance_download_page/winking-face-with-open-eyes"));
		VersionCategories.Add(new DownloadVersionCategory("ancient", Strings.Download_AncientCategory, string.Empty, "instance_download_page/time"));
		VersionCategories.Add(new DownloadVersionCategory("local_import", Strings.Download_LocalImportCategory, string.Empty, "instance_download_page/localimport"));
		SelectVersionCategoryCore(VersionCategories[0], deferRefresh: false);
	}

	public void ApplyDownloadSourcePreference(DownloadSourcePreference preference)
	{
		if (downloadSourcePreference != preference)
		{
			downloadSourcePreference = preference;
			CancelLoad();
			hasLoadedVersions = false;
			IsLoadingVersions = false;
			VersionLoadError = string.Empty;
			VersionEmptyMessage = string.Empty;
			AllVersions.Clear();
			ClearSelectedVersion();
			RefreshVisibleVersions();
		}
	}

	public void ApplyDownloadSpeedLimit(int value)
	{
		downloadSpeedLimitMbPerSecond = Math.Max(value, 0);
	}

	public async Task EnsureVersionsLoadedAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (hasLoadedVersions)
		{
			return;
		}
		if (loadTask == null)
		{
			CancellationTokenSource cancellation = (loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
			Task currentTask = (loadTask = LoadVersionsAsync(cancellation));
			try
			{
				await currentTask;
				return;
			}
			finally
			{
				if (loadTask == currentTask)
				{
					loadTask = null;
				}
				if (loadCancellation == cancellation)
				{
					loadCancellation = null;
				}
				cancellation.Dispose();
			}
		}
		await loadTask.WaitAsync(cancellationToken);
	}

	public void RefreshCurrentCategoryContent()
	{
		RequestVisibleVersionsRefresh(defer: false);
	}

	public void ClearSelectedVersion()
	{
		SelectedMinecraftVersion = null;
		foreach (DownloadMinecraftVersionItem allVersion in AllVersions)
		{
			allVersion.IsSelected = false;
		}
	}

	public void Dispose()
	{
		CancelLoad();
	}

	[RelayCommand]
	private Task RefreshVersionsAsync()
	{
		return EnsureVersionsLoadedAsync();
	}

	[RelayCommand]
	private void SelectVersionCategory(DownloadVersionCategory category)
	{
		if (!category.IsEnabled)
		{
			return;
		}
		if (string.Equals(category.Id, "local_import", StringComparison.Ordinal))
		{
			LocalImportRequested?.Invoke();
			return;
		}
		bool num = SelectedVersionCategory == category;
		SelectVersionCategoryCore(category, deferRefresh: false);
		if (num)
		{
			RefreshCurrentCategoryContent();
			CategoryContentRefreshRequested?.Invoke();
		}
		else if (hasLoadedVersions)
		{
			ListEntranceAnimationToken++;
		}
	}

	[RelayCommand]
	private void SelectMinecraftVersion(DownloadMinecraftVersionItem version)
	{
		SelectedMinecraftVersion = version;
		foreach (DownloadMinecraftVersionItem allVersion in AllVersions)
		{
			allVersion.IsSelected = allVersion == version;
		}
		VersionSelected?.Invoke(version);
	}

	private async Task LoadVersionsAsync(CancellationTokenSource cancellation)
	{
		IsLoadingVersions = true;
		VersionLoadError = string.Empty;
		VersionEmptyMessage = string.Empty;
		try
		{
			IReadOnlyList<MinecraftVersionInfo> versions = await gameVersionService.GetVersionsAsync(downloadSourcePreference, cancellation.Token, downloadSpeedLimitMbPerSecond);
			if (loadCancellation != cancellation)
			{
				return;
			}
			await UiTransitionGate.WaitForIdleAsync(cancellation.Token);
			if (loadCancellation == cancellation)
			{
				AllVersions.Clear();
				AllVersions.AddRange(versions.Select((MinecraftVersionInfo version) => new DownloadMinecraftVersionItem(version)));
				hasLoadedVersions = true;
			}
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
		}
		catch (Exception)
		{
			if (loadCancellation == cancellation)
			{
				VersionLoadError = Strings.Status_LoadVersionsFailed;
			}
		}
		finally
		{
			if (loadCancellation == cancellation)
			{
				IsLoadingVersions = false;
				RefreshVisibleVersions();
				if (hasLoadedVersions)
				{
					ListEntranceAnimationToken++;
				}
			}
		}
	}

	private void SelectVersionCategoryCore(DownloadVersionCategory category, bool deferRefresh)
	{
		SelectedVersionCategory = category;
		foreach (DownloadVersionCategory versionCategory in VersionCategories)
		{
			versionCategory.IsSelected = versionCategory == category;
		}
		RequestVisibleVersionsRefresh(deferRefresh);
	}

	private void RequestVisibleVersionsRefresh(bool defer)
	{
		int requestVersion = ++refreshRequestVersion;
		if (defer && uiDispatcher.HasAccess)
		{
			uiDispatcher.Post(delegate
			{
				if (requestVersion == refreshRequestVersion)
				{
					RefreshVisibleVersions();
				}
			});
		}
		else
		{
			RefreshVisibleVersions();
		}
	}

	private void RefreshVisibleVersions()
	{
		DownloadVersionFilterResult downloadVersionFilterResult = DownloadVersionFilter.Apply(AllVersions, SelectedVersionCategory, VersionSearchQuery, SelectedMinecraftVersion, hasLoadedVersions, IsLoadingVersions, HasVersionLoadError);
		VersionEmptyMessage = downloadVersionFilterResult.EmptyMessage;
		if (downloadVersionFilterResult.ShouldClearSelectedVersion)
		{
			ClearSelectedVersion();
		}
		VisibleVersions = downloadVersionFilterResult.Versions;
	}

	private void CancelLoad()
	{
		loadCancellation?.Cancel();
		loadCancellation = null;
		loadTask = null;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedMinecraftVersionChanged(DownloadMinecraftVersionItem? value)
	{
		OnPropertyChanged("HasSelectedMinecraftVersion");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnVersionLoadErrorChanged(string value)
	{
		OnPropertyChanged("HasVersionLoadError");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnVersionEmptyMessageChanged(string value)
	{
		OnPropertyChanged("HasVersionEmptyMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnVersionSearchQueryChanged(string value)
	{
		RequestVisibleVersionsRefresh(defer: false);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnVisibleVersionsChanged(IReadOnlyList<DownloadMinecraftVersionItem> value)
	{
		OnPropertyChanged("HasVisibleVersions");
		OnPropertyChanged("HasVersionEmptyMessage");
	}
}
