using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;

namespace Launcher.App.ViewModels.Download;

public sealed class DownloadVersionCategory : ObservableObject
{
	[ObservableProperty]
	private bool isSelected;

	public string Id { get; }

	public string Title { get; }

	public string Icon { get; }

	public string? IconKey { get; }

	public string IconMode
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(IconKey))
			{
				return "Svg";
			}
			return "Glyph";
		}
	}

	public bool IsEnabled { get; }

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

	public DownloadVersionCategory(string id, string title, string icon, string? iconKey = null, bool isEnabled = true)
	{
		Id = id;
		Title = title;
		Icon = icon;
		IconKey = iconKey;
		IsEnabled = isEnabled;
	}
}
