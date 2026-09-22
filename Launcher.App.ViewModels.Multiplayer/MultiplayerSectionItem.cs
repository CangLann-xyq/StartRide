using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;

namespace Launcher.App.ViewModels.Multiplayer;

public sealed class MultiplayerSectionItem : ObservableObject
{
	[ObservableProperty]
	private bool isSelected;

	public MultiplayerPageSection Section { get; }

	public string Title { get; }

	public string IconKey { get; }

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

	public MultiplayerSectionItem(MultiplayerPageSection section, string title, string iconKey)
	{
		Section = section;
		Title = title;
		IconKey = iconKey;
	}
}
