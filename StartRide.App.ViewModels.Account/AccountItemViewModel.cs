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

	/// <summary>Steam/本地头像：可为本地绝对路径或 URL；为空时界面回退通用人形图标。</summary>
	public string AvatarSource => Account.AvatarSource;

	/// <summary>是否有可用头像（决定列表项用头像还是通用图标）。</summary>
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
