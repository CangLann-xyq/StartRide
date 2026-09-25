using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StartRide.Core;

namespace StartRide.App.ViewModels.Replays;

/// <summary>回放文件条目。</summary>
public sealed class ReplayItem : ObservableObject
{
	private bool isSelected;

	public string Name { get; }
	public string FilePath { get; }
	public string FileSizeText { get; }
	public string MapName { get; }

	/// <summary>原始字节数（排序/统计用）。</summary>
	public long Bytes { get; }

	/// <summary>录制时间字符串（文件名前 19 位就是 yyyy-MM-dd_HH-mm-ss）。</summary>
	public string RecordedAtText { get; }

	/// <summary>相对今天的人话时间，例如"3 天前"。</summary>
	public string RecordedAgoText { get; }

	public string FileName { get; }

	public string DirectoryPath { get; }

	public bool IsSelected
	{
		get => isSelected;
		set => SetProperty(ref isSelected, value);
	}

	public ReplayItem(string name, string filePath, string fileSizeText, string mapName,
		long bytes = 0, DateTime? recordedAt = null)
	{
		Name = name;
		FilePath = filePath;
		FileSizeText = fileSizeText;
		MapName = mapName;
		Bytes = bytes;
		FileName = Path.GetFileName(filePath);
		DirectoryPath = Path.GetDirectoryName(filePath) ?? "";
		RecordedAtText = recordedAt.HasValue ? recordedAt.Value.ToString("yyyy-MM-dd HH:mm") : "";
		RecordedAgoText = DescribeAgo(recordedAt);
	}

	private static string DescribeAgo(DateTime? at)
	{
		if (!at.HasValue) return "";
		TimeSpan d = DateTime.Now - at.Value;
		if (d.TotalMinutes < 1) return "刚刚";
		if (d.TotalHours < 1) return (int)d.TotalMinutes + " 分钟前";
		if (d.TotalDays < 1) return (int)d.TotalHours + " 小时前";
		if (d.TotalDays < 30) return (int)d.TotalDays + " 天前";
		if (d.TotalDays < 365) return (int)(d.TotalDays / 30) + " 个月前";
		return (int)(d.TotalDays / 365) + " 年前";
	}
}

/// <summary>
/// 回放管理页：扫描本机 BeamNG 用户数据目录的 replays/*.rpl。
/// 识别不到回放目录/文件时，显示"启动游戏打开回放"引导（BeamNG 官方回放功能）。
/// </summary>
public sealed class ReplaysPageViewModel : ObservableObject
{
	private string statusMessage = "";
	private bool hasReplays;
	private bool canLaunchGame;
	private RelayCommand? openGameForReplayCommand;
	private RelayCommand<ReplayItem>? openReplayLocationCommand;
	private RelayCommand<ReplayItem>? selectReplayCommand;
	private RelayCommand<ReplayItem>? copyReplayPathCommand;
	private RelayCommand? sortByNewestCommand;
	private RelayCommand? sortByOldestCommand;
	private RelayCommand? sortBySizeCommand;
	private ReplayItem? selectedReplay;
	private int sortMode;
	private FileSetSummary summary = new();

	/// <summary>当前选中的回放，右侧详情面板绑它。</summary>
	public ReplayItem? SelectedReplay
	{
		get => selectedReplay;
		private set
		{
			if (SetProperty(ref selectedReplay, value))
			{
				OnPropertyChanged(nameof(HasSelection));
				OnPropertyChanged(nameof(SelectedTitle));
				OnPropertyChanged(nameof(SelectedMapName));
				OnPropertyChanged(nameof(SelectedSizeText));
				OnPropertyChanged(nameof(SelectedRecordedAtText));
				OnPropertyChanged(nameof(SelectedRecordedAgoText));
				OnPropertyChanged(nameof(SelectedFileName));
				OnPropertyChanged(nameof(SelectedDirectoryPath));
			}
		}
	}

	public bool HasSelection => selectedReplay != null;

	public string SelectedTitle => selectedReplay?.Name ?? "";

	public string SelectedMapName => selectedReplay?.MapName ?? "";

	public string SelectedSizeText => selectedReplay?.FileSizeText ?? "";

	public string SelectedRecordedAtText => selectedReplay?.RecordedAtText ?? "";

	public string SelectedRecordedAgoText => selectedReplay?.RecordedAgoText ?? "";

	public string SelectedFileName => selectedReplay?.FileName ?? "";

	public string SelectedDirectoryPath => selectedReplay?.DirectoryPath ?? "";

	/// <summary>顶部统计条。</summary>
	public string StatCountText => summary.CountText;

	public string StatTotalText => summary.TotalText;

	public string StatAverageText => summary.AverageText;

	public string StatMaxText => summary.MaxText;

	/// <summary>一共涉及多少张地图——比"总数"更能说明内容构成。</summary>
	public string StatMapCountText { get; private set; } = "0";

	public bool IsSortByNewest => sortMode == 0;

	public bool IsSortByOldest => sortMode == 1;

	public bool IsSortBySize => sortMode == 2;

	public ObservableCollection<ReplayItem> Replays { get; } = new();

	public bool HasReplays
	{
		get => hasReplays;
		private set => SetProperty(ref hasReplays, value);
	}

	public string StatusMessage
	{
		get => statusMessage;
		private set => SetProperty(ref statusMessage, value);
	}

	public string ReplaysDirectory { get; }

	/// <summary>是否未检测到任何回放（用于显示引导卡片）。</summary>
	public bool NeedsGuide => !HasReplays;

	public bool CanLaunchGame
	{
		get => canLaunchGame;
		private set => SetProperty(ref canLaunchGame, value);
	}

	public ReplaysPageViewModel()
	{
		var settings = AppSettings.Load();
		ReplaysDirectory = settings.ResolveReplaysDirectory();
		var launcher = new GameLauncher(settings);
		CanLaunchGame = launcher.IsInstalled;
		LoadReplays();
	}

	public IRelayCommand OpenGameForReplayCommand =>
		openGameForReplayCommand ?? (openGameForReplayCommand = new RelayCommand(OpenGameForReplay));

	/// <summary>在资源管理器中定位某个回放文件。</summary>
	public IRelayCommand<ReplayItem> OpenReplayLocationCommand =>
		openReplayLocationCommand ?? (openReplayLocationCommand = new RelayCommand<ReplayItem>(OpenReplayLocation));

	/// <summary>点行 = 选中（右侧详情面板跟着换）。</summary>
	public IRelayCommand<ReplayItem> SelectReplayCommand =>
		selectReplayCommand ?? (selectReplayCommand = new RelayCommand<ReplayItem>(SelectReplay));

	public IRelayCommand<ReplayItem> CopyReplayPathCommand =>
		copyReplayPathCommand ?? (copyReplayPathCommand = new RelayCommand<ReplayItem>(CopyReplayPath));

	public IRelayCommand SortByNewestCommand =>
		sortByNewestCommand ?? (sortByNewestCommand = new RelayCommand(() => ApplySort(0)));

	public IRelayCommand SortByOldestCommand =>
		sortByOldestCommand ?? (sortByOldestCommand = new RelayCommand(() => ApplySort(1)));

	public IRelayCommand SortBySizeCommand =>
		sortBySizeCommand ?? (sortBySizeCommand = new RelayCommand(() => ApplySort(2)));

	private void SelectReplay(ReplayItem? item)
	{
		if (item == null)
		{
			return;
		}
		foreach (ReplayItem r in Replays)
		{
			r.IsSelected = ReferenceEquals(r, item);
		}
		SelectedReplay = item;
	}

	private void CopyReplayPath(ReplayItem? item)
	{
		string path = item?.FilePath ?? "";
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}
		try
		{
			Clipboard.SetText(path);
			AppState.Current.Notify("路径已复制");
		}
		catch (Exception ex)
		{
			MessageBox.Show("复制路径失败：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void ApplySort(int mode)
	{
		if (sortMode == mode)
		{
			return;
		}
		sortMode = mode;
		OnPropertyChanged(nameof(IsSortByNewest));
		OnPropertyChanged(nameof(IsSortByOldest));
		OnPropertyChanged(nameof(IsSortBySize));
		LoadReplays();
	}

	private void OpenReplayLocation(ReplayItem? item)
	{
		if (item == null || string.IsNullOrWhiteSpace(item.FilePath))
		{
			return;
		}
		try
		{
			Process.Start("explorer.exe", "/select,\"" + item.FilePath + "\"");
		}
		catch (Exception ex)
		{
			MessageBox.Show("无法打开文件位置：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	/// <summary>启动 BeamNG.drive（用户进入游戏后按 Esc → Replay 即可回放）。</summary>
	private void OpenGameForReplay()
	{
		try
		{
			var settings = AppSettings.Load();
			var launcher = new GameLauncher(settings);
			if (!launcher.IsInstalled)
			{
				MessageBox.Show(
					"未检测到 BeamNG.drive 安装目录。\n请先在「全局设置 → 通用」里指定游戏目录。",
					"StartRide",
					MessageBoxButton.OK,
					MessageBoxImage.Information);
				return;
			}

			string? err = launcher.Launch(withMod: true);
			if (err != null)
			{
				MessageBox.Show("启动 BeamNG.drive 失败：" + err, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("启动游戏失败：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void LoadReplays()
	{
		string previous = selectedReplay?.Name ?? "";
		Replays.Clear();
		SelectedReplay = null;

		if (!Directory.Exists(ReplaysDirectory))
		{
			StatusMessage = "尚未生成回放。启动 BeamNG.drive 进入任意地图，按 Esc → Replay 即可录制回放。";
			HasReplays = false;
			RefreshStats(new List<ReplayItem>());
			return;
		}

		var entries = new List<ReplayItem>();
		foreach (string file in Directory.GetFiles(ReplaysDirectory, "*.rpl"))
		{
			string name = Path.GetFileNameWithoutExtension(file);
			// 回放文件名形如 "2026-02-17_14-39-56 west_coast_usa"，末段是地图名
			string mapName = "";
			int idx = name.LastIndexOf(' ');
			if (idx >= 0 && idx < name.Length - 1)
			{
				mapName = name.Substring(idx + 1);
			}

			long bytes = 0;
			DateTime? written = null;
			try
			{
				FileInfo fi = new FileInfo(file);
				bytes = fi.Length;
				written = fi.LastWriteTime;
			}
			catch
			{
				// 拿不到就当 0，不影响列表
			}

			entries.Add(new ReplayItem(name, file, FileSizeFormatter.Format(bytes), mapName, bytes, written));
		}

		entries = sortMode switch
		{
			1 => entries.OrderBy(r => r.RecordedAtText, StringComparer.Ordinal).ToList(),
			2 => entries.OrderByDescending(r => r.Bytes).ThenBy(r => r.Name, StringComparer.Ordinal).ToList(),
			_ => entries.OrderByDescending(r => r.RecordedAtText, StringComparer.Ordinal).ToList(),
		};

		foreach (ReplayItem r in entries)
		{
			Replays.Add(r);
		}

		StatusMessage = entries.Count == 0
			? "尚未生成回放。启动 BeamNG.drive 进入任意地图，按 Esc → Replay 即可录制回放。"
			: "";
		HasReplays = entries.Count > 0;
		OnPropertyChanged(nameof(NeedsGuide));
		RefreshStats(entries);

		if (!string.IsNullOrEmpty(previous))
		{
			ReplayItem? again = Replays.FirstOrDefault(r => r.Name == previous);
			if (again != null)
			{
				SelectReplay(again);
			}
		}

		// 云同步：回放列表上报云端
		try
		{
			AppState.Current.CloudSync.PushReplays(entries.Select(r => new { name = r.Name, map = r.MapName, size = r.FileSizeText }));
		}
		catch
		{
			// 云同步失败不影响本地列表
		}
	}

	private void RefreshStats(List<ReplayItem> items)
	{
		summary = FileSetSummary.From(items.Select(r => (r.Name, r.Bytes)));
		StatMapCountText = items
			.Where(r => !string.IsNullOrWhiteSpace(r.MapName))
			.Select(r => r.MapName)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Count()
			.ToString();
		OnPropertyChanged(nameof(StatCountText));
		OnPropertyChanged(nameof(StatTotalText));
		OnPropertyChanged(nameof(StatAverageText));
		OnPropertyChanged(nameof(StatMaxText));
		OnPropertyChanged(nameof(StatMapCountText));
	}
}
