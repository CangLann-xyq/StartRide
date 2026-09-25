using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Resources;
using StartRide.App.ViewModels.Resources;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class ModManagementModItemViewModel : ObservableObject
{
	[ObservableProperty]
	private string title = string.Empty;

	[ObservableProperty]
	private string fileName = string.Empty;

	[ObservableProperty]
	private string fullPath = string.Empty;

	[ObservableProperty]
	private string? iconSource;

	[ObservableProperty]
	private string? loader;

	[ObservableProperty]
	private string? modId;

	[ObservableProperty]
	private string? version;

	[ObservableProperty]
	private bool isEnabled;

	[ObservableProperty]
	private bool isSelected;

	[ObservableProperty]
	[NotifyPropertyChangedFor("HasTitleTags")]
	private IReadOnlyList<string> titleTags = Array.Empty<string>();

	[ObservableProperty]
	[NotifyPropertyChangedFor("HasProjectDetails")]
	private ResourceProjectReference? projectReference;

	public string Subtitle => FileName;

	public string TrailingText
	{
		get
		{
			if (!IsEnabled)
			{
				return Strings.GameSettings_ModManagementDisabledState;
			}
			return Strings.GameSettings_ModManagementEnabledState;
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
			return "instance_setting_page/mod";
		}
	}

	public bool HasTitleTags => TitleTags.Count > 0;

	public bool HasProjectDetails => (object)ProjectReference != null;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string Title
	{
		get
		{
			return title;
		}
		[MemberNotNull("title")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(title, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Title);
				title = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Title);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string FileName
	{
		get
		{
			return fileName;
		}
		[MemberNotNull("fileName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(fileName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FileName);
				fileName = value;
				OnFileNameChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FileName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string FullPath
	{
		get
		{
			return fullPath;
		}
		[MemberNotNull("fullPath")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(fullPath, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FullPath);
				fullPath = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FullPath);
			}
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
				iconSource = value;
				OnIconSourceChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IconSource);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string? Loader
	{
		get
		{
			return loader;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(loader, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Loader);
				loader = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Loader);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string? ModId
	{
		get
		{
			return modId;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(modId, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ModId);
				modId = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ModId);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string? Version
	{
		get
		{
			return version;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(version, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Version);
				version = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Version);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsEnabled
	{
		get
		{
			return isEnabled;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(isEnabled, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsEnabled);
				isEnabled = value;
				OnIsEnabledChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsEnabled);
			}
		}
	}

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

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IReadOnlyList<string> TitleTags
	{
		get
		{
			return titleTags;
		}
		[MemberNotNull("titleTags")]
		set
		{
			if (!EqualityComparer<IReadOnlyList<string>>.Default.Equals(titleTags, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TitleTags);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasTitleTags);
				titleTags = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TitleTags);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasTitleTags);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ResourceProjectReference? ProjectReference
	{
		get
		{
			return projectReference;
		}
		set
		{
			if (!EqualityComparer<ResourceProjectReference>.Default.Equals(projectReference, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ProjectReference);
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasProjectDetails);
				projectReference = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ProjectReference);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasProjectDetails);
			}
		}
	}

	public ModManagementModItemViewModel(LocalMod mod)
	{
		SyncFrom(mod);
	}

	public void SyncFrom(LocalMod mod)
	{
		Title = (string.IsNullOrWhiteSpace(mod.Name) ? GetDisplayFileNameWithoutModExtensions(mod.FileName) : mod.Name);
		Loader = NormalizeSubtitlePart(mod.Loader);
		ModId = NormalizeSubtitlePart(mod.ModId);
		Version = NormalizeSubtitlePart(mod.Version);
		FileName = mod.FileName;
		FullPath = mod.FullPath;
		IconSource = mod.IconSource;
		TitleTags = ResourceProjectCategoryTitleFormatter.Format(ResourceProjectKind.Mod, mod.Categories);
		ProjectReference = mod.ProjectReference;
		IsEnabled = mod.IsEnabled;
	}

	private static string? NormalizeSubtitlePart(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Trim();
		}
		return null;
	}

	private static string GetDisplayFileNameWithoutModExtensions(string fileName)
	{
		if (fileName.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
		{
			string text = fileName;
			int length = ".jar.disabled".Length;
			return text.Substring(0, text.Length - length);
		}
		if (fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
		{
			string text = fileName;
			int length = ".jar".Length;
			return text.Substring(0, text.Length - length);
		}
		return Path.GetFileNameWithoutExtension(fileName);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnFileNameChanged(string value)
	{
		OnPropertyChanged("Subtitle");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIconSourceChanged(string? value)
	{
		OnPropertyChanged("IconKey");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIsEnabledChanged(bool value)
	{
		OnPropertyChanged("TrailingText");
	}
}
