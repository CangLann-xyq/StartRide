using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.Core;

namespace StartRide.App.ViewModels.Multiplayer
{
	/// <summary>建房页里一个可选玩法（列表项）。</summary>
	public sealed class GameModeOptionItem : ObservableObject
	{
		public GameModeOptionItem(LobbyGameMode mode)
		{
			Mode = mode;
			Name = mode.Title;
			Subtitle = mode.Summary;
			Detail = mode.Detail;
		}

		public LobbyGameMode Mode { get; }

		public string Id => Mode.Id;

		public string Name { get; }

		/// <summary>一句话说明（列表副标题）。</summary>
		public string Subtitle { get; }

		/// <summary>完整规则说明（详情区）。</summary>
		public string Detail { get; }

		public string IconKey => Mode.IconKey;

		public int Revision => Mode.Revision;

		public IReadOnlyList<string> LocalTags => Mode.Tags;

		public bool HasTags => Mode.HasTags;

		// 列表首尾行的分隔线要收掉（ListPageItemButton 的既有约定）
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
}
