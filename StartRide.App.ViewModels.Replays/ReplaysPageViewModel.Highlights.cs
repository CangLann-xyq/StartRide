using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StartRide.Core;

namespace StartRide.App.ViewModels.Replays;

/// <summary>
/// 高光时刻列表里的一条 = 某局里的某条高光 + 它所属的那一局（地图/时间/录像文件）。
///
/// 为什么要包一层：HighlightItem 是纯数据模型（反序列化用），不能带 IsSelected 这类
/// UI 状态；列表又是**跨局扁平**展示（一屏看完所有高光，而不是先选局再看高光）。
/// </summary>
public sealed class HighlightEntry : ObservableObject
{
	private bool isSelected;

	public HighlightItem Item { get; }

	public HighlightSession Session { get; }

	public HighlightEntry(HighlightItem item, HighlightSession session)
	{
		Item = item;
		Session = session;
	}

	public string Type => Item.Type;

	/// <summary>"大跳跃 2.5 秒 · 61 米" —— 类型 + 数值，这一行就是全部信息量。</summary>
	public string Title
	{
		get
		{
			string v = Item.ValueText;
			return string.IsNullOrWhiteSpace(v) ? Item.Title : Item.Title + " " + v;
		}
	}

	/// <summary>副标题：地图 · 何时。</summary>
	public string Subtitle => Session.MapText + " · " + Session.AgoText;

	public string TimeCodeText => Item.TimeCodeText;

	/// <summary>鼠标悬停时的完整信息（含录像文件名与房间），列表上放不下。</summary>
	public string DetailText
	{
		get
		{
			var parts = new List<string> { Title, "地图 " + Session.MapText, "时间码 " + TimeCodeText };
			if (!string.IsNullOrWhiteSpace(Item.ReplayFileName)) parts.Add("录像 " + Item.ReplayFileName);
			if (Session.Room != null && !string.IsNullOrWhiteSpace(Session.Room.Name)) parts.Add("房间 " + Session.Room.Name);
			if (!string.IsNullOrWhiteSpace(Session.Player)) parts.Add("玩家 " + Session.Player);
			if (!Item.HasShot) parts.Add("（这条没有截图，截图每 2 秒最多一张）");
			return string.Join("　·　", parts);
		}
	}

	public ImageSource? ShotImage => Item.ShotImage;

	public bool HasShot => Item.HasShot;

	/// <summary>有截图就定位截图，没有就退回这一局的 JSON —— 点一下总要有东西发生。</summary>
	public string? RevealTarget
	{
		get
		{
			if (Item.HasShot) return Item.ShotPath;
			return string.IsNullOrWhiteSpace(Session.JsonPath) ? null : Session.JsonPath;
		}
	}

	public string? ReplayPath => string.IsNullOrWhiteSpace(Item.Replay) ? null : Item.Replay;

	public bool HasReplay => ReplayPath != null;

	public bool IsSelected
	{
		get => isSelected;
		set => SetProperty(ref isSelected, value);
	}

	/// <summary>复制到剪贴板的一行文本（贴到群里就能说明这是哪一条）。</summary>
	public string ShareText
	{
		get
		{
			string head = "【" + Title + "】" + Session.MapText + " @ " + (Item.At ?? "");
			if (!string.IsNullOrWhiteSpace(Item.ReplayFileName))
			{
				head += "　录像：" + Item.ReplayFileName + "（" + TimeCodeText + "）";
			}
			return head;
		}
	}
}

/// <summary>高光类型筛选按钮。</summary>
public sealed partial class HighlightFilterChip : ObservableObject
{
	private bool isActive;

	/// <summary>空串 = 全部。</summary>
	public string Type { get; }

	public string Label { get; }

	/// <summary>该类型一共多少条（"全部"就是总条数）。</summary>
	public int Count { get; set; }

	public string Text => Count > 0 ? Label + " " + Count : Label;

	public bool IsActive
	{
		get => isActive;
		set => SetProperty(ref isActive, value);
	}

	public HighlightFilterChip(string type, string label)
	{
		Type = type;
		Label = label;
	}
}

public sealed partial class ReplaysPageViewModel
{
	/// <summary>0 = 回放文件，1 = 高光时刻。</summary>
	private int replaysViewMode;

	private string searchQuery = "";

	private string highlightFilterType = "";

	private HighlightEntry? selectedHighlight;

	private RelayCommand? showReplaysViewCommand;
	private RelayCommand? showHighlightsViewCommand;
	private RelayCommand? openHighlightsFolderCommand;
	private RelayCommand<HighlightFilterChip>? filterHighlightsCommand;
	private RelayCommand<HighlightEntry>? selectHighlightCommand;
	private RelayCommand<HighlightEntry>? revealHighlightCommand;
	private RelayCommand<HighlightEntry>? revealHighlightReplayCommand;
	private RelayCommand<HighlightEntry>? copyHighlightCommand;

	private readonly List<HighlightEntry> allHighlights = new();

	// ── 高光设置（写到 <回放目录>/startride/config.json 给游戏内模组读）──
	private bool highlightConfigPushed;
	private bool captureEnabled = true;
	private bool captureAutoRecord = true;
	private bool captureInSinglePlayer;

	/// <summary>总开关（对应设置里的"精彩瞬间自动捕捉"）。</summary>
	public bool CaptureEnabled
	{
		get => captureEnabled;
		set { if (captureEnabled != value) { captureEnabled = value; OnPropertyChanged(); PushCaptureSettings(); } }
	}

	/// <summary>联机时自动开始录制回放。</summary>
	public bool CaptureAutoRecord
	{
		get => captureAutoRecord;
		set { if (captureAutoRecord != value) { captureAutoRecord = value; OnPropertyChanged(); PushCaptureSettings(); } }
	}

	/// <summary>单人开车也记录（默认关：随便跑一圈也生成记录会白占磁盘）。</summary>
	public bool CaptureInSinglePlayer
	{
		get => captureInSinglePlayer;
		set { if (captureInSinglePlayer != value) { captureInSinglePlayer = value; OnPropertyChanged(); PushCaptureSettings(); } }
	}

	/// <summary>设置写盘 —— 同时更新启动器设置与模组配置，两处不能只改一处。</summary>
	private void PushCaptureSettings()
	{
		try
		{
			AppSettings s = AppSettings.Current;
			s.HighlightCaptureEnabled = captureEnabled;
			s.HighlightAutoRecord = captureAutoRecord;
			s.HighlightInSinglePlayer = captureInSinglePlayer;
			s.Save();
		}
		catch
		{
			// 存不下也不该让开关点不动
		}
		try
		{
			HighlightStore.PushModConfig(AppSettings.Current);
		}
		catch
		{
			// 同上
		}
	}

	/// <summary>跨局扁平的高光列表（按时间倒序）。</summary>
	public ObservableCollection<HighlightEntry> Highlights { get; } = new();

	public ObservableCollection<HighlightFilterChip> HighlightFilters { get; } = new();

	/// <summary>筛完后一条都没有（但确实有高光数据）时，列表区显示这句。</summary>
	public string HighlightsEmptyText { get; private set; } = "";

	public string HighlightStatText { get; private set; } = "";

	/// <summary>高光目录（&lt;replays&gt;/startride）。可能不存在。</summary>
	public string HighlightsDirectory => HighlightStore.ResolveDirectory(ReplaysDirectory);

	public bool IsReplaysMode => replaysViewMode == 0;

	public bool IsHighlightsMode => replaysViewMode == 1;

	public bool HasHighlights => allHighlights.Count > 0;

	/// <summary>一条高光都没有（用来显示引导卡片）。</summary>
	public bool NoHighlights => allHighlights.Count == 0;

	public bool HasHighlightSessions { get; private set; }

	/// <summary>筛选条只在"高光模式 + 确实有数据"时出现。</summary>
	public bool IsHighlightFilterVisible => IsHighlightsMode && allHighlights.Count > 0;

	/// <summary>
	/// 搜索框同时服务两个列表：回放模式筛地图名，高光模式筛地图/类型/房间。
	/// 存一个共用字段，切模式时不用清空（用户视角是"同一个框"）。
	/// </summary>
	public string SearchQuery
	{
		get => searchQuery;
		set
		{
			if (SetProperty(ref searchQuery, value ?? ""))
			{
				ApplyReplayFilter();
				ApplyHighlightFilter();
			}
		}
	}

	public HighlightEntry? SelectedHighlight
	{
		get => selectedHighlight;
		private set
		{
			if (SetProperty(ref selectedHighlight, value))
			{
				OnPropertyChanged(nameof(HasHighlightSelection));
			}
		}
	}

	public bool HasHighlightSelection => selectedHighlight != null;

	public IRelayCommand ShowReplaysViewCommand =>
		showReplaysViewCommand ?? (showReplaysViewCommand = new RelayCommand(() => SetViewMode(0)));

	public IRelayCommand ShowHighlightsViewCommand =>
		showHighlightsViewCommand ?? (showHighlightsViewCommand = new RelayCommand(() => SetViewMode(1)));

	/// <summary>在资源管理器里打开高光目录（截图和 JSON 都在这儿）。</summary>
	public IRelayCommand OpenHighlightsFolderCommand =>
		openHighlightsFolderCommand ?? (openHighlightsFolderCommand = new RelayCommand(OpenHighlightsFolder));

	public IRelayCommand<HighlightFilterChip> FilterHighlightsCommand =>
		filterHighlightsCommand ?? (filterHighlightsCommand = new RelayCommand<HighlightFilterChip>(ApplyFilterChip));

	public IRelayCommand<HighlightEntry> SelectHighlightCommand =>
		selectHighlightCommand ?? (selectHighlightCommand = new RelayCommand<HighlightEntry>(SelectHighlight));

	public IRelayCommand<HighlightEntry> RevealHighlightCommand =>
		revealHighlightCommand ?? (revealHighlightCommand = new RelayCommand<HighlightEntry>(RevealHighlight));

	public IRelayCommand<HighlightEntry> RevealHighlightReplayCommand =>
		revealHighlightReplayCommand ?? (revealHighlightReplayCommand = new RelayCommand<HighlightEntry>(RevealHighlightReplay));

	public IRelayCommand<HighlightEntry> CopyHighlightCommand =>
		copyHighlightCommand ?? (copyHighlightCommand = new RelayCommand<HighlightEntry>(CopyHighlight));

	private void SetViewMode(int mode)
	{
		if (replaysViewMode == mode)
		{
			return;
		}
		replaysViewMode = mode;
		OnPropertyChanged(nameof(IsReplaysMode));
		OnPropertyChanged(nameof(IsHighlightsMode));
		OnPropertyChanged(nameof(IsHighlightFilterVisible));
		OnPropertyChanged(nameof(NeedsGuide));      // 引导卡片只在回放模式出现
		// 切到高光模式时顺手重扫一遍 —— 用户很可能刚在游戏里跑完一局
		if (mode == 1)
		{
			RefreshHighlights();
		}
	}

	private void ApplyFilterChip(HighlightFilterChip? chip)
	{
		if (chip == null)
		{
			return;
		}
		highlightFilterType = chip.Type;
		foreach (HighlightFilterChip c in HighlightFilters)
		{
			c.IsActive = ReferenceEquals(c, chip);
		}
		ApplyHighlightFilter();
	}

	private void SelectHighlight(HighlightEntry? entry)
	{
		if (entry == null)
		{
			return;
		}
		foreach (HighlightEntry e in Highlights)
		{
			e.IsSelected = ReferenceEquals(e, entry);
		}
		SelectedHighlight = entry;
	}

	private void RevealHighlight(HighlightEntry? entry)
	{
		string? target = entry?.RevealTarget;
		if (string.IsNullOrWhiteSpace(target))
		{
			AppState.Current.Notify("这条高光没有留下文件");
			return;
		}
		RevealInExplorer(target!);
	}

	private void RevealHighlightReplay(HighlightEntry? entry)
	{
		string? target = entry?.ReplayPath;
		if (string.IsNullOrWhiteSpace(target))
		{
			AppState.Current.Notify("这条高光没有对应录像");
			return;
		}
		RevealInExplorer(target!);
	}

	private void CopyHighlight(HighlightEntry? entry)
	{
		if (entry == null)
		{
			return;
		}
		try
		{
			Clipboard.SetText(entry.ShareText);
			AppState.Current.Notify("已复制这条高光的信息");
		}
		catch (Exception ex)
		{
			MessageBox.Show("复制失败：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void RevealInExplorer(string path)
	{
		try
		{
			if (File.Exists(path) || Directory.Exists(path))
			{
				Process.Start("explorer.exe", "/select,\"" + path + "\"");
			}
			else
			{
				// 文件可能已经被删了 —— 退而打开它应该在的目录
				string dir = Path.GetDirectoryName(path) ?? "";
				if (Directory.Exists(dir))
				{
					Process.Start("explorer.exe", "\"" + dir + "\"");
				}
				else
				{
					AppState.Current.Notify("文件已不存在");
				}
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("无法打开文件位置：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void OpenHighlightsFolder()
	{
		string dir = HighlightsDirectory;
		if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
		{
			AppState.Current.Notify("还没有高光记录。联机跑一局就有了。");
			return;
		}
		try
		{
			Process.Start("explorer.exe", "\"" + dir + "\"");
		}
		catch (Exception ex)
		{
			MessageBox.Show("无法打开目录：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	/// <summary>
	/// 重扫高光会话。坏文件/半截文件由 HighlightStore 内部吞掉（游戏随时可能被强杀，
	/// 这些 JSON 是"随时可能在写"的），这里不再兜第二层。
	/// </summary>
	private void RefreshHighlights()
	{
		EnsureCaptureSettings();

		List<HighlightSession> sessions = HighlightStore.LoadAll(ReplaysDirectory);
		HasHighlightSessions = sessions.Count > 0;

		allHighlights.Clear();
		foreach (HighlightSession s in sessions)
		{
			foreach (HighlightItem it in s.Items)
			{
				allHighlights.Add(new HighlightEntry(it, s));
			}
		}
		// 同一局里按时间码升序（还原当时的顺序），跨局按会话倒序（新的在前）
		allHighlights.Sort((a, b) =>
		{
			DateTime da = a.Session.StartedLocal ?? DateTime.MinValue;
			DateTime db = b.Session.StartedLocal ?? DateTime.MinValue;
			int c = db.CompareTo(da);
			return c != 0 ? c : a.Item.Offset.CompareTo(b.Item.Offset);
		});

		RebuildHighlightFilters(sessions);
		ApplyHighlightFilter();
		RebuildHighlightStats(sessions);
		OnPropertyChanged(nameof(HasHighlights));
		OnPropertyChanged(nameof(NoHighlights));
		OnPropertyChanged(nameof(HasHighlightSessions));
		OnPropertyChanged(nameof(IsHighlightFilterVisible));
	}

	/// <summary>
	/// 首次进页面时把设置读进来，并**把配置推给模组**。
	/// 只做一次：RefreshHighlights 每次切模式都会跑，重复推没必要。
	/// </summary>
	private void EnsureCaptureSettings()
	{
		if (highlightConfigPushed)
		{
			return;
		}
		highlightConfigPushed = true;
		try
		{
			AppSettings s = AppSettings.Load();
			captureEnabled = s.HighlightCaptureEnabled;
			captureAutoRecord = s.HighlightAutoRecord;
			captureInSinglePlayer = s.HighlightInSinglePlayer;
		}
		catch
		{
			// 读不到就用字段默认值（都是"开"，对用户最友好的降级）
		}
		OnPropertyChanged(nameof(CaptureEnabled));
		OnPropertyChanged(nameof(CaptureAutoRecord));
		OnPropertyChanged(nameof(CaptureInSinglePlayer));
		try
		{
			HighlightStore.PushModConfig(AppSettings.Current);
		}
		catch
		{
			// 推不进去不影响页面
		}
	}

	private void RebuildHighlightFilters(List<HighlightSession> sessions)
	{
		HighlightFilters.Clear();
		HighlightFilters.Add(new HighlightFilterChip("", "全部") { IsActive = highlightFilterType.Length == 0 });
		// 固定顺序，不按数量排 —— 按钮位置来回变会让人点错
		foreach ((string type, string label) in new[]
		{
			("jump", "大跳跃"), ("impact", "重击"), ("rollover", "翻车"),
			("burnout", "烧胎"), ("topspeed", "极速"),
		})
		{
			int n = allHighlights.Count(e => e.Type == type);
			if (n == 0 && highlightFilterType != type)
			{
				continue;      // 没这个类型就别摆个 0 条的空按钮
			}
			HighlightFilters.Add(new HighlightFilterChip(type, label)
			{
				Count = n,
				IsActive = highlightFilterType == type,
			});
		}
		int total = allHighlights.Count;
		HighlightFilters[0].Count = total;
	}

	private void ApplyHighlightFilter()
	{
		string q = searchQuery.Trim();
		Highlights.Clear();
		foreach (HighlightEntry e in allHighlights)
		{
			if (highlightFilterType.Length > 0 && e.Type != highlightFilterType)
			{
				continue;
			}
			if (q.Length > 0 && !Matches(e, q))
			{
				continue;
			}
			Highlights.Add(e);
		}

		bool filtered = highlightFilterType.Length > 0 || q.Length > 0;
		HighlightsEmptyText = allHighlights.Count > 0 && Highlights.Count == 0
			? "没有符合条件的高光。换个筛选项，或清空搜索。"
			: allHighlights.Count == 0
				? "还没有高光记录"
				: "";
		OnPropertyChanged(nameof(HighlightsEmptyText));
		OnPropertyChanged(nameof(HasHighlights));

		// 选中的那条被筛掉了就取消选中，避免右侧/详情指向看不见的东西
		if (selectedHighlight != null && !Highlights.Contains(selectedHighlight))
		{
			selectedHighlight.IsSelected = false;
			SelectedHighlight = null;
		}
		_ = filtered;
	}

	private static bool Matches(HighlightEntry e, string q)
	{
		return Contains(e.Item.Title, q)
			|| Contains(e.Item.Type, q)
			|| Contains(e.Session.Map, q)
			|| Contains(e.Session.Player, q)
			|| Contains(e.Session.Room?.Name, q)
			|| Contains(e.Item.ReplayFileName, q)
			|| Contains(e.Session.AtText, q);
	}

	private static bool Contains(string? haystack, string needle)
	{
		return !string.IsNullOrEmpty(haystack)
			&& haystack!.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private void RebuildHighlightStats(List<HighlightSession> sessions)
	{
		if (allHighlights.Count == 0)
		{
			HighlightStatText = "";
		}
		else
		{
			int games = sessions.Count(s => s.HasHighlights);
			int seconds = (int)Math.Round(sessions.Where(s => s.HasHighlights).Sum(s => s.DurationSeconds));
			string dur = seconds < 60
				? seconds + " 秒"
				: (seconds / 60) + " 分钟";
			HighlightStatText = allHighlights.Count.ToString(CultureInfo.InvariantCulture)
				+ " 条高光 · " + games + " 局 · 共 " + dur;
		}
		OnPropertyChanged(nameof(HighlightStatText));
	}
}
