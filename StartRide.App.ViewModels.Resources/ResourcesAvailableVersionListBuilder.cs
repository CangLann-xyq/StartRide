using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using StartRide.App.Resources;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Resources;

internal sealed class ResourcesAvailableVersionListBuilder
{
	private sealed class AvailableVersionCompatibilityGroup
	{
		public string Title { get; }

		public List<ResourceProjectVersion> Versions { get; } = new List<ResourceProjectVersion>();

		public AvailableVersionCompatibilityGroup(string title)
		{
			Title = title;
		}
	}

	private readonly ResourcesOnlineProjectPageOptions options;

	public AvailableVersionListBuildResult Build(IReadOnlyList<ResourceProjectVersion> versions, string title, ResourcesModProjectItemViewModel? selectedProject, string fallbackIconKey, string? selectedVersionId, string? selectedLoaderId, string searchQuery)
	{
		List<object> items = new List<object>();
		List<ResourceProjectVersion> versions2 = versions.Where((ResourceProjectVersion version) => MatchesFilters(version, selectedVersionId, selectedLoaderId, searchQuery)).ToList();
		int visibleVersionCount = AddGroupedItems(items, versions2, title, selectedProject, fallbackIconKey, selectedVersionId, selectedLoaderId);
		return new AvailableVersionListBuildResult(items, visibleVersionCount);
	}

	public int Append(IList<object> items, IReadOnlyList<ResourceProjectVersion> versions, string title, ResourcesModProjectItemViewModel? selectedProject, string fallbackIconKey, string? selectedVersionId, string? selectedLoaderId, string searchQuery, int currentVisibleCount)
	{
		RemoveEmptyPlaceholderHeader(items, title, currentVisibleCount);
		int num = 0;
		foreach (ResourceProjectVersion item in versions.Where((ResourceProjectVersion version) => MatchesFilters(version, selectedVersionId, selectedLoaderId, searchQuery)))
		{
			foreach (string item2 in CreateFilteredCompatibilityGroupTitles(item, selectedVersionId, selectedLoaderId))
			{
				int index = FindGroupInsertIndex(items, item2);
				items.Insert(index, new ResourcesModVersionItemViewModel(item, selectedProject, fallbackIconKey));
				num++;
			}
		}
		return num;
	}

	public string GetLoaderTitle(string loaderId)
	{
		return loaderId switch
		{
			"fabric" => Strings.Download_FabricLoaderTitle, 
			"forge" => Strings.Download_ForgeLoaderTitle, 
			"neoforge" => Strings.Download_NeoForgeLoaderTitle, 
			"quilt" => Strings.Download_QuiltLoaderTitle, 
			_ => loaderId, 
		};
	}

	internal bool MatchesFilters(ResourceProjectVersion version, string? selectedVersionId, string? selectedLoaderId, string searchQuery)
	{
		if (MatchesSearch(version, searchQuery) && MatchesVersionFilter(version, selectedVersionId))
		{
			return MatchesLoaderFilter(version, selectedLoaderId);
		}
		return false;
	}

	private int AddGroupedItems(ICollection<object> items, IReadOnlyList<ResourceProjectVersion> versions, string title, ResourcesModProjectItemViewModel? selectedProject, string fallbackIconKey, string? selectedVersionId, string? selectedLoaderId)
	{
		List<AvailableVersionCompatibilityGroup> list = new List<AvailableVersionCompatibilityGroup>();
		Dictionary<string, AvailableVersionCompatibilityGroup> dictionary = new Dictionary<string, AvailableVersionCompatibilityGroup>(StringComparer.OrdinalIgnoreCase);
		foreach (ResourceProjectVersion version in versions)
		{
			foreach (string item in CreateFilteredCompatibilityGroupTitles(version, selectedVersionId, selectedLoaderId))
			{
				if (!dictionary.TryGetValue(item, out var value))
				{
					value = new AvailableVersionCompatibilityGroup(item);
					dictionary.Add(item, value);
					list.Add(value);
				}
				value.Versions.Add(version);
			}
		}
		if (list.Count == 0)
		{
			items.Add(new ResourcesModVersionListHeaderItem(title));
			return 0;
		}
		int num = 0;
		foreach (AvailableVersionCompatibilityGroup item2 in list)
		{
			items.Add(new ResourcesModVersionListHeaderItem(item2.Title));
			foreach (ResourceProjectVersion version2 in item2.Versions)
			{
				items.Add(new ResourcesModVersionItemViewModel(version2, selectedProject, fallbackIconKey));
				num++;
			}
		}
		return num;
	}

	private IEnumerable<string> CreateFilteredCompatibilityGroupTitles(ResourceProjectVersion version, string? selectedVersionId, string? selectedLoaderId)
	{
		IReadOnlyList<string> readOnlyList = NormalizeGameVersionCompatibilityValues(version.GameVersions);
		IReadOnlyList<string> readOnlyList3;
		if (!options.ShowsLoaderFilters)
		{
			IReadOnlyList<string> readOnlyList2 = new global::_003C_003Ez__ReadOnlySingleElementList<string>(string.Empty);
			readOnlyList3 = readOnlyList2;
		}
		else
		{
			readOnlyList3 = ResolveCompatibilityLoaders(version);
		}
		IReadOnlyList<string> loaders = readOnlyList3;
		if (!string.IsNullOrWhiteSpace(selectedVersionId) && !string.Equals(selectedVersionId, "all", StringComparison.OrdinalIgnoreCase))
		{
			readOnlyList = readOnlyList.Where((string a) => string.Equals(a, selectedVersionId, StringComparison.OrdinalIgnoreCase)).ToList();
		}
		if (options.ShowsLoaderFilters && !string.IsNullOrWhiteSpace(selectedLoaderId) && !string.Equals(selectedLoaderId, "all", StringComparison.OrdinalIgnoreCase))
		{
			loaders = loaders.Where((string loader) => string.Equals(loader, selectedLoaderId, StringComparison.OrdinalIgnoreCase)).ToList();
		}
		foreach (string gameVersion in readOnlyList)
		{
			foreach (string item in loaders)
			{
				yield return string.IsNullOrWhiteSpace(item) ? gameVersion : (gameVersion + "-" + item);
			}
		}
	}

	private bool MatchesSearch(ResourceProjectVersion version, string searchQuery)
	{
		string text = searchQuery.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return true;
		}
		if (!ContainsSearchText(version.Name, text) && !ContainsSearchText(version.VersionNumber, text) && !ContainsSearchText(version.FileName, text))
		{
			return ContainsSearchText(version.VersionType, text);
		}
		return true;
	}

	private static bool MatchesVersionFilter(ResourceProjectVersion version, string? selectedVersion)
	{
		if (string.IsNullOrWhiteSpace(selectedVersion) || string.Equals(selectedVersion, "all", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return NormalizeGameVersionCompatibilityValues(version.GameVersions).Any((string versionId) => string.Equals(versionId, selectedVersion, StringComparison.OrdinalIgnoreCase));
	}

	private bool MatchesLoaderFilter(ResourceProjectVersion version, string? selectedLoader)
	{
		if (string.IsNullOrWhiteSpace(selectedLoader) || string.Equals(selectedLoader, "all", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (!options.ShowsLoaderFilters)
		{
			return true;
		}
		return ResolveCompatibilityLoaders(version).Any((string loader) => string.Equals(loader, selectedLoader, StringComparison.OrdinalIgnoreCase));
	}

	private static bool ContainsSearchText(string value, string query)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Contains(query, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static void RemoveEmptyPlaceholderHeader(IList<object> items, string title, int currentVisibleCount)
	{
		if (currentVisibleCount == 0 && items.Count == 1 && items[0] is ResourcesModVersionListHeaderItem resourcesModVersionListHeaderItem && string.Equals(resourcesModVersionListHeaderItem.Title, title, StringComparison.OrdinalIgnoreCase))
		{
			items.Clear();
		}
	}

	private static int FindGroupInsertIndex(IList<object> items, string title)
	{
		for (int i = 0; i < items.Count; i++)
		{
			if (items[i] is ResourcesModVersionListHeaderItem resourcesModVersionListHeaderItem && string.Equals(resourcesModVersionListHeaderItem.Title, title, StringComparison.OrdinalIgnoreCase))
			{
				int j;
				for (j = i + 1; j < items.Count && !(items[j] is ResourcesModVersionListHeaderItem); j++)
				{
				}
				return j;
			}
		}
		items.Add(new ResourcesModVersionListHeaderItem(title));
		return items.Count;
	}

	internal static IReadOnlyList<string> NormalizeGameVersionCompatibilityValues(IReadOnlyList<string> values)
	{
		List<string> list = (from value in values
			select value.Trim() into value
			where !string.IsNullOrWhiteSpace(value)
			select value).Where(IsMinecraftVersionLike).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
		if (list.Count != 0)
		{
			return list;
		}
		int num = 1;
		List<string> list2 = new List<string>(num);
		CollectionsMarshal.SetCount(list2, num);
		CollectionsMarshal.AsSpan(list2)[0] = Strings.Resources_ModVersionsUnknown;
		return list2;
	}

	internal static IReadOnlyList<string> ResolveCompatibilityLoaders(ResourceProjectVersion version)
	{
		IReadOnlyList<string> readOnlyList = NormalizeLoaderCompatibilityValues(version.Loaders);
		if (readOnlyList.Count > 0)
		{
			return readOnlyList;
		}
		readOnlyList = NormalizeLoaderCompatibilityValues(version.GameVersions);
		if (readOnlyList.Count > 0)
		{
			return readOnlyList;
		}
		readOnlyList = InferLoadersFromVersionText(version);
		if (readOnlyList.Count != 0)
		{
			return readOnlyList;
		}
		return new global::_003C_003Ez__ReadOnlySingleElementList<string>(Strings.Resources_ModLoadersUnknown);
	}

	private static IReadOnlyList<string> NormalizeLoaderCompatibilityValues(IReadOnlyList<string> values)
	{
		return (from value in values.Select(TryNormalizeLoaderId)
			where !string.IsNullOrWhiteSpace(value)
			select value).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
	}

	private static IReadOnlyList<string> InferLoadersFromVersionText(ResourceProjectVersion version)
	{
		string text = string.Join(' ', version.FileName, version.Name, version.VersionNumber);
		List<string> list = new List<string>();
		AddLoaderIfFound(text, "neoforge", list);
		AddLoaderIfFound(text, "fabric", list);
		AddLoaderIfFound(text, "forge", list);
		AddLoaderIfFound(text, "quilt", list);
		return list;
	}

	private static void AddLoaderIfFound(string text, string loader, ICollection<string> loaders)
	{
		if (ContainsLoaderToken(text, loader) && !loaders.Contains<string>(loader, StringComparer.OrdinalIgnoreCase))
		{
			loaders.Add(loader);
		}
	}

	private static bool ContainsLoaderToken(string text, string loader)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		for (int num = text.IndexOf(loader, StringComparison.OrdinalIgnoreCase); num >= 0; num = text.IndexOf(loader, num + loader.Length, StringComparison.OrdinalIgnoreCase))
		{
			char c = ((num != 0) ? text[num - 1] : '\0');
			int num2 = num + loader.Length;
			char c2 = ((num2 < text.Length) ? text[num2] : '\0');
			if (!char.IsLetterOrDigit(c) && !char.IsLetterOrDigit(c2))
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsMinecraftVersionLike(string value)
	{
		string text = value.Trim();
		if (text.Length == 0 || !char.IsDigit(text[0]))
		{
			return false;
		}
		return text.All(delegate(char character)
		{
			bool flag = char.IsLetterOrDigit(character);
			if (!flag)
			{
				bool flag2 = ((character == '-' || character == '.' || character == '_') ? true : false);
				flag = flag2;
			}
			return flag;
		});
	}

	private static string? TryNormalizeLoaderId(string value)
	{
		return value.Trim().ToLowerInvariant() switch
		{
			"fabric" => "fabric", 
			"forge" => "forge", 
			"neoforge" => "neoforge", 
			"quilt" => "quilt", 
			_ => null, 
		};
	}

	internal static bool IsUnknownInstanceVersionTarget(ResourcesModInstallTargetItemViewModel? target)
	{
		bool? flag = target?.IsLocalDownload;
		if (flag.HasValue && flag != true && !target.IsNewInstanceInstall && !target.IsServerInstall)
		{
			return string.IsNullOrWhiteSpace(target.Instance?.MinecraftVersion);
		}
		return false;
	}

	private static string GetLoaderId(LoaderKind loader)
	{
		return loader switch
		{
			LoaderKind.Fabric => "fabric", 
			LoaderKind.Forge => "forge", 
			LoaderKind.NeoForge => "neoforge", 
			LoaderKind.Quilt => "quilt", 
			_ => "vanilla", 
		};
	}

	public ResourcesAvailableVersionListBuilder(ResourcesOnlineProjectPageOptions options)
	{
		this.options = options;
	}

	public ResourcesFilterOptionItem CreateAllVersionFilterOption()
	{
		return new ResourcesFilterOptionItem
		{
			Id = "all",
			Title = options.AllVersionsText
		};
	}

	public List<ResourcesFilterOptionItem> CreateDefaultLoaderFilterOptions()
	{
		int num = 1;
		List<ResourcesFilterOptionItem> list = new List<ResourcesFilterOptionItem>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = new ResourcesFilterOptionItem
		{
			Id = "all",
			Title = options.AllLoadersText
		};
		return list;
	}

	public IReadOnlyList<ResourcesFilterOptionItem> CreateVersionFilterOptions(IReadOnlyList<ResourceProjectVersion> versions)
	{
		return (from version in versions.SelectMany((ResourceProjectVersion version) => NormalizeGameVersionCompatibilityValues(version.GameVersions)).Distinct<string>(StringComparer.OrdinalIgnoreCase)
			select new ResourcesFilterOptionItem
			{
				Id = version,
				Title = version
			}).ToList();
	}

	public IReadOnlyList<ResourcesFilterOptionItem> CreateLoaderFilterOptions(IReadOnlyList<ResourceProjectVersion> versions)
	{
		List<ResourcesFilterOptionItem> list = CreateDefaultLoaderFilterOptions();
		if (!options.ShowsLoaderFilters)
		{
			return list;
		}
		foreach (string item in (from loader in versions.SelectMany(ResolveCompatibilityLoaders).Distinct<string>(StringComparer.OrdinalIgnoreCase)
			where !string.Equals(loader, "all", StringComparison.OrdinalIgnoreCase)
			select loader).ToList())
		{
			list.Add(new ResourcesFilterOptionItem
			{
				Id = item,
				Title = GetLoaderTitle(item)
			});
		}
		return list.DistinctBy<ResourcesFilterOptionItem, string>((ResourcesFilterOptionItem option) => option.Id, StringComparer.OrdinalIgnoreCase).ToList();
	}

	public string ResolveDefaultVersionFilterId(ResourcesModInstallTargetItemViewModel? target)
	{
		if (target == null || target.IsLocalDownload || IsUnknownInstanceVersionTarget(target))
		{
			return "all";
		}
		string text = target.Instance?.MinecraftVersion?.Trim();
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return "all";
	}

	public string ResolveDefaultLoaderFilterId(ResourcesModInstallTargetItemViewModel? target)
	{
		if (target == null || target.IsLocalDownload || IsUnknownInstanceVersionTarget(target) || target.Instance == null)
		{
			return "all";
		}
		return target.Instance.Loader switch
		{
			LoaderKind.Fabric => "fabric", 
			LoaderKind.Forge => "forge", 
			LoaderKind.NeoForge => "neoforge", 
			LoaderKind.Quilt => "quilt", 
			_ => "all", 
		};
	}

	public string FormatTitle(ResourcesModInstallTargetItemViewModel? target)
	{
		if (target == null || target.IsLocalDownload || IsUnknownInstanceVersionTarget(target))
		{
			return options.VersionsAllTitleText;
		}
		GameInstance instance = target.Instance;
		if (instance == null)
		{
			return options.VersionsAllTitleText;
		}
		string text = instance.MinecraftVersion?.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return options.VersionsAllTitleText;
		}
		if (!options.ShowsLoaderFilters)
		{
			return text;
		}
		return text + "-" + GetLoaderId(instance.Loader);
	}
}
