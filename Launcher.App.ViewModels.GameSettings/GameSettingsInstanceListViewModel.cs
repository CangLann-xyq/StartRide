using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.App.Resources;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.GameSettings;

public sealed class GameSettingsInstanceListViewModel : ObservableObject
{
	public const string LocalImportCategoryId = "local_import";

	private readonly ILogger<GameSettingsInstanceListViewModel> logger;

	private bool hasLoadedInstances;

	private bool preserveFilteredSelection;

	private long appliedCatalogRevision = -1L;

	[ObservableProperty]
	private GameSettingsInstanceCategory? selectedCategory;

	[ObservableProperty]
	private GameSettingsInstanceItem? selectedInstance;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private string loadError = string.Empty;

	[ObservableProperty]
	private string emptyMessage = string.Empty;

	[ObservableProperty]
	private string searchQuery = string.Empty;

	[ObservableProperty]
	private int entranceAnimationToken;

	public ObservableCollection<GameSettingsInstanceCategory> Categories { get; } = new ObservableCollection<GameSettingsInstanceCategory>();

	public List<GameSettingsInstanceItem> AllInstances { get; } = new List<GameSettingsInstanceItem>();

	public ObservableCollection<GameSettingsInstanceItem> VisibleInstances { get; } = new ObservableCollection<GameSettingsInstanceItem>();

	public bool HasVisibleInstances => VisibleInstances.Count > 0;

	public bool HasLoadError => !string.IsNullOrWhiteSpace(LoadError);

	public bool HasEmptyMessage => !string.IsNullOrWhiteSpace(EmptyMessage);

	public long AppliedCatalogRevision => appliedCatalogRevision;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public GameSettingsInstanceCategory? SelectedCategory
	{
		get
		{
			return selectedCategory;
		}
		set
		{
			if (!EqualityComparer<GameSettingsInstanceCategory>.Default.Equals(selectedCategory, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedCategory);
				selectedCategory = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedCategory);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public GameSettingsInstanceItem? SelectedInstance
	{
		get
		{
			return selectedInstance;
		}
		set
		{
			if (!EqualityComparer<GameSettingsInstanceItem>.Default.Equals(selectedInstance, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedInstance);
				selectedInstance = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedInstance);
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
	public string LoadError
	{
		get
		{
			return loadError;
		}
		[MemberNotNull("loadError")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(loadError, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LoadError);
				loadError = value;
				OnLoadErrorChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LoadError);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string EmptyMessage
	{
		get
		{
			return emptyMessage;
		}
		[MemberNotNull("emptyMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(emptyMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EmptyMessage);
				emptyMessage = value;
				OnEmptyMessageChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EmptyMessage);
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
	public int EntranceAnimationToken
	{
		get
		{
			return entranceAnimationToken;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(entranceAnimationToken, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EntranceAnimationToken);
				entranceAnimationToken = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EntranceAnimationToken);
			}
		}
	}

	public event Action? LocalImportRequested;

	public GameSettingsInstanceListViewModel(ILogger<GameSettingsInstanceListViewModel>? logger = null)
	{
		this.logger = logger ?? NullLogger<GameSettingsInstanceListViewModel>.Instance;
		// StartRide：BeamNG 没有快照版/愚人节版/远古版这类"特殊版本"，
		// 版本分类只保留官方正式版（release），不再出现任何 Minecraft 专用版本类型。
		Categories.Add(new GameSettingsInstanceCategory("all", Strings.GameSettings_AllCategory, string.Empty, "general/general_all_application"));
		Categories.Add(new GameSettingsInstanceCategory("release", Strings.Download_ReleaseCategory, string.Empty, "instance_download_page/release"));
		Categories.Add(new GameSettingsInstanceCategory("local_import", Strings.Download_LocalImportCategory, string.Empty, "instance_download_page/localimport"));
		SelectCategory(Categories[0], refreshVisibleInstances: false);
	}

	public bool ApplyInstanceCatalog(IReadOnlyList<GameInstance> instances, long catalogRevision, bool playEntranceAnimation)
	{
		if (appliedCatalogRevision == catalogRevision)
		{
			return false;
		}
		bool num = hasLoadedInstances;
		string instanceId = SelectedInstance?.Instance.Id;
		IsLoading = false;
		LoadError = string.Empty;
		EmptyMessage = string.Empty;
		bool result = Reconcile(instances);
		RestoreSelection(instanceId);
		hasLoadedInstances = true;
		appliedCatalogRevision = catalogRevision;
		RefreshVisibleInstances();
		if (!num & playEntranceAnimation)
		{
			EntranceAnimationToken++;
		}
		logger.LogDebug("Game settings instance catalog applied. Count={InstanceCount} VisibleCount={VisibleCount} SelectedInstanceId={SelectedInstanceId} CatalogRevision={CatalogRevision}", AllInstances.Count, VisibleInstances.Count, SelectedInstance?.Instance.Id, catalogRevision);
		return result;
	}

	public void SetPreserveFilteredSelection(bool value)
	{
		preserveFilteredSelection = value;
		RefreshVisibleInstances();
	}

	public void SelectCategory(GameSettingsInstanceCategory category, bool refreshVisibleInstances = true)
	{
		if (string.Equals(category.Id, "local_import", StringComparison.Ordinal))
		{
			LocalImportRequested?.Invoke();
			return;
		}
		bool flag = SelectedCategory != category && !string.Equals(SelectedCategory?.Id, category.Id, StringComparison.OrdinalIgnoreCase);
		SelectedCategory = category;
		foreach (GameSettingsInstanceCategory category2 in Categories)
		{
			category2.IsSelected = category2 == category;
		}
		if (refreshVisibleInstances)
		{
			RefreshVisibleInstances();
		}
		if (hasLoadedInstances & flag)
		{
			EntranceAnimationToken++;
		}
	}

	public GameSettingsInstanceItem SelectInstance(GameSettingsInstanceItem instance)
	{
		SelectInstanceCore(instance);
		return instance;
	}

	public GameSettingsInstanceItem GetOrAdd(GameInstance instance)
	{
		GameSettingsInstanceItem gameSettingsInstanceItem = Find(instance.Id);
		if (gameSettingsInstanceItem != null)
		{
			return gameSettingsInstanceItem;
		}
		gameSettingsInstanceItem = CreateItem(instance);
		AllInstances.Add(gameSettingsInstanceItem);
		RefreshVisibleInstances();
		return gameSettingsInstanceItem;
	}

	public GameSettingsInstanceItem? Find(string? instanceId)
	{
		if (string.IsNullOrWhiteSpace(instanceId))
		{
			return null;
		}
		return AllInstances.FirstOrDefault((GameSettingsInstanceItem item) => string.Equals(item.Instance.Id, instanceId, StringComparison.OrdinalIgnoreCase));
	}

	public void AddOrUpdate(GameInstance instance)
	{
		if (!hasLoadedInstances)
		{
			return;
		}
		GameSettingsInstanceItem gameSettingsInstanceItem = Find(instance.Id);
		if (gameSettingsInstanceItem == null)
		{
			AllInstances.Add(CreateItem(instance));
		}
		else
		{
			bool num = SelectedInstance == gameSettingsInstanceItem;
			GameInstance instance2 = gameSettingsInstanceItem.Instance;
			gameSettingsInstanceItem.Update(instance, ResolveVersionType(instance));
			if (num && instance2 != gameSettingsInstanceItem.Instance)
			{
				SelectInstanceCore(gameSettingsInstanceItem, forceNotification: true);
			}
		}
		RefreshVisibleInstances();
	}

	public bool Remove(string instanceId)
	{
		if (AllInstances.RemoveAll((GameSettingsInstanceItem item) => string.Equals(item.Instance.Id, instanceId, StringComparison.OrdinalIgnoreCase)) <= 0)
		{
			return false;
		}
		if (string.Equals(SelectedInstance?.Instance.Id, instanceId, StringComparison.OrdinalIgnoreCase))
		{
			SelectInstanceCore(null);
		}
		RefreshVisibleInstances();
		return true;
	}

	private void RefreshVisibleInstances()
	{
		GameSettingsInstanceFilterResult gameSettingsInstanceFilterResult = GameSettingsInstanceFilter.Apply(AllInstances, SelectedCategory, SearchQuery, SelectedInstance, hasLoadedInstances, IsLoading, HasLoadError);
		EmptyMessage = gameSettingsInstanceFilterResult.EmptyMessage;
		if (gameSettingsInstanceFilterResult.ShouldClearSelectedInstance && (!preserveFilteredSelection || SelectedInstance == null || !ContainsSelectedInstance()))
		{
			SelectInstanceCore(null);
		}
		ApplyVisibleInstances(gameSettingsInstanceFilterResult.Instances);
	}

	private bool ContainsSelectedInstance()
	{
		if (SelectedInstance != null)
		{
			return AllInstances.Any((GameSettingsInstanceItem item) => item == SelectedInstance || (!string.IsNullOrWhiteSpace(item.Instance.Id) && string.Equals(item.Instance.Id, SelectedInstance.Instance.Id, StringComparison.OrdinalIgnoreCase)));
		}
		return false;
	}

	private bool Reconcile(IReadOnlyList<GameInstance> instances)
	{
		Dictionary<string, GameSettingsInstanceItem> dictionary = AllInstances.Where((GameSettingsInstanceItem item) => !string.IsNullOrWhiteSpace(item.Instance.Id)).GroupBy<GameSettingsInstanceItem, string>((GameSettingsInstanceItem item) => item.Instance.Id, StringComparer.OrdinalIgnoreCase).ToDictionary<IGrouping<string, GameSettingsInstanceItem>, string, GameSettingsInstanceItem>((IGrouping<string, GameSettingsInstanceItem> group) => group.Key, (IGrouping<string, GameSettingsInstanceItem> group) => group.First(), StringComparer.OrdinalIgnoreCase);
		List<GameSettingsInstanceItem> list = new List<GameSettingsInstanceItem>(instances.Count);
		bool flag = AllInstances.Count != instances.Count;
		foreach (GameInstance instance in instances)
		{
			if (!string.IsNullOrWhiteSpace(instance.Id) && dictionary.TryGetValue(instance.Id, out var value))
			{
				flag |= value.Update(instance, ResolveVersionType(instance));
				list.Add(value);
			}
			else
			{
				list.Add(CreateItem(instance));
				flag = true;
			}
		}
		if (!flag)
		{
			for (int num = 0; num < list.Count; num++)
			{
				if (AllInstances[num] != list[num])
				{
					flag = true;
					break;
				}
			}
		}
		AllInstances.Clear();
		AllInstances.AddRange(list);
		return flag;
	}

	private void RestoreSelection(string? instanceId)
	{
		SelectInstanceCore(string.IsNullOrWhiteSpace(instanceId) ? null : Find(instanceId));
	}

	private void SelectInstanceCore(GameSettingsInstanceItem? instance, bool forceNotification = false)
	{
		GameSettingsInstanceItem gameSettingsInstanceItem = SelectedInstance;
		SelectedInstance = instance;
		if (forceNotification && gameSettingsInstanceItem == instance)
		{
			OnPropertyChanged("SelectedInstance");
		}
		foreach (GameSettingsInstanceItem allInstance in AllInstances)
		{
			allInstance.IsSelected = allInstance == instance;
		}
	}

	private void ApplyVisibleInstances(IReadOnlyList<GameSettingsInstanceItem> instances)
	{
		bool flag = false;
		int index;
		for (index = VisibleInstances.Count - 1; index >= 0; index--)
		{
			if (!instances.Any((GameSettingsInstanceItem item) => item == VisibleInstances[index]))
			{
				VisibleInstances.RemoveAt(index);
				flag = true;
			}
		}
		for (int num = 0; num < instances.Count; num++)
		{
			GameSettingsInstanceItem gameSettingsInstanceItem = instances[num];
			if (num < VisibleInstances.Count && VisibleInstances[num] == gameSettingsInstanceItem)
			{
				continue;
			}
			int num2 = -1;
			for (int num3 = num + 1; num3 < VisibleInstances.Count; num3++)
			{
				if (VisibleInstances[num3] == gameSettingsInstanceItem)
				{
					num2 = num3;
					break;
				}
			}
			if (num2 >= 0)
			{
				VisibleInstances.Move(num2, num);
			}
			else
			{
				VisibleInstances.Insert(num, gameSettingsInstanceItem);
			}
			flag = true;
		}
		if (flag)
		{
			NotifyVisibleInstancesChanged();
		}
	}

	private void NotifyVisibleInstancesChanged()
	{
		OnPropertyChanged("VisibleInstances");
		OnPropertyChanged("HasVisibleInstances");
		OnPropertyChanged("HasEmptyMessage");
	}

	private GameSettingsInstanceItem CreateItem(GameInstance instance)
	{
		return new GameSettingsInstanceItem(instance, ResolveVersionType(instance));
	}

	private static string ResolveVersionType(GameInstance instance)
	{
		return instance.VersionType;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnLoadErrorChanged(string value)
	{
		OnPropertyChanged("HasLoadError");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnEmptyMessageChanged(string value)
	{
		OnPropertyChanged("HasEmptyMessage");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnSearchQueryChanged(string value)
	{
		RefreshVisibleInstances();
	}
}
