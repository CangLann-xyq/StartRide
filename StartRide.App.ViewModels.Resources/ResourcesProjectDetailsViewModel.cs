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
using StartRide.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;

namespace StartRide.App.ViewModels.Resources;

public sealed class ResourcesProjectDetailsViewModel : ObservableObject, IDisposable
{
	private readonly Stack<ResourcesModProjectItemViewModel> backStack = new Stack<ResourcesModProjectItemViewModel>();

	private readonly ResourcesOnlineProjectPageOptions options;

	private readonly IResourceCatalogService? resourceCatalogService;

	private readonly IResourceThumbnailService? thumbnailService;

	private readonly IUiDispatcher uiDispatcher;

	private readonly ILogger? logger;

	private CancellationTokenSource? dependenciesCancellation;

	private CancellationTokenSource? relatedWebsiteCancellation;

	[ObservableProperty]
	private ResourcesModProjectItemViewModel? currentProject;

	[ObservableProperty]
	private bool isLoadingDependencies;

	[ObservableProperty]
	[NotifyPropertyChangedFor("HasRelatedWebsite")]
	private ResourceProjectRelatedWebsite? relatedWebsite;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<ResourcesModProjectItemViewModel?>? openDependencyCommand;

	public ObservableCollection<ResourcesModProjectItemViewModel> RequiredDependencies { get; } = new ObservableCollection<ResourcesModProjectItemViewModel>();

	public bool CanGoBackToDependencyParent => backStack.Count > 0;

	public bool HasRequiredDependencies => RequiredDependencies.Count > 0;

	public bool HasRelatedWebsite => (object)RelatedWebsite != null;

	public string InfoSectionText => options.DetailsInfoSectionText;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourcesModProjectItemViewModel? CurrentProject
	{
		get
		{
			return currentProject;
		}
		set
		{
			if (!EqualityComparer<ResourcesModProjectItemViewModel>.Default.Equals(currentProject, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentProject);
				currentProject = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentProject);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLoadingDependencies
	{
		get
		{
			return isLoadingDependencies;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isLoadingDependencies, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLoadingDependencies);
				isLoadingDependencies = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLoadingDependencies);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourceProjectRelatedWebsite? RelatedWebsite
	{
		get
		{
			return relatedWebsite;
		}
		set
		{
			if (!EqualityComparer<ResourceProjectRelatedWebsite>.Default.Equals(relatedWebsite, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RelatedWebsite);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasRelatedWebsite);
				relatedWebsite = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RelatedWebsite);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasRelatedWebsite);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ResourcesModProjectItemViewModel?> OpenDependencyCommand => openDependencyCommand ?? (openDependencyCommand = new RelayCommand<ResourcesModProjectItemViewModel>(OpenDependency));

	public event Action<ResourcesModProjectItemViewModel>? ProjectChanged;

	internal ResourcesProjectDetailsViewModel(ResourcesOnlineProjectPageOptions options, IResourceCatalogService? resourceCatalogService, IUiDispatcher uiDispatcher, ILogger? logger)
	{
		this.options = options;
		this.resourceCatalogService = resourceCatalogService;
		thumbnailService = resourceCatalogService as IResourceThumbnailService;
		this.uiDispatcher = uiDispatcher;
		this.logger = logger;
	}

	public void SelectRoot(ResourcesModProjectItemViewModel project)
	{
		backStack.Clear();
		SelectCore(project);
	}

	[RelayCommand]
	public void OpenDependency(ResourcesModProjectItemViewModel? project)
	{
		if (project != null)
		{
			if (CurrentProject != null)
			{
				backStack.Push(CurrentProject);
			}
			SelectCore(project);
			logger?.LogDebug("Resource dependency project selected. Kind={Kind} Source={Source} ProjectId={ProjectId}", options.Kind, project.Project.Source, project.Project.ProjectId);
		}
	}

	public bool TryGoBack(out ResourcesModProjectItemViewModel? project)
	{
		if (!backStack.TryPop(out project) || project == null)
		{
			return false;
		}
		SelectCore(project);
		return true;
	}

	public void Reset()
	{
		CancelDependenciesLoad();
		CancelRelatedWebsiteLoad();
		backStack.Clear();
		CurrentProject = null;
		RelatedWebsite = null;
		RequiredDependencies.Clear();
		IsLoadingDependencies = false;
		NotifyStateChanged();
	}

	public void Dispose()
	{
		CancelDependenciesLoad();
		CancelRelatedWebsiteLoad();
	}

	private void SelectCore(ResourcesModProjectItemViewModel project)
	{
		CancelRelatedWebsiteLoad();
		RelatedWebsite = null;
		CurrentProject = project;
		RequiredDependencies.Clear();
		NotifyStateChanged();
		ProjectChanged?.Invoke(project);
		LoadDependenciesAsync(project);
		LoadRelatedWebsiteAsync(project);
	}

	private async Task LoadRelatedWebsiteAsync(ResourcesModProjectItemViewModel project)
	{
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		CancellationTokenSource? cancellationTokenSource2 = Interlocked.Exchange(ref relatedWebsiteCancellation, cancellationTokenSource);
		cancellationTokenSource2?.Cancel();
		cancellationTokenSource2?.Dispose();
		CancellationToken cancellationToken = cancellationTokenSource.Token;
		bool flag = resourceCatalogService == null;
		if (!flag)
		{
			ResourceProjectKind kind = project.Project.Kind;
			bool flag2 = (uint)(kind - 1) <= 2u;
			flag = !flag2;
		}
		if (flag)
		{
			return;
		}
		try
		{
			ResourceProjectRelatedWebsite website = await resourceCatalogService.GetRelatedWebsiteAsync(new ResourceProjectReference(project.Project.Kind, project.Project.Source, project.Project.ProjectId), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			cancellationToken.ThrowIfCancellationRequested();
			uiDispatcher.Invoke(delegate
			{
				if (!cancellationToken.IsCancellationRequested && CurrentProject == project)
				{
					RelatedWebsite = website;
				}
			});
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			uiDispatcher.Invoke(delegate
			{
				if (CurrentProject == project)
				{
					RelatedWebsite = null;
				}
			});
			logger?.LogWarning(exception, "Failed to load resource project related website. Kind={Kind} Source={Source} ProjectId={ProjectId}", project.Project.Kind, project.Project.Source, project.Project.ProjectId);
		}
	}

	private async Task LoadDependenciesAsync(ResourcesModProjectItemViewModel project)
	{
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		CancellationTokenSource? cancellationTokenSource2 = Interlocked.Exchange(ref dependenciesCancellation, cancellationTokenSource);
		cancellationTokenSource2?.Cancel();
		cancellationTokenSource2?.Dispose();
		CancellationToken cancellationToken = cancellationTokenSource.Token;
		bool flag = resourceCatalogService == null || project.Project.Kind != ResourceProjectKind.Mod;
		if (!flag)
		{
			ResourceProjectSource source = project.Project.Source;
			bool flag2 = (uint)source <= 1u;
			flag = !flag2;
		}
		if (flag)
		{
			IsLoadingDependencies = false;
			return;
		}
		IsLoadingDependencies = true;
		try
		{
			ResourceProjectDependenciesResult resourceProjectDependenciesResult = await resourceCatalogService.GetProjectDependenciesAsync(new ResourceProjectDependenciesRequest
			{
				Kind = project.Project.Kind,
				Source = project.Project.Source,
				ProjectId = project.Project.ProjectId,
				Slug = project.Project.Slug
			}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			cancellationToken.ThrowIfCancellationRequested();
			List<ResourcesModProjectItemViewModel> items = resourceProjectDependenciesResult.RequiredProjects.Select((ResourceProject dependency) => new ResourcesModProjectItemViewModel(dependency, null, options.FallbackIconKey, options.TypeOptions)).ToList();
			if (thumbnailService != null)
			{
				foreach (ResourcesModProjectItemViewModel item in items)
				{
					try
					{
						item.SetManagedIconSource(thumbnailService.TryGetCachedThumbnailSource(item.Project));
					}
					catch (Exception exception)
					{
						item.SetManagedIconSource(null);
						logger?.LogWarning(exception, "Failed to resolve cached dependency thumbnail. Source={Source} ProjectId={ProjectId}", item.Project.Source, item.Project.ProjectId);
					}
				}
			}
			uiDispatcher.Invoke(delegate
			{
				if (!cancellationToken.IsCancellationRequested && CurrentProject == project)
				{
					RequiredDependencies.Clear();
					foreach (ResourcesModProjectItemViewModel item2 in items)
					{
						RequiredDependencies.Add(item2);
					}
					IsLoadingDependencies = false;
					NotifyStateChanged();
				}
			});
			if (thumbnailService != null)
			{
				await RefreshDependencyThumbnailsAsync(project, items, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception exception2)
		{
			if (!cancellationToken.IsCancellationRequested)
			{
				uiDispatcher.Invoke(delegate
				{
					RequiredDependencies.Clear();
					IsLoadingDependencies = false;
					NotifyStateChanged();
				});
				logger?.LogError(exception2, "Failed to load resource project dependencies. Kind={Kind} Source={Source} ProjectId={ProjectId}", options.Kind, project.Project.Source, project.Project.ProjectId);
			}
		}
	}

	private async Task RefreshDependencyThumbnailsAsync(ResourcesModProjectItemViewModel parent, IReadOnlyList<ResourcesModProjectItemViewModel> items, CancellationToken cancellationToken)
	{
		await Task.WhenAll(items.Where((ResourcesModProjectItemViewModel item) => !string.IsNullOrWhiteSpace(item.Project.IconUrl)).Select((Func<ResourcesModProjectItemViewModel, Task>)async delegate(ResourcesModProjectItemViewModel item)
		{
			string source = await thumbnailService.GetOrCreateThumbnailSourceAsync(item.Project, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!string.IsNullOrWhiteSpace(source) && !cancellationToken.IsCancellationRequested)
			{
				uiDispatcher.Invoke(delegate
				{
					if (!cancellationToken.IsCancellationRequested && CurrentProject == parent && RequiredDependencies.Contains(item))
					{
						item.SetManagedIconSource(source);
					}
				});
			}
		})).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void CancelDependenciesLoad()
	{
		CancellationTokenSource? cancellationTokenSource = Interlocked.Exchange(ref dependenciesCancellation, null);
		cancellationTokenSource?.Cancel();
		cancellationTokenSource?.Dispose();
	}

	private void CancelRelatedWebsiteLoad()
	{
		CancellationTokenSource? cancellationTokenSource = Interlocked.Exchange(ref relatedWebsiteCancellation, null);
		cancellationTokenSource?.Cancel();
		cancellationTokenSource?.Dispose();
	}

	private void NotifyStateChanged()
	{
		OnPropertyChanged("CanGoBackToDependencyParent");
		OnPropertyChanged("HasRequiredDependencies");
	}
}
