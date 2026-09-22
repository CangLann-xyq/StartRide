using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.Domain.Models;

namespace Launcher.App.ViewModels.Download;

public sealed class DownloadLoaderOption : ObservableObject
{
	[ObservableProperty]
	private bool isSelected;

	public LoaderKind Kind { get; }

	public string Title { get; }

	public string Subtitle { get; }

	public string Icon { get; }

	public string? IconSource { get; }

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

	public DownloadLoaderOption(LoaderKind kind, string title, string subtitle, string icon, string? iconSource = null)
	{
		Kind = kind;
		Title = title;
		Subtitle = subtitle;
		Icon = icon;
		IconSource = iconSource;
	}
}
