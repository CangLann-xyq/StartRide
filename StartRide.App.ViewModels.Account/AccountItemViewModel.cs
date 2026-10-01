using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Launcher.Application.Accounts;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Account;

public sealed class AccountItemViewModel : ObservableObject
{
	[ObservableProperty]
	private bool isSelected;

	public LauncherAccount Account { get; }

	public string Id => Account.Id;

	public string DisplayName => Account.DisplayName;

	public string? Uuid => Account.Uuid;

	public bool IsOffline => Account.IsOffline;

	public LauncherAccountKind Kind => Account.Kind;

	public string AvatarUrl => Account.AvatarUrl;

	public string AvatarSource => Account.AvatarSource;

	public bool HasAvatar => !string.IsNullOrWhiteSpace(Account.AvatarSource);

	public MinecraftSkinModel? SkinModel => Account.SkinModel;

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

	public AccountItemViewModel(LauncherAccount account)
	{
		Account = account;
	}
}
