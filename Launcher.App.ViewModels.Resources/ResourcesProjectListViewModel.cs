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
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Launcher.App.ViewModels.Resources;

public sealed class ResourcesProjectListViewModel : ObservableObject, IDisposable
{
	private sealed record ReleaseVersionData(IReadOnlyList<string> ReleaseVersionOrder, IReadOnlyList<ResourcesFilterOptionItem> VersionOptions);

	private const int SearchDebounceMilliseconds = 250;

	private const int CatalogPageSize = 20;

	private const int InitialProjectBatchSize = 12;

	private const int AppendProjectBatchSize = 8;

	private readonly ResourcesOnlineProjectPageOptions options;

	private readonly IResourceCatalogService? resourceCatalogService;

	private readonly IResourceThumbnailService? thumbnailService;

	private readonly IGameVersionService? gameVersionService;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger? logger;

	private readonly object releaseVersionGate = new object();

	private CancellationTokenSource? requestCancellation;

	private Task<ReleaseVersionData>? releaseVersionDataTask;

	private bool hasRequestedInitialLoad;

	private bool isApplyingVersionOptions;

	private bool isApplyingInstanceFilters;

	private bool isApplyingPendingFilters;

	[ObservableProperty]
	private string searchQuery = string.Empty;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private bool isLoadingMore;

	[ObservableProperty]
	private string loadErrorMessage = string.Empty;

	[ObservableProperty]
	private string loadMoreMessage = string.Empty;

	[ObservableProperty]
	private string partialWarningMessage = string.Empty;

	[ObservableProperty]
	private int listEntranceAnimationToken;

	[ObservableProperty]
	private bool hasMore;

	[ObservableProperty]
	private int nextPageOffset;

	[ObservableProperty]
	private ResourcesFilterOptionItem? selectedVersionOption;

	[ObservableProperty]
	private ResourcesFilterOptionItem? selectedLoaderOption;

	[ObservableProperty]
	private ResourcesFilterOptionItem? selectedSourceOption;

	[ObservableProperty]
	private ResourcesFilterOptionItem? selectedTypeOption;

	[ObservableProperty]
	private ResourcesFilterOptionItem? pendingVersionOption;

	[ObservableProperty]
	private ResourcesFilterOptionItem? pendingLoaderOption;

	[ObservableProperty]
	private ResourcesFilterOptionItem? pendingSourceOption;

	[ObservableProperty]
	private ResourcesFilterOptionItem? pendingTypeOption;

	[ObservableProperty]
	private bool isFilterDialogOpen;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? refreshCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand? loadMoreCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ResourcesModProjectItemViewModel?>? selectProjectCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? openFilterDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? cancelFilterDialogCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? confirmFilterDialogCommand;

	public ObservableCollection<ResourcesFilterOptionItem> VersionOptions { get; }

	public ObservableCollection<ResourcesFilterOptionItem> LoaderOptions { get; }

	public ObservableCollection<ResourcesFilterOptionItem> SourceOptions { get; }

	public ObservableCollection<ResourcesFilterOptionItem> TypeOptions { get; }

	public ObservableCollection<ResourcesModProjectItemViewModel> VisibleProjects { get; } = new ObservableCollection<ResourcesModProjectItemViewModel>();

	public ObservableCollection<object> ListItems { get; } = new ObservableCollection<object>();

	public bool ShowsLoaderFilters => options.ShowsLoaderFilters;

	public bool ShowsSourceFilters => SourceOptions.Count > 1;

	public string LoadingMessage => options.ProjectsLoadingText;

	public string EmptyMessage => options.ProjectsEmptyText;

	public bool HasVisibleProjects => VisibleProjects.Count > 0;

	public bool HasLoadErrorMessage => !string.IsNullOrWhiteSpace(LoadErrorMessage);

	public bool HasPartialWarningMessage => !string.IsNullOrWhiteSpace(PartialWarningMessage);

	public bool CanShowLoadingState
	{
		get
		{
			if (IsLoading)
			{
				return !HasVisibleProjects;
			}
			return false;
		}
	}

	public bool CanShowEmptyState
	{
		get
		{
			if (!IsLoading && !HasVisibleProjects)
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
			if (!IsLoading && !HasVisibleProjects)
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
			if (HasVisibleProjects)
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
	public string PartialWarningMessage
	{
		get
		{
			return partialWarningMessage;
		}
		[MemberNotNull("partialWarningMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(partialWarningMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PartialWarningMessage);
				partialWarningMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PartialWarningMessage);
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
	public int NextPageOffset
	{
		get
		{
			return nextPageOffset;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(nextPageOffset, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.NextPageOffset);
				nextPageOffset = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.NextPageOffset);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesFilterOptionItem? SelectedVersionOption
	{
		get
		{
			return selectedVersionOption;
		}
		set
		{
			if (!EqualityComparer<ResourcesFilterOptionItem>.Default.Equals(selectedVersionOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedVersionOption);
				selectedVersionOption = value;
				OnSelectedVersionOptionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedVersionOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesFilterOptionItem? SelectedLoaderOption
	{
		get
		{
			return selectedLoaderOption;
		}
		set
		{
			if (!EqualityComparer<ResourcesFilterOptionItem>.Default.Equals(selectedLoaderOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedLoaderOption);
				selectedLoaderOption = value;
				OnSelectedLoaderOptionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedLoaderOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesFilterOptionItem? SelectedSourceOption
	{
		get
		{
			return selectedSourceOption;
		}
		set
		{
			if (!EqualityComparer<ResourcesFilterOptionItem>.Default.Equals(selectedSourceOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedSourceOption);
				selectedSourceOption = value;
				OnSelectedSourceOptionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedSourceOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesFilterOptionItem? SelectedTypeOption
	{
		get
		{
			return selectedTypeOption;
		}
		set
		{
			if (!EqualityComparer<ResourcesFilterOptionItem>.Default.Equals(selectedTypeOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedTypeOption);
				selectedTypeOption = value;
				OnSelectedTypeOptionChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedTypeOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesFilterOptionItem? PendingVersionOption
	{
		get
		{
			return pendingVersionOption;
		}
		set
		{
			if (!EqualityComparer<ResourcesFilterOptionItem>.Default.Equals(pendingVersionOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PendingVersionOption);
				pendingVersionOption = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PendingVersionOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesFilterOptionItem? PendingLoaderOption
	{
		get
		{
			return pendingLoaderOption;
		}
		set
		{
			if (!EqualityComparer<ResourcesFilterOptionItem>.Default.Equals(pendingLoaderOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PendingLoaderOption);
				pendingLoaderOption = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PendingLoaderOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesFilterOptionItem? PendingSourceOption
	{
		get
		{
			return pendingSourceOption;
		}
		set
		{
			if (!EqualityComparer<ResourcesFilterOptionItem>.Default.Equals(pendingSourceOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PendingSourceOption);
				pendingSourceOption = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PendingSourceOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesFilterOptionItem? PendingTypeOption
	{
		get
		{
			return pendingTypeOption;
		}
		set
		{
			if (!EqualityComparer<ResourcesFilterOptionItem>.Default.Equals(pendingTypeOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PendingTypeOption);
				pendingTypeOption = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PendingTypeOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsFilterDialogOpen
	{
		get
		{
			return isFilterDialogOpen;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isFilterDialogOpen, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsFilterDialogOpen);
				isFilterDialogOpen = value;
				OnIsFilterDialogOpenChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsFilterDialogOpen);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand RefreshCommand => refreshCommand ?? (refreshCommand = new AsyncRelayCommand(RefreshAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand LoadMoreCommand => loadMoreCommand ?? (loadMoreCommand = new AsyncRelayCommand(LoadMoreAsync));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ResourcesModProjectItemViewModel?> SelectProjectCommand => selectProjectCommand ?? (selectProjectCommand = new RelayCommand<ResourcesModProjectItemViewModel>(SelectProject));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand OpenFilterDialogCommand => openFilterDialogCommand ?? (openFilterDialogCommand = new RelayCommand(OpenFilterDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand CancelFilterDialogCommand => cancelFilterDialogCommand ?? (cancelFilterDialogCommand = new RelayCommand(CancelFilterDialog));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ConfirmFilterDialogCommand => confirmFilterDialogCommand ?? (confirmFilterDialogCommand = new RelayCommand(ConfirmFilterDialog));

	public event Action<ResourcesModProjectItemViewModel>? ProjectSelected;

	public event Action? NavigationResetRequested;

	internal ResourcesProjectListViewModel(ResourcesOnlineProjectPageOptions options, IResourceCatalogService? resourceCatalogService, IGameVersionService? gameVersionService, IUiDispatcher uiDispatcher, ILogger? logger)
	{
		this.options = options;
		this.resourceCatalogService = resourceCatalogService;
		thumbnailService = resourceCatalogService as IResourceThumbnailService;
		this.gameVersionService = gameVersionService;
		this.uiDispatcher = uiDispatcher;
		this.logger = logger;
		VersionOptions = new ObservableCollection<ResourcesFilterOptionItem>
		{
			new ResourcesFilterOptionItem
			{
				Id = "all",
				Title = options.AllVersionsText
			}
		};
		LoaderOptions = CreateLoaderOptions(options);
		SourceOptions = CreateSourceOptions(options);
		TypeOptions = CreateTypeOptions(options);
		selectedVersionOption = VersionOptions[0];
		selectedLoaderOption = LoaderOptions[0];
		selectedSourceOption = SourceOptions[0];
		selectedTypeOption = TypeOptions[0];
	}

	public void BeginEnsureLoaded()
	{
		BeginEnsureVersionOptionsLoaded();
		if (!hasRequestedInitialLoad && resourceCatalogService != null)
		{
			Observe(RefreshAsync(), "load initial resource projects");
		}
	}

	public void BeginLoadMore()
	{
		if (resourceCatalogService != null && HasVisibleProjects && HasMore && !IsLoading && !IsLoadingMore)
		{
			Observe(LoadMoreAsync(), "load more resource projects");
		}
	}

	[RelayCommand]
	public Task RefreshAsync()
	{
		if (resourceCatalogService == null)
		{
			return Task.CompletedTask;
		}
		CancellationToken cancellationToken = BeginRequest();
		return LoadAsync(CreateSearchRequest(0), append: false, cancellationToken);
	}

	[RelayCommand]
	public Task LoadMoreAsync()
	{
		if (resourceCatalogService == null || !HasVisibleProjects || !HasMore || IsLoading || IsLoadingMore)
		{
			return Task.CompletedTask;
		}
		IsLoadingMore = true;
		LoadMoreMessage = options.ProjectsLoadingMoreText;
		UpdateFooter();
		return LoadAsync(CreateSearchRequest(NextPageOffset), append: true, requestCancellation?.Token ?? CancellationToken.None);
	}

	[RelayCommand]
	private void SelectProject(ResourcesModProjectItemViewModel? project)
	{
		if (project != null)
		{
			ProjectSelected?.Invoke(project);
		}
	}

	[RelayCommand]
	private void OpenFilterDialog()
	{
		PendingVersionOption = SelectedVersionOption;
		PendingLoaderOption = SelectedLoaderOption;
		PendingSourceOption = SelectedSourceOption;
		PendingTypeOption = SelectedTypeOption;
		IsFilterDialogOpen = true;
	}

	[RelayCommand]
	private void CancelFilterDialog()
	{
		IsFilterDialogOpen = false;
	}

	[RelayCommand]
	private void ConfirmFilterDialog()
	{
		bool flag = PendingVersionOption != SelectedVersionOption || PendingLoaderOption != SelectedLoaderOption || PendingSourceOption != SelectedSourceOption || PendingTypeOption != SelectedTypeOption;
		try
		{
			isApplyingPendingFilters = true;
			ApplyPendingOption(PendingVersionOption, SelectedVersionOption, delegate(ResourcesFilterOptionItem? value)
			{
				SelectedVersionOption = value;
			});
			ApplyPendingOption(PendingLoaderOption, SelectedLoaderOption, delegate(ResourcesFilterOptionItem? value)
			{
				SelectedLoaderOption = value;
			});
			ApplyPendingOption(PendingSourceOption, SelectedSourceOption, delegate(ResourcesFilterOptionItem? value)
			{
				SelectedSourceOption = value;
			});
			ApplyPendingOption(PendingTypeOption, SelectedTypeOption, delegate(ResourcesFilterOptionItem? value)
			{
				SelectedTypeOption = value;
			});
		}
		finally
		{
			isApplyingPendingFilters = false;
		}
		IsFilterDialogOpen = false;
		if (flag)
		{
			NavigationResetRequested?.Invoke();
			logger?.LogDebug("Resource project filters confirmed. Kind={Kind}", options.Kind);
			ScheduleRefresh(debounce: false);
		}
	}

	public async Task ApplyInstanceFiltersAsync(GameInstance instance)
	{
		await EnsureVersionOptionsLoadedAsync().ConfigureAwait(continueOnCapturedContext: true);
		try
		{
			isApplyingInstanceFilters = true;
			SelectedVersionOption = ResolveVersionOption(instance);
			SelectedLoaderOption = ResolveLoaderOption(instance);
		}
		finally
		{
			isApplyingInstanceFilters = false;
		}
		NavigationResetRequested?.Invoke();
		logger?.LogDebug("Applied resource project filters from instance. Kind={Kind} InstanceId={InstanceId} VersionFilter={VersionFilter} LoaderFilter={LoaderFilter}", options.Kind, instance.Id, SelectedVersionOption?.Id, SelectedLoaderOption?.Id);
		await RefreshAsync().ConfigureAwait(continueOnCapturedContext: true);
	}

	public void Dispose()
	{
		CancellationTokenSource? cancellationTokenSource = Interlocked.Exchange(ref requestCancellation, null);
		cancellationTokenSource?.Cancel();
		cancellationTokenSource?.Dispose();
	}

	private void FilterChanged(string filter, ResourcesFilterOptionItem? value)
	{
		if (!isApplyingPendingFilters)
		{
			NavigationResetRequested?.Invoke();
			logger?.LogDebug("Resource project filter selected. Kind={Kind} FilterId={FilterId} OptionId={OptionId}", options.Kind, filter, value?.Id);
			ScheduleRefresh(debounce: false);
		}
	}

	private void ScheduleRefresh(bool debounce)
	{
		if (resourceCatalogService != null)
		{
			CancellationToken cancellationToken = BeginRequest();
			Observe(ScheduleRefreshAsync(cancellationToken, debounce), "refresh resource projects after query change");
		}
	}

	private async Task ScheduleRefreshAsync(CancellationToken cancellationToken, bool debounce)
	{
		_ = 1;
		try
		{
			if (debounce)
			{
				await Task.Delay(250, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			await LoadAsync(CreateSearchRequest(0), append: false, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private CancellationToken BeginRequest()
	{
		hasRequestedInitialLoad = true;
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		CancellationTokenSource? cancellationTokenSource2 = Interlocked.Exchange(ref requestCancellation, cancellationTokenSource);
		cancellationTokenSource2?.Cancel();
		cancellationTokenSource2?.Dispose();
		IsLoading = true;
		IsLoadingMore = false;
		HasMore = false;
		NextPageOffset = 0;
		LoadErrorMessage = string.Empty;
		LoadMoreMessage = string.Empty;
		PartialWarningMessage = string.Empty;
		UpdateFooter();
		RaiseStateChanged();
		return cancellationTokenSource.Token;
	}

	private async Task LoadAsync(ResourceCatalogSearchRequest request, bool append, CancellationToken cancellationToken)
	{
		try
		{
			Task<IReadOnlyList<string>?> releaseOrderTask = GetReleaseVersionOrderAsync(cancellationToken);
			ResourceCatalogSearchResult result = await resourceCatalogService.SearchProjectsAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			IReadOnlyList<string> releaseOrder = await releaseOrderTask.ConfigureAwait(continueOnCapturedContext: false);
			List<ResourcesModProjectItemViewModel> items = result.Projects.Select((ResourceProject project) => new ResourcesModProjectItemViewModel(project, releaseOrder, options.FallbackIconKey, options.TypeOptions)).ToList();
			ApplyCachedThumbnailSources(items);
			cancellationToken.ThrowIfCancellationRequested();
			await uiDispatcher.PostAfterTransitionAsync(delegate
			{
				ApplyResult(result, items, request.Offset, append, cancellationToken);
			}).ConfigureAwait(continueOnCapturedContext: false);
			if (thumbnailService != null)
			{
				Observe(RefreshThumbnailSourcesAsync(items, cancellationToken), "refresh resource project thumbnails");
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			Exception exception = ex3;
			if (!cancellationToken.IsCancellationRequested)
			{
				uiDispatcher.Invoke(delegate
				{
					ApplyFailure(exception, append, cancellationToken);
				});
			}
		}
	}

	private void ApplyCachedThumbnailSources(IEnumerable<ResourcesModProjectItemViewModel> items)
	{
		if (thumbnailService == null)
		{
			return;
		}
		foreach (ResourcesModProjectItemViewModel item in items)
		{
			try
			{
				item.SetManagedIconSource(thumbnailService.TryGetCachedThumbnailSource(item.Project));
			}
			catch (Exception exception)
			{
				item.SetManagedIconSource(null);
				logger?.LogDebug(exception, "Failed to resolve cached resource project thumbnail. Kind={Kind} Source={Source} ProjectId={ProjectId}", item.Project.Kind, item.Project.Source, item.Project.ProjectId);
			}
		}
	}

	private async Task RefreshThumbnailSourcesAsync(IReadOnlyList<ResourcesModProjectItemViewModel> items, CancellationToken cancellationToken)
	{
		if (thumbnailService == null)
		{
			return;
		}
		await Task.WhenAll(items.Where((ResourcesModProjectItemViewModel item) => !string.IsNullOrWhiteSpace(item.Project.IconUrl)).Select((Func<ResourcesModProjectItemViewModel, Task>)async delegate(ResourcesModProjectItemViewModel item)
		{
			string source = await thumbnailService.GetOrCreateThumbnailSourceAsync(item.Project, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!string.IsNullOrWhiteSpace(source) && !cancellationToken.IsCancellationRequested)
			{
				uiDispatcher.PostAfterTransition(delegate
				{
					if (!cancellationToken.IsCancellationRequested)
					{
						item.SetManagedIconSource(source);
					}
				});
			}
		})).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void ApplyResult(ResourceCatalogSearchResult result, IReadOnlyList<ResourcesModProjectItemViewModel> items, int offset, bool append, CancellationToken cancellationToken)
	{
		if (!cancellationToken.IsCancellationRequested)
		{
			if (!append)
			{
				VisibleProjects.Clear();
				ListItems.Clear();
			}
			NextPageOffset = offset + 20;
			HasMore = result.HasMore && items.Count > 0;
			LoadMoreMessage = ((HasMore || items.Count == 0) ? string.Empty : options.ProjectsNoMoreText);
			PartialWarningMessage = (result.IsCurseForgeApiKeyMissing ? options.CurseForgeMissingApiKeyText : string.Empty);
			int num = (append ? 8 : 12);
			AddBatch(items, 0, num);
			if (!append)
			{
				ListEntranceAnimationToken++;
			}
			if (items.Count > num)
			{
				Observe(AppendRemainingBatchesAsync(items, num, cancellationToken), "append resource project batches");
			}
			IsLoading = false;
			IsLoadingMore = false;
			UpdateFooter();
			RaiseStateChanged();
			logger?.LogDebug(append ? "Resource projects appended. Kind={Kind} ResultCount={ResultCount}" : "Resource projects loaded. Kind={Kind} ResultCount={ResultCount}", options.Kind, items.Count);
		}
	}

	private async Task AppendRemainingBatchesAsync(IReadOnlyList<ResourcesModProjectItemViewModel> items, int startIndex, CancellationToken cancellationToken)
	{
		for (int index = startIndex; index < items.Count; index += 8)
		{
			await Task.Yield();
			cancellationToken.ThrowIfCancellationRequested();
			int batchStart = index;
			uiDispatcher.PostAfterTransition(delegate
			{
				if (!cancellationToken.IsCancellationRequested)
				{
					AddBatch(items, batchStart, 8);
				}
			});
		}
	}

	private void AddBatch(IReadOnlyList<ResourcesModProjectItemViewModel> items, int startIndex, int count)
	{
		RemoveFooter();
		foreach (ResourcesModProjectItemViewModel item in items.Skip(startIndex).Take(count))
		{
			VisibleProjects.Add(item);
			ListItems.Add(item);
		}
		UpdateFooter();
		RaiseStateChanged();
	}

	private void ApplyFailure(Exception exception, bool append, CancellationToken cancellationToken)
	{
		if (!cancellationToken.IsCancellationRequested)
		{
			if (append)
			{
				IsLoadingMore = false;
				LoadMoreMessage = options.ProjectsLoadMoreErrorText;
			}
			else
			{
				hasRequestedInitialLoad = false;
				IsLoading = false;
				LoadErrorMessage = options.ProjectsLoadErrorText;
			}
			UpdateFooter();
			RaiseStateChanged();
			logger?.LogError(exception, "Failed to load resource projects. Kind={Kind} Append={Append}", options.Kind, append);
		}
	}

	private ResourceCatalogSearchRequest CreateSearchRequest(int offset)
	{
		IReadOnlyList<string> readOnlyList = ResolveMinecraftVersions(SelectedVersionOption);
		string text = SelectedSourceOption?.Id;
		// 注：Loader/Source/Category/Offset/PageSize 都是 init-only，
		// 必须在对象初始化器里一次性赋值（反编译原本展开成了逐条赋值，不合法）。
		return new ResourceCatalogSearchRequest
		{
			Kind = options.Kind,
			Query = SearchQuery,
			MinecraftVersion = ((readOnlyList.Count == 1) ? readOnlyList[0] : string.Empty),
			MinecraftVersions = readOnlyList,
			Loader = (options.ShowsLoaderFilters ? (SelectedLoaderOption?.Id switch
			{
				"fabric" => LoaderKind.Fabric, 
				"forge" => LoaderKind.Forge, 
				"neoforge" => LoaderKind.NeoForge, 
				"quilt" => LoaderKind.Quilt, 
				_ => LoaderKind.Vanilla, 
			}) : LoaderKind.Vanilla),
			Source = ((text == "modrinth") ? new ResourceProjectSource?(ResourceProjectSource.Modrinth) : ((!(text == "curseforge")) ? ((ResourceProjectSource?)null) : new ResourceProjectSource?(ResourceProjectSource.CurseForge))),
			Category = ResolveCategory(SelectedTypeOption),
			Offset = offset,
			PageSize = 20
		};
	}

	private async Task<IReadOnlyList<string>?> GetReleaseVersionOrderAsync(CancellationToken cancellationToken)
	{
		return (await GetReleaseVersionDataAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).ReleaseVersionOrder;
	}

	private void BeginEnsureVersionOptionsLoaded()
	{
		if (gameVersionService != null)
		{
			Observe(GetReleaseVersionDataAsync(CancellationToken.None), "load resource version filters");
		}
	}

	private async Task EnsureVersionOptionsLoadedAsync()
	{
		ReleaseVersionData data = await GetReleaseVersionDataAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
		if (data.VersionOptions.Count > 0)
		{
			uiDispatcher.Invoke(delegate
			{
				ApplyVersionOptions(data.VersionOptions);
			});
		}
	}

	private async Task<ReleaseVersionData> GetReleaseVersionDataAsync(CancellationToken cancellationToken)
	{
		if (gameVersionService == null)
		{
			return new ReleaseVersionData(Array.Empty<string>(), Array.Empty<ResourcesFilterOptionItem>());
		}
		Task<ReleaseVersionData> task;
		lock (releaseVersionGate)
		{
			if (releaseVersionDataTask == null)
			{
				releaseVersionDataTask = LoadReleaseVersionDataAsync();
			}
			task = releaseVersionDataTask;
		}
		return await task.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task<ReleaseVersionData> LoadReleaseVersionDataAsync()
	{
		try
		{
			List<string> list = (from version in await gameVersionService.GetVersionsAsync().ConfigureAwait(continueOnCapturedContext: false)
				where string.Equals(version.Type, "release", StringComparison.OrdinalIgnoreCase)
				select version.Name into version
				where !string.IsNullOrWhiteSpace(version)
				select version).ToList();
			IReadOnlyList<ResourcesFilterOptionItem> filterOptions = CreateVersionFilterOptions(list);
			uiDispatcher.Post(delegate
			{
				ApplyVersionOptions(filterOptions);
			});
			return new ReleaseVersionData(list, filterOptions);
		}
		catch (Exception exception)
		{
			logger?.LogWarning(exception, "Failed to load Minecraft release versions for resource filters. Kind={Kind}", options.Kind);
			return new ReleaseVersionData(Array.Empty<string>(), Array.Empty<ResourcesFilterOptionItem>());
		}
	}

	private void ApplyVersionOptions(IReadOnlyList<ResourcesFilterOptionItem> values)
	{
		if (values.Count == 0)
		{
			return;
		}
		string selectedId = SelectedVersionOption?.Id ?? "all";
		VersionOptions.Clear();
		VersionOptions.Add(new ResourcesFilterOptionItem
		{
			Id = "all",
			Title = options.AllVersionsText
		});
		foreach (ResourcesFilterOptionItem value in values)
		{
			VersionOptions.Add(value);
		}
		try
		{
			isApplyingVersionOptions = true;
			SelectedVersionOption = VersionOptions.FirstOrDefault((ResourcesFilterOptionItem option) => string.Equals(option.Id, selectedId, StringComparison.OrdinalIgnoreCase)) ?? VersionOptions[0];
		}
		finally
		{
			isApplyingVersionOptions = false;
		}
	}

	private ResourcesFilterOptionItem ResolveVersionOption(GameInstance instance)
	{
		ResourcesFilterOptionItem resourcesFilterOptionItem = VersionOptions.FirstOrDefault((ResourcesFilterOptionItem option) => option.Id == "all") ?? VersionOptions[0];
		if (string.IsNullOrWhiteSpace(instance.MinecraftVersion))
		{
			return resourcesFilterOptionItem;
		}
		return VersionOptions.FirstOrDefault((ResourcesFilterOptionItem option) => option.Id != "all" && (string.Equals(option.Id, instance.MinecraftVersion, StringComparison.OrdinalIgnoreCase) || option.MinecraftVersions.Contains<string>(instance.MinecraftVersion, StringComparer.OrdinalIgnoreCase))) ?? resourcesFilterOptionItem;
	}

	private ResourcesFilterOptionItem ResolveLoaderOption(GameInstance instance)
	{
		string id = instance.Loader switch
		{
			LoaderKind.Fabric => "fabric", 
			LoaderKind.Forge => "forge", 
			LoaderKind.NeoForge => "neoforge", 
			LoaderKind.Quilt => "quilt", 
			_ => "all", 
		};
		return LoaderOptions.FirstOrDefault((ResourcesFilterOptionItem option) => string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase)) ?? LoaderOptions[0];
	}

	private void UpdateFooter()
	{
		RemoveFooter();
		if (CanShowLoadMoreState && !string.IsNullOrWhiteSpace(LoadMoreMessage))
		{
			ListItems.Add(new ResourcesListFooterStatusItem(LoadMoreMessage));
		}
	}

	private void RemoveFooter()
	{
		for (int num = ListItems.Count - 1; num >= 0; num--)
		{
			if (ListItems[num] is ResourcesListFooterStatusItem)
			{
				ListItems.RemoveAt(num);
			}
		}
	}

	private void RaiseStateChanged()
	{
		OnPropertyChanged("HasVisibleProjects");
		OnPropertyChanged("HasLoadErrorMessage");
		OnPropertyChanged("HasPartialWarningMessage");
		OnPropertyChanged("CanShowLoadingState");
		OnPropertyChanged("CanShowEmptyState");
		OnPropertyChanged("CanShowLoadErrorState");
		OnPropertyChanged("CanShowLoadMoreState");
	}

	private void Observe(Task task, string operation)
	{
		ObserveAsync(task, operation);
	}

	private async Task ObserveAsync(Task task, string operation)
	{
		try
		{
			await task.ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			logger?.LogError(exception, "Unhandled resource list operation failure. Operation={Operation} Kind={Kind}", operation, options.Kind);
		}
	}

	private static void ApplyPendingOption(ResourcesFilterOptionItem? pending, ResourcesFilterOptionItem? current, Action<ResourcesFilterOptionItem?> apply)
	{
		if (pending != current)
		{
			apply(pending);
		}
	}

	private static IReadOnlyList<string> ResolveMinecraftVersions(ResourcesFilterOptionItem? option)
	{
		if (option == null || option.Id == "all")
		{
			return Array.Empty<string>();
		}
		if (option.MinecraftVersions.Count <= 0)
		{
			return new global::_003C_003Ez__ReadOnlySingleElementList<string>(option.Id);
		}
		return option.MinecraftVersions;
	}

	private ResourceProjectCategory? ResolveCategory(ResourcesFilterOptionItem? option)
	{
		if (option == null || option.Id == "all")
		{
			return null;
		}
		return options.TypeOptions.FirstOrDefault((ResourcesOnlineProjectTypeOption value) => string.Equals(value.Id, option.Id, StringComparison.OrdinalIgnoreCase))?.Category;
	}

	private static IReadOnlyList<ResourcesFilterOptionItem> CreateVersionFilterOptions(IReadOnlyList<string> releases)
	{
		return (from @group in (from version in releases
				select new
				{
					Original = version.Trim(),
					Major = TryNormalizeMajorMinecraftVersion(version)
				} into version
				where !string.IsNullOrWhiteSpace(version.Original) && version.Major != null
				select version).GroupBy(version => version.Major, StringComparer.OrdinalIgnoreCase)
			select new ResourcesFilterOptionItem
			{
				Id = @group.Key,
				Title = @group.Key,
				MinecraftVersions = @group.Select(value => value.Original).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList()
			}).ToList();
	}

	private static string? TryNormalizeMajorMinecraftVersion(string? version)
	{
		if (string.IsNullOrWhiteSpace(version))
		{
			return null;
		}
		string[] array = version.Trim().Split('.');
		if (array.Length == 1 && int.TryParse(array[0], out var result))
		{
			return result.ToString();
		}
		if (array.Length < 2 || !TryParseLeadingNumber(array[0], out var number) || !TryParseLeadingNumber(array[1], out var number2))
		{
			return null;
		}
		if (number != 1)
		{
			return number.ToString();
		}
		return $"{number}.{number2}";
	}

	private static bool TryParseLeadingNumber(string value, out int number)
	{
		return int.TryParse(value[..value.TakeWhile(char.IsDigit).Count()], out number);
	}

	private static ObservableCollection<ResourcesFilterOptionItem> CreateLoaderOptions(ResourcesOnlineProjectPageOptions options)
	{
		if (!options.ShowsLoaderFilters)
		{
			return new ObservableCollection<ResourcesFilterOptionItem>
			{
				new ResourcesFilterOptionItem
				{
					Id = "all",
					Title = options.AllLoadersText
				}
			};
		}
		return new ObservableCollection<ResourcesFilterOptionItem>
		{
			new ResourcesFilterOptionItem
			{
				Id = "all",
				Title = options.AllLoadersText
			},
			new ResourcesFilterOptionItem
			{
				Id = "fabric",
				Title = Strings.Download_FabricLoaderTitle
			},
			new ResourcesFilterOptionItem
			{
				Id = "forge",
				Title = Strings.Download_ForgeLoaderTitle
			},
			new ResourcesFilterOptionItem
			{
				Id = "neoforge",
				Title = Strings.Download_NeoForgeLoaderTitle
			},
			new ResourcesFilterOptionItem
			{
				Id = "quilt",
				Title = Strings.Download_QuiltLoaderTitle
			}
		};
	}

	private static ObservableCollection<ResourcesFilterOptionItem> CreateSourceOptions(ResourcesOnlineProjectPageOptions options)
	{
		IReadOnlyList<ResourcesFilterOptionItem> sourceOptions = options.SourceOptions;
		if (sourceOptions != null && sourceOptions.Count > 0)
		{
			ObservableCollection<ResourcesFilterOptionItem> observableCollection = new ObservableCollection<ResourcesFilterOptionItem>();
			{
				foreach (ResourcesFilterOptionItem item in sourceOptions)
				{
					observableCollection.Add(item);
				}
				return observableCollection;
			}
		}
		return new ObservableCollection<ResourcesFilterOptionItem>
		{
			new ResourcesFilterOptionItem
			{
				Id = "all",
				Title = Strings.Resources_ModFilterAllSources
			},
			new ResourcesFilterOptionItem
			{
				Id = "modrinth",
				Title = Strings.Resources_ModSourceModrinth
			},
			new ResourcesFilterOptionItem
			{
				Id = "curseforge",
				Title = Strings.Resources_ModSourceCurseForge
			}
		};
	}

	private static ObservableCollection<ResourcesFilterOptionItem> CreateTypeOptions(ResourcesOnlineProjectPageOptions options)
	{
		ObservableCollection<ResourcesFilterOptionItem> observableCollection = new ObservableCollection<ResourcesFilterOptionItem>
		{
			new ResourcesFilterOptionItem
			{
				Id = "all",
				Title = Strings.Resources_ModFilterAllTypes
			}
		};
		foreach (ResourcesOnlineProjectTypeOption typeOption in options.TypeOptions)
		{
			observableCollection.Add(new ResourcesFilterOptionItem
			{
				Id = typeOption.Id,
				Title = typeOption.Title
			});
		}
		return observableCollection;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSearchQueryChanged(string value)
	{
		ScheduleRefresh(debounce: true);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedVersionOptionChanged(ResourcesFilterOptionItem? value)
	{
		if (!isApplyingVersionOptions && !isApplyingInstanceFilters)
		{
			FilterChanged("version", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedLoaderOptionChanged(ResourcesFilterOptionItem? value)
	{
		if (!isApplyingInstanceFilters)
		{
			FilterChanged("loader", value);
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedSourceOptionChanged(ResourcesFilterOptionItem? value)
	{
		FilterChanged("source", value);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSelectedTypeOptionChanged(ResourcesFilterOptionItem? value)
	{
		FilterChanged("type", value);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsFilterDialogOpenChanged(bool value)
	{
		if (!value)
		{
			PendingVersionOption = null;
			PendingLoaderOption = null;
			PendingSourceOption = null;
			PendingTypeOption = null;
		}
	}
}
