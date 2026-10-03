using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using StartRide.App.Resources;
using StartRide.Core;

namespace StartRide.App.ViewModels.Multiplayer;

/// <summary>联机页里一张可选地图（列表项）。</summary>
public sealed class LevelOptionItem : ObservableObject
{
	public LevelOptionItem(BeamNgLevel level)
	{
		Level = level;
		Name = string.IsNullOrWhiteSpace(level.Name) ? level.Id : level.Name;
		Meta = level.SpawnPoints.Count > 0
			? string.Format(Strings.Lobby_SpawnCountFormat, level.SpawnPoints.Count)
			: Strings.Lobby_SpawnDefaultOnly;
		Subtitle = BuildSubtitle(level);
	}

	public BeamNgLevel Level { get; }

	public string Id => Level.Id;

	public string Name { get; }

	/// <summary>列表右侧的元信息，例如「13 个出生点」。</summary>
	public string Meta { get; }

	/// <summary>副标题：把地图自己的介绍压成一行（列表里只放得下这么长）。</summary>
	public string Subtitle { get; }

	/// <summary>地图的原始介绍（多行也会原样保留，给详情区用）。</summary>
	public string Description => Level.Description;

	/// <summary>缩略图的 file:/// 路径，取不到就是空串。</summary>
	public string PreviewPath => Level.PreviewPath;

	public bool HasPreview => !string.IsNullOrWhiteSpace(Level.PreviewPath);

	public bool HasSpawnPoints => Level.SpawnPoints.Count > 0;

	public bool IsMod => Level.IsMod;

	/// <summary>mod 地图后面挂的小胶囊。</summary>
	public IReadOnlyList<string> LocalTags => IsMod ? new[] { Strings.Lobby_LevelModTag } : Array.Empty<string>();

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

	/// <summary>
	/// 副标题：把地图自己的介绍压成一行（列表里只放得下这么长）。
	/// 游戏没给这张图写介绍时留空 —— 宁可只显示名字，也不要把 industrial / italy
	/// 这种内部 id 当成介绍摆出来。
	/// </summary>
	private static string BuildSubtitle(BeamNgLevel level)
	{
		string text = level.Description;
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		return text.Replace('\r', ' ').Replace('\n', ' ').Trim();
	}
}

/// <summary>联机页里一个可选出生点（列表项）。</summary>
public sealed class SpawnOptionItem : ObservableObject
{
	public SpawnOptionItem(BeamNgSpawnPoint point)
	{
		Point = point;
		Name = string.IsNullOrWhiteSpace(point.Name) ? point.ObjectName : point.Name;
		Subtitle = BuildDescription(point);
	}

	public BeamNgSpawnPoint Point { get; }

	/// <summary>scenetree 对象名，发给游戏模组的就是它。</summary>
	public string ObjectName => Point.ObjectName;

	public string Name { get; }

	/// <summary>出生点的一句话介绍（游戏自己的词条），没有就退回对象名。</summary>
	public string Subtitle { get; }

	public bool IsDefault => Point.IsDefault;

	/// <summary>默认出生点后面挂的小胶囊「默认」。</summary>
	public IReadOnlyList<string> LocalTags =>
		Point.IsDefault ? new[] { Strings.Lobby_SpawnIsDefaultTag } : Array.Empty<string>();

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

	/// <summary>出生点的一句话介绍（游戏自己的词条）；没写就留空，不摆内部对象名。</summary>
	private static string BuildDescription(BeamNgSpawnPoint point)
	{
		string text = point.Description;
		if (string.IsNullOrWhiteSpace(text)) return string.Empty;
		return text.Replace('\r', ' ').Replace('\n', ' ').Trim();
	}
}
