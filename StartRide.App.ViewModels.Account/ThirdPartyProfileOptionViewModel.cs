using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.Application.Accounts;

namespace StartRide.App.ViewModels.Account;

public sealed class ThirdPartyProfileOptionViewModel(ThirdPartyProfileOption profile) : ObservableObject
{
	[ObservableProperty]
	private bool isSelected;

	public ThirdPartyProfileOption Profile { get; } = profile;

	public string Uuid => Profile.Uuid;

	public string Name => Profile.Name;

	public string AvatarSource => Profile.AvatarSource;

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
}
