using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace StartRide.App.ViewModels.Resources;

public sealed class ResourcesProjectVersionsViewModel : ObservableObject, IDisposable
{
	private sealed record AvailableVersionPage(ResourceProjectVersionsResult Result, IReadOnlyList<ResourceProjectVersion> Versions);

	private const int PageSize = 10000;

	private readonly HashSet<string> loadedVersionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly ResourcesOnlineProjectPageOptions options;

	private readonly IResourceCatalogService? resourceCatalogService;

	private readonly Func<IReadOnlyList<GameInstance>>? getInstanceCatalogSnapshot;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger? logger;

	private CancellationTokenSource? targetsCancellation;

	private CancellationTokenSource? versionsCancellation;

	private bool isApplyingFilters;

	private ResourcesModProjectItemViewModel? currentProject;

	[ObservableProperty]
	private ResourcesModInstallTargetItemViewModel? selectedTarget;

	[ObservableProperty]
	private bool isLoadingTargets;

	[ObservableProperty]
	private string targetsLoadErrorMessage = string.Empty;

	[ObservableProperty]
	private int installTargetsEntranceAnimationToken;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private string loadErrorMessage = string.Empty;

	[ObservableProperty]
	private bool isLoadingMore;

	[ObservableProperty]
	private string loadMoreMessage = string.Empty;

	[ObservableProperty]
	private bool hasMore;

	[ObservableProperty]
	[NotifyPropertyChangedFor("HasVisibleVersions")]
	private int visibleVersionCount;

	[ObservableProperty]
	private int listEntranceAnimationToken;

	[ObservableProperty]
	private string searchQuery = string.Empty;

	[ObservableProperty]
	private ResourcesFilterOptionItem? selectedVersionFilter;

	[ObservableProperty]
	private ResourcesFilterOptionItem? selectedLoaderFilter;

	[ObservableProperty]
	private bool isUnknownInstanceVersionDialogOpen;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ResourcesModInstallTargetItemViewModel?>? selectTargetCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? refreshCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ResourcesModVersionItemViewModel?>? installVersionCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? closeUnknownInstanceVersionDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? loadMoreCommand;

	internal ResourcesAvailableVersionListBuilder Builder { get; }

	public ObservableCollection<ResourcesModInstallTargetItemViewModel> InstallTargets { get; } = new ObservableCollection<ResourcesModInstallTargetItemViewModel>();

	public ObservableCollection<object> ListItems { get; } = new ObservableCollection<object>();

	public ObservableCollection<ResourcesFilterOptionItem> VersionFilterOptions { get; } = new ObservableCollection<ResourcesFilterOptionItem>();

	public ObservableCollection<ResourcesFilterOptionItem> LoaderFilterOptions { get; } = new ObservableCollection<ResourcesFilterOptionItem>();

	public IReadOnlyList<ResourceProjectVersion> SourceVersions { get; private set; } = Array.Empty<ResourceProjectVersion>();

	public int NextPageOffset { get; private set; }

	public string InstallTargetSectionText => options.InstallTargetSectionText;

	public string InstallTargetsLoadingMessage => options.InstallTargetsLoadingText;

	public string LoadingMessage => options.VersionsLoadingText;

	public string Title => Builder.FormatTitle(SelectedTarget);

	public bool HasTargetsLoadErrorMessage => !string.IsNullOrWhiteSpace(TargetsLoadErrorMessage);

	public bool CanShowTargetsLoadingState
	{
		get
		{
			if (IsLoadingTargets)
			{
				return InstallTargets.Count == 0;
			}
			return false;
		}
	}

	public bool CanShowTargetsLoadErrorState
	{
		get
		{
			if (!IsLoadingTargets)
			{
				return HasTargetsLoadErrorMessage;
			}
			return false;
		}
	}

	public bool HasInstallTargets => InstallTargets.Count > 0;

	public bool HasLoadErrorMessage => !string.IsNullOrWhiteSpace(LoadErrorMessage);

	public bool HasVisibleVersions => VisibleVersionCount > 0;

	public bool HasFilters
	{
		get
		{
			if (string.IsNullOrWhiteSpace(SearchQuery))
			{
				string text = SelectedVersionFilter?.Id;
				if (text == null || string.Equals(text, "all", StringComparison.OrdinalIgnoreCase))
				{
					if (options.ShowsLoaderFilters)
					{
						string text2 = SelectedLoaderFilter?.Id;
						if (text2 != null)
						{
							return !string.Equals(text2, "all", StringComparison.OrdinalIgnoreCase);
						}
					}
					return false;
				}
			}
			return true;
		}
	}

	public string EmptyMessage
	{
		get
		{
			if (!HasFilters)
			{
				ResourcesModInstallTargetItemViewModel? resourcesModInstallTargetItemViewModel = SelectedTarget;
				if ((resourcesModInstallTargetItemViewModel == null || !resourcesModInstallTargetItemViewModel.IsLocalDownload) && !ResourcesAvailableVersionListBuilder.IsUnknownInstanceVersionTarget(SelectedTarget))
				{
					return options.VersionsEmptyText;
				}
				return options.VersionsEmptyLocalText;
			}
			return options.VersionsFilterEmptyText;
		}
	}

	public bool CanShowLoadingState
	{
		get
		{
			if (IsLoading)
			{
				return VisibleVersionCount == 0;
			}
			return false;
		}
	}

	public bool CanShowEmptyState
	{
		get
		{
			if (!IsLoading && VisibleVersionCount == 0 && !HasMore)
			{
				return !HasLoadErrorMessage;
			}
			return false;
		}
	}

	public bool CanShowLoadErrorState
	{
		get
		{
			if (!IsLoading)
			{
				return HasLoadErrorMessage;
			}
			return false;
		}
	}

	public bool CanShowLoadMoreState
	{
		get
		{
			if (VisibleVersionCount > 0)
			{
				if (!IsLoadingMore)
				{
					return !string.IsNullOrWhiteSpace(LoadMoreMessage);
				}
				return true;
			}
			return false;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesModInstallTargetItemViewModel? SelectedTarget
	{
		get
		{
			return selectedTarget;
		}
		set
		{
			if (!EqualityComparer<ResourcesModInstallTargetItemViewModel>.Default.Equals(selectedTarget, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedTarget);
				selectedTarget = value;
				OnSelectedTargetChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedTarget);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLoadingTargets
	{
		get
		{
			return isLoadingTargets;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLoadingTargets, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLoadingTargets);
				isLoadingTargets = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLoadingTargets);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string TargetsLoadErrorMessage
	{
		get
		{
			return targetsLoadErrorMessage;
		}
		[MemberNotNull("targetsLoadErrorMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(targetsLoadErrorMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TargetsLoadErrorMessage);
				targetsLoadErrorMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TargetsLoadErrorMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int InstallTargetsEntranceAnimationToken
	{
		get
		{
			return installTargetsEntranceAnimationToken;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(installTargetsEntranceAnimationToken, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InstallTargetsEntranceAnimationToken);
				installTargetsEntranceAnimationToken = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InstallTargetsEntranceAnimationToken);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLoading
	{
		get
		{
			return isLoading;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLoading, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLoading);
				isLoading = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLoading);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LoadErrorMessage
	{
		get
		{
			return loadErrorMessage;
		}
		[MemberNotNull("loadErrorMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(loadErrorMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LoadErrorMessage);
				loadErrorMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LoadErrorMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLoadingMore
	{
		get
		{
			return isLoadingMore;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLoadingMore, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLoadingMore);
				isLoadingMore = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLoadingMore);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string LoadMoreMessage
	{
		get
		{
			return loadMoreMessage;
		}
		[MemberNotNull("loadMoreMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(loadMoreMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LoadMoreMessage);
				loadMoreMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LoadMoreMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasMore
	{
		get
		{
			return hasMore;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(hasMore, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasMore);
				hasMore = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasMore);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int VisibleVersionCount
	{
		get
		{
			return visibleVersionCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(visibleVersionCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VisibleVersionCount);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasVisibleVersions);
				visibleVersionCount = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VisibleVersionCount);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasVisibleVersions);
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

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string SearchQuery
	{
		get
		{
			return searchQuery;
		}
		[MemberNotNull("searchQuery")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(searchQuery, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SearchQuery);
				searchQuery = value;
				OnSearchQueryChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SearchQuery);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesFilterOptionItem? SelectedVersionFilter
	{
		get
		{
			return selectedVersionFilter;
		}
		set
		{
			if (!EqualityComparer<ResourcesFilterOptionItem>.Default.Equals(selectedVersionFilter, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedVersionFilter);
				selectedVersionFilter = value;
				OnSelectedVersionFilterChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedVersionFilter);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesFilterOptionItem? SelectedLoaderFilter
	{
		get
		{
			return selectedLoaderFilter;
		}
		set
		{
			if (!EqualityComparer<ResourcesFilterOptionItem>.Default.Equals(selectedLoaderFilter, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedLoaderFilter);
				selectedLoaderFilter = value;
				OnSelectedLoaderFilterChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedLoaderFilter);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsUnknownInstanceVersionDialogOpen
	{
		get
		{
			return isUnknownInstanceVersionDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isUnknownInstanceVersionDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsUnknownInstanceVersionDialogOpen);
				isUnknownInstanceVersionDialogOpen = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsUnknownInstanceVersionDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ResourcesModInstallTargetItemViewModel?> SelectTargetCommand => selectTargetCommand ?? (selectTargetCommand = new RelayCommand<ResourcesModInstallTargetItemViewModel>(SelectTarget));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RefreshCommand => refreshCommand ?? (refreshCommand = new AsyncRelayCommand(RefreshAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ResourcesModVersionItemViewModel?> InstallVersionCommand => installVersionCommand ?? (installVersionCommand = new RelayCommand<ResourcesModVersionItemViewModel>(InstallVersion));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CloseUnknownInstanceVersionDialogCommand => closeUnknownInstanceVersionDialogCommand ?? (closeUnknownInstanceVersionDialogCommand = new RelayCommand(CloseUnknownInstanceVersionDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand LoadMoreCommand => loadMoreCommand ?? (loadMoreCommand = new AsyncRelayCommand(LoadMoreAsync));

	public event Action<ResourcesModInstallTargetItemViewModel>? TargetSelected;

	public event Action<ResourcesModVersionItemViewModel>? InstallRequested;

	internal ResourcesProjectVersionsViewModel(ResourcesOnlineProjectPageOptions options, IResourceCatalogService? resourceCatalogService, Func<IReadOnlyList<GameInstance>>? getInstanceCatalogSnapshot, IUiDispatcher uiDispatcher, ILogger? logger)
	{
		this.options = options;
		this.resourceCatalogService = resourceCatalogService;
		this.getInstanceCatalogSnapshot = getInstanceCatalogSnapshot;
		this.uiDispatcher = uiDispatcher;
		this.logger = logger;
		Builder = new ResourcesAvailableVersionListBuilder(options);
		ResetFilterOptions();
	}

	public void SetProject(ResourcesModProjectItemViewModel project)
	{
		currentProject = project;
		SelectedTarget = null;
		CancelVersionsLoad();
		ResetVersions(resetFilters: true);
		LoadTargetsAsync();
	}

	public void Reset()
	{
		currentProject = null;
		SelectedTarget = null;
		CancelTargetsLoad();
		CancelVersionsLoad();
		InstallTargets.Clear();
		TargetsLoadErrorMessage = string.Empty;
		IsLoadingTargets = false;
		IsUnknownInstanceVersionDialogOpen = false;
		ResetVersions(resetFilters: true);
		RaiseTargetStateChanged();
	}

	[RelayCommand]
	private void SelectTarget(ResourcesModInstallTargetItemViewModel? target)
	{
		if (target != null && currentProject != null && resourceCatalogService != null)
		{
			SelectedTarget = target;
			IsUnknownInstanceVersionDialogOpen = ResourcesAvailableVersionListBuilder.IsUnknownInstanceVersionTarget(target);
			TargetSelected?.Invoke(target);
			RefreshAsync();
		}
	}

	[RelayCommand]
	public Task RefreshAsync()
	{
		ResourcesModInstallTargetItemViewModel resourcesModInstallTargetItemViewModel = SelectedTarget;
		if (resourceCatalogService == null || currentProject == null || resourcesModInstallTargetItemViewModel == null)
		{
			return Task.CompletedTask;
		}
		return LoadVersionsAsync(resourcesModInstallTargetItemViewModel);
	}

	[RelayCommand]
	private void InstallVersion(ResourcesModVersionItemViewModel? item)
	{
		if (item != null)
		{
			InstallRequested?.Invoke(item);
		}
	}

	[RelayCommand]
	private void CloseUnknownInstanceVersionDialog()
	{
		IsUnknownInstanceVersionDialogOpen = false;
	}

	public void BeginLoadMore()
	{
		if (resourceCatalogService != null && currentProject != null && SelectedTarget != null && HasMore && !IsLoading && !IsLoadingMore)
		{
			LoadMoreAsync();
		}
	}

	[RelayCommand]
	public async Task LoadMoreAsync()
	{
		ResourcesModProjectItemViewModel resourcesModProjectItemViewModel = currentProject;
		if (resourceCatalogService == null || resourcesModProjectItemViewModel == null || SelectedTarget == null || !HasMore || IsLoading || IsLoadingMore)
		{
			return;
		}
		CancellationToken cancellationToken = versionsCancellation?.Token ?? CancellationToken.None;
		IsLoadingMore = true;
		LoadMoreMessage = options.VersionsLoadingMoreText;
		UpdateFooter();
		RaiseStateChanged();
		try
		{
			AvailableVersionPage page = await LoadNextPageAsync(resourcesModProjectItemViewModel.Project, SelectedTarget, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			cancellationToken.ThrowIfCancellationRequested();
			uiDispatcher.Invoke(delegate
			{
				ApplyMore(page, cancellationToken);
			});
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			if (!cancellationToken.IsCancellationRequested)
			{
				uiDispatcher.Invoke(delegate
				{
					IsLoadingMore = false;
					LoadMoreMessage = options.VersionsLoadMoreErrorText;
					UpdateFooter();
					RaiseStateChanged();
				});
				logger?.LogError(exception, "Failed to load more resource versions. Kind={Kind}", options.Kind);
			}
		}
	}

	public void Dispose()
	{
		CancelTargetsLoad();
		CancelVersionsLoad();
	}

	private Task LoadTargetsAsync()
	{
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		CancellationTokenSource? cancellationTokenSource2 = Interlocked.Exchange(ref targetsCancellation, cancellationTokenSource);
		cancellationTokenSource2?.Cancel();
		cancellationTokenSource2?.Dispose();
		CancellationToken cancellationToken = cancellationTokenSource.Token;
		uiDispatcher.Invoke(delegate
		{
			InstallTargets.Clear();
			TargetsLoadErrorMessage = string.Empty;
			IsLoadingTargets = true;
			RaiseTargetStateChanged();
		});
		try
		{
			IReadOnlyList<GameInstance> instances = Array.Empty<GameInstance>();
			if (getInstanceCatalogSnapshot != null && options.InstallTargetMode == ResourcesOnlineProjectInstallTargetMode.ExistingInstance)
			{
				uiDispatcher.Invoke(delegate
				{
					instances = getInstanceCatalogSnapshot();
				});
			}
			cancellationToken.ThrowIfCancellationRequested();
			uiDispatcher.Invoke(delegate
			{
				ApplyTargets(instances, string.Empty, cancellationToken);
			});
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.CompletedTask;
			}
			uiDispatcher.Invoke(delegate
			{
				ApplyTargets(Array.Empty<GameInstance>(), options.InstallTargetsLoadErrorText, cancellationToken);
			});
			logger?.LogError(exception, "Failed to load resource install targets. Kind={Kind}", options.Kind);
		}
		return Task.CompletedTask;
	}

	private void ApplyTargets(IReadOnlyList<GameInstance> instances, string error, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return;
		}
		List<ResourcesModInstallTargetItemViewModel> list = instances.Where((GameInstance instance) => options.Kind != ResourceProjectKind.Mod || instance.Loader != LoaderKind.Vanilla).Select(ResourcesModInstallTargetItemViewModel.FromInstance).ToList();
		if (options.InstallTargetMode == ResourcesOnlineProjectInstallTargetMode.NewInstance)
		{
			int num = 3;
			List<ResourcesModInstallTargetItemViewModel> list2 = new List<ResourcesModInstallTargetItemViewModel>(num);
			CollectionsMarshal.SetCount(list2, num);
			Span<ResourcesModInstallTargetItemViewModel> span = CollectionsMarshal.AsSpan(list2);
			span[0] = ResourcesModInstallTargetItemViewModel.CreateNewInstanceInstall(options.InstallTargetNewInstanceText ?? options.InstallTargetSectionText);
			span[1] = ResourcesModInstallTargetItemViewModel.CreateServerInstall(options.InstallTargetServerText ?? Strings.Resources_ModpackInstallTargetServer);
			span[2] = ResourcesModInstallTargetItemViewModel.CreateLocalDownload(options.InstallTargetLocalText);
			list = list2;
		}
		else
		{
			list.Add(ResourcesModInstallTargetItemViewModel.CreateLocalDownload(options.InstallTargetLocalText));
		}
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			list[num2].SetVisiblePosition(num2 == 0, num2 == list.Count - 1);
		}
		InstallTargets.Clear();
		foreach (ResourcesModInstallTargetItemViewModel item in list)
		{
			InstallTargets.Add(item);
		}
		TargetsLoadErrorMessage = error;
		IsLoadingTargets = false;
		RaiseTargetStateChanged();
		if (InstallTargets.Count > 0)
		{
			InstallTargetsEntranceAnimationToken++;
		}
	}

	private async Task LoadVersionsAsync(ResourcesModInstallTargetItemViewModel target)
	{
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		CancellationTokenSource? cancellationTokenSource2 = Interlocked.Exchange(ref versionsCancellation, cancellationTokenSource);
		cancellationTokenSource2?.Cancel();
		cancellationTokenSource2?.Dispose();
		CancellationToken cancellationToken = cancellationTokenSource.Token;
		uiDispatcher.Invoke(delegate
		{
			ResetVersions(resetFilters: true);
			ListItems.Add(new ResourcesModVersionListHeaderItem(Title));
			IsLoading = true;
			RaiseStateChanged();
		});
		try
		{
			ResourcesModProjectItemViewModel resourcesModProjectItemViewModel = currentProject;
			if (resourceCatalogService == null || resourcesModProjectItemViewModel == null || target != SelectedTarget || (!target.IsLocalDownload && !target.IsNewInstanceInstall && !target.IsServerInstall && target.Instance == null))
			{
				uiDispatcher.Invoke(delegate
				{
					ApplyInitial(new AvailableVersionPage(new ResourceProjectVersionsResult(), Array.Empty<ResourceProjectVersion>()), options.VersionsLoadErrorText, cancellationToken);
				});
				return;
			}
			AvailableVersionPage page = await LoadNextPageAsync(resourcesModProjectItemViewModel.Project, target, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			cancellationToken.ThrowIfCancellationRequested();
			string error = (page.Result.IsCurseForgeUnavailable ? options.VersionsLoadErrorText : string.Empty);
			uiDispatcher.Invoke(delegate
			{
				ApplyInitial(page, error, cancellationToken);
			});
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			if (!cancellationToken.IsCancellationRequested)
			{
				uiDispatcher.Invoke(delegate
				{
					ApplyInitial(new AvailableVersionPage(new ResourceProjectVersionsResult(), Array.Empty<ResourceProjectVersion>()), options.VersionsLoadErrorText, cancellationToken);
				});
				logger?.LogError(exception, "Failed to load resource project versions. Kind={Kind}", options.Kind);
			}
		}
	}

	private async Task<AvailableVersionPage> LoadNextPageAsync(ResourceProject project, ResourcesModInstallTargetItemViewModel target, CancellationToken cancellationToken)
	{
		ResourceProjectVersionsRequest request = new ResourceProjectVersionsRequest
		{
			Kind = options.Kind,
			Source = project.Source,
			ProjectId = project.ProjectId,
			Slug = project.Slug,
			MinecraftVersion = string.Empty,
			Loader = LoaderKind.Vanilla,
			IncludeAllVersions = true,
			ForServerInstallation = target.IsServerInstall,
			Offset = NextPageOffset,
			PageSize = 10000
		};
		ResourceProjectVersionsResult resourceProjectVersionsResult = await resourceCatalogService.GetProjectVersionsAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		IReadOnlyList<ResourceProjectVersion> versions = AcceptPage(resourceProjectVersionsResult.Versions, request.Offset, request.PageSize);
		return new AvailableVersionPage(resourceProjectVersionsResult, versions);
	}

	private IReadOnlyList<ResourceProjectVersion> AcceptPage(IReadOnlyList<ResourceProjectVersion> versions, int requestOffset, int pageSize)
	{
		List<ResourceProjectVersion> list = new List<ResourceProjectVersion>(versions.Count);
		foreach (ResourceProjectVersion version in versions)
		{
			string text = (string.IsNullOrWhiteSpace(version.VersionId) ? $"{version.FileName}|{version.VersionNumber}|{version.PublishedAt:O}" : version.VersionId);
			if (!string.IsNullOrWhiteSpace(text) && loadedVersionIds.Add(text))
			{
				list.Add(version);
			}
		}
		NextPageOffset = requestOffset + pageSize;
		return list;
	}

	private void ApplyInitial(AvailableVersionPage page, string error, CancellationToken cancellationToken)
	{
		if (!cancellationToken.IsCancellationRequested)
		{
			SourceVersions = page.Versions.ToList();
			ApplyDefaultFilters(SourceVersions, SelectedTarget);
			RebuildList();
			LoadErrorMessage = error;
			HasMore = !page.Result.IsCurseForgeUnavailable && page.Result.HasMore;
			LoadMoreMessage = (HasMore ? string.Empty : options.VersionsNoMoreText);
			IsLoading = false;
			IsLoadingMore = false;
			UpdateFooter();
			RaiseStateChanged();
		}
	}

	private void ApplyMore(AvailableVersionPage page, CancellationToken cancellationToken)
	{
		if (!cancellationToken.IsCancellationRequested)
		{
			SourceVersions = SourceVersions.Concat(page.Versions).ToList();
			UpdateFiltersPreservingSelection(SourceVersions);
			RebuildList(playEntranceAnimation: false);
			HasMore = !page.Result.IsCurseForgeUnavailable && page.Result.HasMore && page.Versions.Count > 0;
			LoadMoreMessage = (HasMore ? string.Empty : options.VersionsNoMoreText);
			IsLoadingMore = false;
			UpdateFooter();
			RaiseStateChanged();
		}
	}

	private void ResetVersions(bool resetFilters)
	{
		SourceVersions = Array.Empty<ResourceProjectVersion>();
		NextPageOffset = 0;
		loadedVersionIds.Clear();
		ListItems.Clear();
		VisibleVersionCount = 0;
		HasMore = false;
		IsLoading = false;
		IsLoadingMore = false;
		LoadErrorMessage = string.Empty;
		LoadMoreMessage = string.Empty;
		if (resetFilters)
		{
			try
			{
				isApplyingFilters = true;
				SearchQuery = string.Empty;
				ResetFilterOptions();
			}
			finally
			{
				isApplyingFilters = false;
			}
		}
		RaiseStateChanged();
	}

	private void ResetFilterOptions()
	{
		VersionFilterOptions.Clear();
		VersionFilterOptions.Add(Builder.CreateAllVersionFilterOption());
		LoaderFilterOptions.Clear();
		foreach (ResourcesFilterOptionItem item in Builder.CreateDefaultLoaderFilterOptions())
		{
			LoaderFilterOptions.Add(item);
		}
		SelectedVersionFilter = VersionFilterOptions[0];
		SelectedLoaderFilter = LoaderFilterOptions[0];
	}

	private void ApplyDefaultFilters(IReadOnlyList<ResourceProjectVersion> versions, ResourcesModInstallTargetItemViewModel? target)
	{
		ApplyFilters(versions, Builder.ResolveDefaultVersionFilterId(target), Builder.ResolveDefaultLoaderFilterId(target));
	}

	private void UpdateFiltersPreservingSelection(IReadOnlyList<ResourceProjectVersion> versions)
	{
		ApplyFilters(versions, SelectedVersionFilter?.Id ?? "all", SelectedLoaderFilter?.Id ?? "all");
	}

	private void ApplyFilters(IReadOnlyList<ResourceProjectVersion> versions, string versionId, string loaderId)
	{
		try
		{
			isApplyingFilters = true;
			VersionFilterOptions.Clear();
			VersionFilterOptions.Add(Builder.CreateAllVersionFilterOption());
			foreach (ResourcesFilterOptionItem item in Builder.CreateVersionFilterOptions(versions))
			{
				VersionFilterOptions.Add(item);
			}
			LoaderFilterOptions.Clear();
			foreach (ResourcesFilterOptionItem item2 in Builder.CreateLoaderFilterOptions(versions))
			{
				LoaderFilterOptions.Add(item2);
			}
			EnsureFilter(VersionFilterOptions, versionId, (string id) => id);
			EnsureFilter(LoaderFilterOptions, loaderId, Builder.GetLoaderTitle);
			SelectedVersionFilter = VersionFilterOptions.First((ResourcesFilterOptionItem option) => string.Equals(option.Id, versionId, StringComparison.OrdinalIgnoreCase));
			SelectedLoaderFilter = LoaderFilterOptions.First((ResourcesFilterOptionItem option) => string.Equals(option.Id, loaderId, StringComparison.OrdinalIgnoreCase));
		}
		finally
		{
			isApplyingFilters = false;
		}
	}

	private static void EnsureFilter(ICollection<ResourcesFilterOptionItem> values, string id, Func<string, string> titleFactory)
	{
		if (!string.IsNullOrWhiteSpace(id) && !values.Any((ResourcesFilterOptionItem value) => string.Equals(value.Id, id, StringComparison.OrdinalIgnoreCase)))
		{
			values.Add(new ResourcesFilterOptionItem
			{
				Id = id,
				Title = titleFactory(id)
			});
		}
	}

	private void RebuildList(bool playEntranceAnimation = true)
	{
		ListItems.Clear();
		AvailableVersionListBuildResult availableVersionListBuildResult = Builder.Build(SourceVersions, Title, currentProject, options.FallbackIconKey, SelectedVersionFilter?.Id, SelectedLoaderFilter?.Id, SearchQuery);
		foreach (object item in availableVersionListBuildResult.Items)
		{
			ListItems.Add(item);
		}
		VisibleVersionCount = availableVersionListBuildResult.VisibleVersionCount;
		if (playEntranceAnimation)
		{
			ListEntranceAnimationToken++;
		}
		UpdateFooter();
		RaiseStateChanged();
	}

	private void UpdateFooter()
	{
		for (int num = ListItems.Count - 1; num >= 0; num--)
		{
			if (ListItems[num] is ResourcesListFooterStatusItem)
			{
				ListItems.RemoveAt(num);
			}
		}
		if (CanShowLoadMoreState && !string.IsNullOrWhiteSpace(LoadMoreMessage))
		{
			ListItems.Add(new ResourcesListFooterStatusItem(LoadMoreMessage));
		}
	}

	private void RaiseTargetStateChanged()
	{
		OnPropertyChanged("HasInstallTargets");
		OnPropertyChanged("HasTargetsLoadErrorMessage");
		OnPropertyChanged("CanShowTargetsLoadingState");
		OnPropertyChanged("CanShowTargetsLoadErrorState");
	}

	private void RaiseStateChanged()
	{
		OnPropertyChanged("Title");
		OnPropertyChanged("HasLoadErrorMessage");
		OnPropertyChanged("HasFilters");
		OnPropertyChanged("EmptyMessage");
		OnPropertyChanged("CanShowLoadingState");
		OnPropertyChanged("CanShowEmptyState");
		OnPropertyChanged("CanShowLoadErrorState");
		OnPropertyChanged("CanShowLoadMoreState");
	}

	private void CancelTargetsLoad()
	{
		CancellationTokenSource? cancellationTokenSource = Interlocked.Exchange(ref targetsCancellation, null);
		cancellationTokenSource?.Cancel();
		cancellationTokenSource?.Dispose();
	}

	private void CancelVersionsLoad()
	{
		CancellationTokenSource? cancellationTokenSource = Interlocked.Exchange(ref versionsCancellation, null);
		cancellationTokenSource?.Cancel();
		cancellationTokenSource?.Dispose();
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedTargetChanged(ResourcesModInstallTargetItemViewModel? value)
	{
		OnPropertyChanged("Title");
		OnPropertyChanged("EmptyMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSearchQueryChanged(string value)
	{
		if (!isApplyingFilters)
		{
			RebuildList();
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedVersionFilterChanged(ResourcesFilterOptionItem? value)
	{
		if (!isApplyingFilters)
		{
			RebuildList();
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedLoaderFilterChanged(ResourcesFilterOptionItem? value)
	{
		if (!isApplyingFilters)
		{
			RebuildList();
		}
	}
}
