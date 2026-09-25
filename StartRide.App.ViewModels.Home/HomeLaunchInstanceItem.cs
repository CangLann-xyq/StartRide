using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Utilities;
using StartRide.App.ViewModels.GameSettings;
using StartRide.App.ViewModels.Shared;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Home;

public sealed class HomeLaunchInstanceItem : ObservableObject
{
	private InstanceCatalogEntrySnapshot catalogSnapshot;

	[ObservableProperty]
	private bool isSelected;

	public GameInstance Instance { get; private set; }

	public string VersionType { get; private set; }

	public string Name => GameInstanceDisplayFormatter.GetName(Instance);

	public string MinecraftVersion => GameInstanceDisplayFormatter.GetMinecraftVersion(Instance);

	public string VersionName => GameInstanceDisplayFormatter.GetVersionName(Instance);

	public string LoaderLabel => GameInstanceDisplayFormatter.GetLoaderLabel(Instance.Loader);

	public string LoaderVersionDisplay => LoaderVersionDisplayFormatter.Format(Instance.Loader, Instance.LoaderVersion);

	public string Subtitle => GameInstanceDisplayFormatter.GetSubtitle(Instance);

	public string IconSource => VersionIconResolver.Resolve(Instance, VersionType, MinecraftVersion);

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

	public HomeLaunchInstanceItem(GameInstance instance, string versionType = "")
	{
		Instance = instance;
		VersionType = VersionIconResolver.NormalizeVersionType(versionType);
		catalogSnapshot = InstanceCatalogEntrySnapshot.Create(instance);
	}

	public bool Update(GameInstance instance, string versionType)
	{
		string text = VersionIconResolver.NormalizeVersionType(versionType);
		InstanceCatalogEntrySnapshot instanceCatalogEntrySnapshot = InstanceCatalogEntrySnapshot.Create(instance);
		bool num = catalogSnapshot != instanceCatalogEntrySnapshot || !string.Equals(VersionType, text, StringComparison.OrdinalIgnoreCase);
		Instance = instance;
		VersionType = text;
		catalogSnapshot = instanceCatalogEntrySnapshot;
		if (!num)
		{
			return false;
		}
		OnPropertyChanged("Name");
		OnPropertyChanged("MinecraftVersion");
		OnPropertyChanged("VersionName");
		OnPropertyChanged("LoaderLabel");
		OnPropertyChanged("LoaderVersionDisplay");
		OnPropertyChanged("Subtitle");
		OnPropertyChanged("IconSource");
		return true;
	}
}
