using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Resources;
using StartRide.Core;

namespace StartRide.App.ViewModels.Multiplayer;

public sealed class PublicRoomItem : ObservableObject
{
	public PublicRoomItem(Room room)
	{
		Room = room;
	}

	public Room Room { get; }

	public string RoomCode => Room.Id;

	public string Name => string.IsNullOrWhiteSpace(Room.Name) ? Room.Id : Room.Name;

	public string Host => string.IsNullOrWhiteSpace(Room.Host) ? Strings.Multiplayer_LobbyOwnerPlaceholder : Room.Host;

	/// <summary>房主那张图的关卡 id，加入者要跟着进的那张。</summary>
	public string MapId => Room.Map ?? string.Empty;

	/// <summary>
	/// 房主那张图的名字。
	/// 由 VM 从关卡目录里查好后填进来（查目录要读游戏文件，不适合放在属性 getter 里顺手做）。
	/// </summary>
	public string MapName { get; set; } = "";

	public string Map => !string.IsNullOrWhiteSpace(MapName)
		? MapName
		: (string.IsNullOrWhiteSpace(Room.Map) ? "—" : Room.Map);

	/// <summary>地图胶囊的 tooltip：关卡 id 原文。</summary>
	public string MapTooltip => Room.Map ?? string.Empty;

	public string Mode => string.IsNullOrWhiteSpace(Room.Mode) ? "freeroam" : Room.Mode;

	public string HostText => string.Format(Strings.Multiplayer_RoomHostFormat, Host);

	public string PlayerCountText => string.Format(Strings.Multiplayer_RoomPlayerCountFormat, Room.Players, Room.Capacity);

	public int Players => Room.Players;

	public int Capacity => Room.Capacity;

	public bool HasPassword => !string.IsNullOrWhiteSpace(Room.Password);

	public bool IsFirst { get; set; }

	public bool IsLast { get; set; }

	[ObservableProperty]
	private bool isSelected;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsSelected
	{
		get => isSelected;
		set
		{
			if (EqualityComparer<bool>.Default.Equals(isSelected, value)) return;
			OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsSelected);
			isSelected = value;
			OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsSelected);
		}
	}
}
