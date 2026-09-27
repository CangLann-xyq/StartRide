using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.Domain.Models;

namespace StartRide.App.Models;

public sealed class NavigationItem : ObservableObject
{
	[ObservableProperty]
	private bool isSelected;

	[ObservableProperty]
	private string? avatarUrl;

	public required string Page { get; init; }

	public required string Title { get; init; }

	public required string Icon { get; init; }

	public string? IconKey { get; init; }

	public LoaderKind? Loader { get; init; }

	public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);

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
	public string? AvatarUrl
	{
		get
		{
			return avatarUrl;
		}
		set
		{
			if (!EqualityComparer<string>.Default.Equals(avatarUrl, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.AvatarUrl);
				avatarUrl = value;
				OnAvatarUrlChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AvatarUrl);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnAvatarUrlChanged(string? value)
	{
		OnPropertyChanged("HasAvatar");
	}
}
