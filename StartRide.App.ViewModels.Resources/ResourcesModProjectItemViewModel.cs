using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Resources;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Resources;

public sealed class ResourcesModProjectItemViewModel : ObservableObject
{
	private static readonly string[] LoaderDisplayOrder = new string[4] { "fabric", "forge", "neoforge", "quilt" };

	private readonly IReadOnlyList<string>? minecraftReleaseVersionOrder;

	private readonly string fallbackIconKey;

	[ObservableProperty]
	[NotifyPropertyChangedFor("IconKey")]
	private string? iconSource;

	public ResourceProject Project { get; }

	public string Title => Project.Title;

	public string Description => Project.Description;

	public IReadOnlyList<string> TitleTags { get; }

	public bool HasTitleTags => TitleTags.Count > 0;

	public string TitleTagsText => string.Join(", ", TitleTags);

	public string Subtitle
	{
		get
		{
			if (Project.Kind != ResourceProjectKind.Mod)
			{
				return string.Join("  ", SupportedMinecraftVersionsText, SourceText);
			}
			return string.Join("  ", SupportedMinecraftVersionsText, SupportedLoadersText, SourceText);
		}
	}

	public string TrailingText => string.Format(Strings.Resources_ModDownloadsFormat, DownloadsText);

	public string SupportedMinecraftVersionsText => ResourceMinecraftVersionSupportFormatter.Format(Project.SupportedMinecraftVersions, minecraftReleaseVersionOrder);

	public string SupportedLoadersText => FormatLoaders(Project.SupportedLoaders);

	public string SourceText => Project.Source switch
	{
		ResourceProjectSource.Modrinth => Strings.Resources_ModSourceModrinth, 
		ResourceProjectSource.CurseForge => Strings.Resources_ModSourceCurseForge, 
		_ => string.Empty, 
	};

	public string DownloadsText => FormatDownloads(Project.Downloads);

	public bool ShowsLoaders
	{
		get
		{
			ResourceProjectKind kind = Project.Kind;
			if (kind == ResourceProjectKind.Mod || kind == ResourceProjectKind.Modpack)
			{
				return true;
			}
			return false;
		}
	}

	public string IconKey
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(IconSource))
			{
				return string.Empty;
			}
			return fallbackIconKey;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string? IconSource
	{
		get
		{
			return iconSource;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(iconSource, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconSource);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IconKey);
				iconSource = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconSource);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconKey);
			}
		}
	}

	public ResourcesModProjectItemViewModel(ResourceProject project, IReadOnlyList<string>? minecraftReleaseVersionOrder = null, string fallbackIconKey = "instance_setting_page/mod", IReadOnlyList<ResourcesOnlineProjectTypeOption>? typeOptions = null)
	{
		Project = project;
		this.minecraftReleaseVersionOrder = minecraftReleaseVersionOrder;
		this.fallbackIconKey = fallbackIconKey;
		iconSource = project.IconUrl;
		TitleTags = CreateTitleTags(project.Categories, typeOptions);
	}

	internal void SetManagedIconSource(string? source)
	{
		IconSource = source;
	}

	private static string FormatLoaders(IReadOnlyList<string> loaders)
	{
		List<string> list = (from loader in loaders
			where !string.IsNullOrWhiteSpace(loader)
			select loader.Trim().ToLowerInvariant()).Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy(delegate(string loader)
		{
			int num = Array.IndexOf(LoaderDisplayOrder, loader);
			return (num >= 0) ? num : int.MaxValue;
		}).ThenBy<string, string>((string loader) => loader, StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (list.Count != 0)
		{
			return string.Join("/", list);
		}
		return Strings.Resources_ModLoadersUnknown;
	}

	private static string FormatDownloads(long downloads)
	{
		if (downloads >= 100000000)
		{
			return string.Format(Strings.Resources_ModDownloadsHundredMillionFormat, (double)downloads / 100000000.0);
		}
		if (downloads >= 10000)
		{
			return string.Format(Strings.Resources_ModDownloadsTenThousandFormat, (double)downloads / 10000.0);
		}
		return downloads.ToString("N0");
	}

	private static IReadOnlyList<string> CreateTitleTags(IReadOnlyList<ResourceProjectCategory> categories, IReadOnlyList<ResourcesOnlineProjectTypeOption>? typeOptions)
	{
		if (categories.Count == 0 || typeOptions == null || typeOptions.Count == 0)
		{
			return Array.Empty<string>();
		}
		Dictionary<ResourceProjectCategory, string> titlesByCategory = (from option in typeOptions
			group option by option.Category).ToDictionary((IGrouping<ResourceProjectCategory, ResourcesOnlineProjectTypeOption> group) => group.Key, (IGrouping<ResourceProjectCategory, ResourcesOnlineProjectTypeOption> group) => group.First().Title);
		return (from category in categories.Distinct().Where(titlesByCategory.ContainsKey)
			select titlesByCategory[category] into title
			where !string.IsNullOrWhiteSpace(title)
			select title).ToList();
	}
}
