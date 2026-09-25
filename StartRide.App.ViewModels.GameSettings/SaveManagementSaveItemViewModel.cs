using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.GameSettings;

public sealed class SaveManagementSaveItemViewModel : ObservableObject
{
	[ObservableProperty]
	private string title = string.Empty;

	[ObservableProperty]
	private string? subtitle;

	[ObservableProperty]
	private string fullPath = string.Empty;

	[ObservableProperty]
	private string? iconSource;

	[ObservableProperty]
	private DateTimeOffset createdAt;

	[ObservableProperty]
	private bool isSelected;

	public string TrailingText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

	public string IconKey
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(IconSource))
			{
				return string.Empty;
			}
			return "instance_setting_page/saves";
		}
	}

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
	public string? Subtitle
	{
		get
		{
			return subtitle;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(subtitle, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Subtitle);
				subtitle = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Subtitle);
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
	public DateTimeOffset CreatedAt
	{
		get
		{
			return createdAt;
		}
		set
		{
			if (!EqualityComparer<DateTimeOffset>.Default.Equals(createdAt, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CreatedAt);
				createdAt = value;
				OnCreatedAtChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CreatedAt);
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

	public SaveManagementSaveItemViewModel(LocalSave save)
	{
		SyncFrom(save);
	}

	public void SyncFrom(LocalSave save)
	{
		Title = (string.IsNullOrWhiteSpace(save.Name) ? save.DirectoryName : save.Name);
		Subtitle = (string.Equals(Title, save.DirectoryName, StringComparison.OrdinalIgnoreCase) ? null : save.DirectoryName);
		FullPath = save.FullPath;
		IconSource = save.IconSource;
		CreatedAt = save.CreatedAt;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnIconSourceChanged(string? value)
	{
		OnPropertyChanged("IconKey");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnCreatedAtChanged(DateTimeOffset value)
	{
		OnPropertyChanged("TrailingText");
	}
}
