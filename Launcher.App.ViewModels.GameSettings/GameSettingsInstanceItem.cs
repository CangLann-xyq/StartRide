using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.App.Utilities;
using Launcher.App.ViewModels.Shared;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.GameSettings;

public sealed class GameSettingsInstanceItem : ObservableObject
{
	private InstanceCatalogEntrySnapshot catalogSnapshot;

	[ObservableProperty]
	private bool isSelected;

	public GameInstance Instance { get; private set; }

	public string VersionType { get; private set; }

	public string Name => GameInstanceDisplayFormatter.GetName(Instance);

	public string MinecraftVersion => GameInstanceDisplayFormatter.GetMinecraftVersion(Instance);

	public string VersionName => GameInstanceDisplayFormatter.GetVersionName(Instance);

	public LoaderKind Loader => Instance.Loader;

	public bool HasModLoader => Loader != LoaderKind.Vanilla;

	public bool IsRelease => VersionType.Equals("release", StringComparison.OrdinalIgnoreCase);

	public bool IsSnapshot => VersionType.Equals("snapshot", StringComparison.OrdinalIgnoreCase);

	public bool IsAprilFools
	{
		get
		{
			if (!MinecraftAprilFoolsVersionClassifier.IsAprilFoolsVersion(MinecraftVersion))
			{
				return MinecraftAprilFoolsVersionClassifier.IsAprilFoolsVersion(VersionName);
			}
			return true;
		}
	}

	public bool IsBeta => VersionType.Equals("old_beta", StringComparison.OrdinalIgnoreCase);

	public bool IsAlpha => VersionType.Equals("old_alpha", StringComparison.OrdinalIgnoreCase);

	public string TypeLabel => MinecraftVersionTypeDisplayProvider.GetLabel(VersionType);

	public string LoaderLabel => GameInstanceDisplayFormatter.GetLoaderLabel(Loader);

	public string LoaderVersionDisplay => LoaderVersionDisplayFormatter.Format(Loader, Instance.LoaderVersion);

	public string Subtitle => GameInstanceDisplayFormatter.GetSubtitle(Instance);

	public string UpdatedDateText => Instance.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd");

	public string IconSource => MinecraftVersionIconResolver.Resolve(Instance, VersionType, MinecraftVersion);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsSelected
	{
		get
		{
			return isSelected;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isSelected, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsSelected);
				isSelected = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsSelected);
			}
		}
	}

	public GameSettingsInstanceItem(GameInstance instance, string versionType)
	{
		Instance = instance;
		VersionType = NormalizeVersionType(versionType);
		catalogSnapshot = InstanceCatalogEntrySnapshot.Create(instance);
	}

	public bool Update(GameInstance instance, string versionType)
	{
		string text = NormalizeVersionType(versionType);
		InstanceCatalogEntrySnapshot instanceCatalogEntrySnapshot = InstanceCatalogEntrySnapshot.Create(instance);
		bool num = catalogSnapshot != instanceCatalogEntrySnapshot || !string.Equals(VersionType, text, StringComparison.OrdinalIgnoreCase);
		Instance = instance;
		VersionType = text;
		catalogSnapshot = instanceCatalogEntrySnapshot;
		if (!num)
		{
			return false;
		}
		NotifyDisplayPropertiesChanged();
		return true;
	}

	public bool MatchesSearch(string query)
	{
		if (!Contains(Name, query) && !Contains(MinecraftVersion, query) && !Contains(VersionName, query) && !Contains(LoaderLabel, query) && !Contains(Instance.LoaderVersion ?? string.Empty, query))
		{
			return Contains(TypeLabel, query);
		}
		return true;
	}

	public static string NormalizeVersionType(string? type)
	{
		return MinecraftVersionIconResolver.NormalizeVersionType(type);
	}

	private static bool Contains(string value, string query)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Contains(query, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private void NotifyDisplayPropertiesChanged()
	{
		OnPropertyChanged("Name");
		OnPropertyChanged("MinecraftVersion");
		OnPropertyChanged("VersionName");
		OnPropertyChanged("Loader");
		OnPropertyChanged("HasModLoader");
		OnPropertyChanged("IsRelease");
		OnPropertyChanged("IsSnapshot");
		OnPropertyChanged("IsAprilFools");
		OnPropertyChanged("IsBeta");
		OnPropertyChanged("IsAlpha");
		OnPropertyChanged("TypeLabel");
		OnPropertyChanged("LoaderLabel");
		OnPropertyChanged("LoaderVersionDisplay");
		OnPropertyChanged("Subtitle");
		OnPropertyChanged("UpdatedDateText");
		OnPropertyChanged("IconSource");
	}
}
