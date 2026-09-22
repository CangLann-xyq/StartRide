using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;

namespace Launcher.App.ViewModels.GameSettings;

public sealed class GameSettingsDetailSectionItem : ObservableObject
{
	[ObservableProperty]
	private bool isSelected;

	public string Id { get; }

	public string Title { get; }

	public string Icon { get; } = string.Empty;

	public string IconKey { get; }

	public string IconMode => "Svg";

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

	public GameSettingsDetailSectionItem(string id, string title, string iconKey)
	{
		Id = id;
		Title = title;
		IconKey = iconKey;
	}
}
