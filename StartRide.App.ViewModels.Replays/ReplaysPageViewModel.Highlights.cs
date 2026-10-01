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

	public string Title
	{
		get
		{
			string v = Item.ValueText;
			return string.IsNullOrWhiteSpace(v) ? Item.Title : Item.Title + " " + v;
		}
	}

	public string Subtitle => Session.MapText + " · " + Session.AgoText;

	public string TimeCodeText => Item.TimeCodeText;

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

public sealed partial class HighlightFilterChip : ObservableObject
{
	private bool isActive;

	public string Type { get; }

	public string Label { get; }

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

	private bool highlightConfigPushed;
	private bool captureEnabled = true;
	private bool captureAutoRecord = true;
	private bool captureInSinglePlayer;

	public bool CaptureEnabled
	{
		get => captureEnabled;
		set { if (captureEnabled != value) { captureEnabled = value; OnPropertyChanged(); PushCaptureSettings(); } }
	}

	public bool CaptureAutoRecord
	{
		get => captureAutoRecord;
		set { if (captureAutoRecord != value) { captureAutoRecord = value; OnPropertyChanged(); PushCaptureSettings(); } }
	}

	public bool CaptureInSinglePlayer
	{
		get => captureInSinglePlayer;
		set { if (captureInSinglePlayer != value) { captureInSinglePlayer = value; OnPropertyChanged(); PushCaptureSettings(); } }
	}

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
		}
		try
		{
			HighlightStore.PushModConfig(AppSettings.Current);
		}
		catch
		{
		}
	}

	public ObservableCollection<HighlightEntry> Highlights { get; } = new();

	public ObservableCollection<HighlightFilterChip> HighlightFilters { get; } = new();

	public string HighlightsEmptyText { get; private set; } = "";

	public string HighlightStatText { get; private set; } = "";

	public string HighlightsDirectory => HighlightStore.ResolveDirectory(ReplaysDirectory);

	public bool IsReplaysMode => replaysViewMode == 0;

	public bool IsHighlightsMode => replaysViewMode == 1;

	public string HeaderTitle => IsHighlightsMode ? "高光时刻" : "回放";

	public bool HasHighlights => allHighlights.Count > 0;

	public bool NoHighlights => allHighlights.Count == 0;

	public bool HasHighlightSessions { get; private set; }

	public bool IsHighlightFilterVisible => IsHighlightsMode && allHighlights.Count > 0;

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

	public event Action<int>? ViewModeChanged;

	public void ApplyShellViewMode(int mode) => SetViewMode(mode, notifyShell: false);

	private void SetViewMode(int mode) => SetViewMode(mode, notifyShell: true);

	private void SetViewMode(int mode, bool notifyShell)
	{
		if (replaysViewMode == mode)
		{
			return;
		}
		replaysViewMode = mode;
		OnPropertyChanged(nameof(IsReplaysMode));
		OnPropertyChanged(nameof(IsHighlightsMode));
		OnPropertyChanged(nameof(HeaderTitle));
		OnPropertyChanged(nameof(IsHighlightFilterVisible));
		OnPropertyChanged(nameof(NeedsGuide));
		if (mode == 1)
		{
			RefreshHighlights();
		}
		if (notifyShell)
		{
			try
			{
				ViewModeChanged?.Invoke(mode);
			}
			catch
			{
			}
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

	private string? lastRevealPath;

	private DateTime lastRevealAt;

	private void RevealInExplorer(string path)
	{

		if (string.Equals(path, lastRevealPath, StringComparison.OrdinalIgnoreCase)
			&& (DateTime.UtcNow - lastRevealAt).TotalMilliseconds < 900)
		{
			return;
		}
		lastRevealPath = path;
		lastRevealAt = DateTime.UtcNow;
		try
		{
			if (File.Exists(path) || Directory.Exists(path))
			{
				Process.Start("explorer.exe", "/select,\"" + path + "\"");
			}
			else
			{
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
		}
	}

	private void RebuildHighlightFilters(List<HighlightSession> sessions)
	{
		HighlightFilters.Clear();
		HighlightFilters.Add(new HighlightFilterChip("", "全部") { IsActive = highlightFilterType.Length == 0 });
		foreach ((string type, string label) in new[]
		{
			("jump", "大跳跃"), ("impact", "重击"), ("rollover", "翻车"),
			("burnout", "烧胎"), ("topspeed", "极速"),
		})
		{
			int n = allHighlights.Count(e => e.Type == type);
			if (n == 0 && highlightFilterType != type)
			{
				continue;
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
