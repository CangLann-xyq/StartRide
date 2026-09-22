using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StartRide.Core;

namespace Launcher.App.ViewModels.Mods;

/// <summary>已安装模组条目（本机 mods/*.zip）。</summary>
public sealed class ModItem : ObservableObject
{
	public string Name { get; }
	public string FilePath { get; }
	public string FileSizeText { get; }

	public ModItem(string name, string filePath, string fileSizeText)
	{
		Name = name;
		FilePath = filePath;
		FileSizeText = fileSizeText;
	}
}

/// <summary>在线仓库的模组条目（来自 BeamNG 官方资源库列表页解析）。</summary>
public sealed class RepositoryModItem : ObservableObject
{
	public long ResourceId { get; }
	public string Slug { get; }
	public string Name { get; }
	public string Author { get; }
	public string Category { get; }
	public string Description { get; }
	public string Version { get; }
	public string RatingText { get; }
	public string DownloadsText { get; }

	/// <summary>资源详情页地址（下载前用它解析 download?version=NNN 直链）。</summary>
	public string PageUrl { get; }

	public RepositoryModItem(BeamNgModInfo info)
	{
		ResourceId = info.Id;
		Slug = info.Slug;
		Name = info.Name;
		Author = info.Author;
		Category = info.CategoryName;
		Description = info.Description;
		Version = info.Version;
		RatingText = info.RatingText;
		DownloadsText = info.DownloadsText;
		PageUrl = info.PageUrl;
	}
}

/// <summary>
/// 模组仓库页：在线数据实时拉取 BeamNG 官方资源库（www.beamng.com/resources）列表页解析。
/// 全库约 1500+ 页 / 15 万+ 条目 → 首屏并发拉 10 页（约 1000 条）快速展示，
/// 之后"滚动到底自动续拉"下一波，直到拉完全库。
/// 下载：先解析详情页的 download?version=NNN 直链，再走客户端分段并行下载（R2 支持 Range）。
/// </summary>
public sealed class ModsPageViewModel : ObservableObject
{
	private string repositoryStatusText = "";
	private string statusMessage = "";
	private string searchText = "";
	private string selectedCategory = "全部";
	private bool isOnlineView;
	private bool isDownloading;
	private bool isLoadingRepository;
	private bool isLoadingMore;
	private double downloadProgress;
	private string? downloadStatusText;
	private RelayCommand? showOnlineCommand;
	private RelayCommand? showLocalCommand;
	private RelayCommand? openModsFolderCommand;
	private RelayCommand? refreshRepositoryCommand;
	private RelayCommand? loadMoreCommand;
	private RelayCommand<ModItem?> revealModCommand;

	private List<RepositoryModItem> allRepositoryMods = new();
	private CancellationTokenSource? loadCts;
	private int nextPage = 1;
	private int totalPages;

	/// <summary>首屏并发拉取页数（约 1000 条快速展示）。</summary>
	private const int FirstWavePages = 10;

	/// <summary>滚动到底后续拉的页数。</summary>
	private const int LoadMorePages = 10;

	/// <summary>在线视图分类选项（与 BeamNgRepositoryClient 的映射对应）。</summary>
	private static readonly string[] Categories = { "全部", "车辆", "地图", "涂装", "场景", "界面应用", "模组扩展", "音效", "其他" };

	public ObservableCollection<ModItem> Mods { get; } = new();
	public ObservableCollection<RepositoryModItem> RepositoryMods { get; } = new();
	public ObservableCollection<string> CategoryOptions { get; } = new();

	public bool HasMods => Mods.Count > 0;

	public string ModsDirectory { get; }

	public string RepositoryStatusText
	{
		get => repositoryStatusText;
		private set => SetProperty(ref repositoryStatusText, value);
	}

	public string StatusMessage
	{
		get => statusMessage;
		private set => SetProperty(ref statusMessage, value);
	}

	public bool IsOnlineView
	{
		get => isOnlineView;
		set
		{
			if (SetProperty(ref isOnlineView, value))
			{
				OnPropertyChanged(nameof(IsLocalView));
				if (value && RepositoryMods.Count == 0 && !IsLoadingRepository)
				{
					StartLoadRepository();
				}
			}
		}
	}

	public bool IsLocalView => !IsOnlineView;

	public string SearchText
	{
		get => searchText;
		set
		{
			if (SetProperty(ref searchText, value))
			{
				ApplyFilter();
			}
		}
	}

	public string SelectedCategory
	{
		get => selectedCategory;
		set
		{
			if (SetProperty(ref selectedCategory, value))
			{
				StartLoadRepository();
			}
		}
	}

	public bool IsDownloading
	{
		get => isDownloading;
		private set => SetProperty(ref isDownloading, value);
	}

	public bool IsLoadingRepository
	{
		get => isLoadingRepository;
		private set => SetProperty(ref isLoadingRepository, value);
	}

	public bool IsLoadingMore
	{
		get => isLoadingMore;
		private set => SetProperty(ref isLoadingMore, value);
	}

	public double DownloadProgress
	{
		get => downloadProgress;
		private set => SetProperty(ref downloadProgress, value);
	}

	public string? DownloadStatusText
	{
		get => downloadStatusText;
		private set => SetProperty(ref downloadStatusText, value);
	}

	public ModsPageViewModel()
	{
		ModsDirectory = new AppSettings().ResolveModsDirectory();
		foreach (string c in Categories)
		{
			CategoryOptions.Add(c);
		}
		LoadMods();
	}

	public IRelayCommand ShowOnlineCommand =>
		showOnlineCommand ?? (showOnlineCommand = new RelayCommand(ShowOnline));

	public IRelayCommand ShowLocalCommand =>
		showLocalCommand ?? (showLocalCommand = new RelayCommand(ShowLocal));

	public IRelayCommand OpenModsFolderCommand =>
		openModsFolderCommand ?? (openModsFolderCommand = new RelayCommand(OpenModsFolder));

	public IRelayCommand RefreshRepositoryCommand =>
		refreshRepositoryCommand ?? (refreshRepositoryCommand = new RelayCommand(StartLoadRepository));

	/// <summary>滚动到底部触发：续拉下一波页面（内部有防重入守卫）。</summary>
	public IRelayCommand LoadMoreCommand =>
		loadMoreCommand ?? (loadMoreCommand = new RelayCommand(() => _ = TryLoadMoreAsync()));

	/// <summary>点击本机模组行：在资源管理器中定位该 zip。</summary>
	public IRelayCommand<ModItem?> RevealModCommand =>
		revealModCommand ?? (revealModCommand = new RelayCommand<ModItem?>(RevealMod));

	private void ShowOnline()
	{
		IsOnlineView = true;
	}

	private void ShowLocal()
	{
		IsOnlineView = false;
	}

	/// <summary>启动在线仓库拉取（fire-and-forget，异常全部吞掉并显示失败状态，不崩 UI 线程）。</summary>
	private async void StartLoadRepository()
	{
		if (IsLoadingRepository)
		{
			return;
		}
		try
		{
			// 切换分类 / 刷新：取消上一轮未完成的续拉
			loadCts?.Cancel();
			loadCts?.Dispose();
			loadCts = new CancellationTokenSource();
			await LoadRepositoryAsync(loadCts.Token).ConfigureAwait(true);
		}
		catch (OperationCanceledException)
		{
			IsLoadingRepository = false;
		}
		catch (Exception ex)
		{
			IsLoadingRepository = false;
			RepositoryStatusText = "拉取官方仓库失败：" + ex.Message;
		}
	}

	private async Task LoadRepositoryAsync(CancellationToken ct)
	{
		IsLoadingRepository = true;
		IsLoadingMore = false;
		nextPage = 1;
		totalPages = 0;
		allRepositoryMods = new List<RepositoryModItem>();
		RepositoryMods.Clear();
		RepositoryStatusText = "正在拉取 BeamNG 官方仓库（第 1 页）…";
		try
		{
			string? slug = BeamNgRepositoryClient.ChineseToCategorySlug(SelectedCategory);
			var (firstPage, total) = await BeamNgRepositoryClient.FetchFirstPageAsync(slug, ct).ConfigureAwait(true);
			ct.ThrowIfCancellationRequested();
			totalPages = Math.Max(total, 1);
			AppendItems(firstPage);
			nextPage = 2;
			ApplyFilter();

			if (totalPages <= 1)
			{
				RepositoryStatusText = $"已从 BeamNG 官方仓库拉取 {allRepositoryMods.Count} 个模组";
			}
			else
			{
				RepositoryStatusText = $"已加载 {allRepositoryMods.Count} / 约 {FormatCount(totalPages * 100L)} 个模组 · 下拉到底自动继续加载";
			}

			// 首屏渲染后再自动续拉一波（共约 2000 条），之后由滚动触发。
			// 注意先解除 IsLoadingRepository 守卫，否则续拉会被防重入检查直接拦掉。
			IsLoadingRepository = false;
			if (nextPage <= totalPages && IsOnlineView)
			{
				_ = TryLoadMoreAsync();
			}
		}
		finally
		{
			IsLoadingRepository = false;
		}
	}

	/// <summary>续拉下一波页面（滚动到底触发）。返回是否真的启动了加载。</summary>
	public async Task<bool> TryLoadMoreAsync()
	{
		if (!IsOnlineView || IsLoadingRepository || IsLoadingMore || IsDownloading)
		{
			return false;
		}
		if (nextPage > totalPages || totalPages <= 0)
		{
			return false;
		}
		CancellationTokenSource? cts = loadCts;
		if (cts == null || cts.IsCancellationRequested)
		{
			return false;
		}

		IsLoadingMore = true;
		try
		{
			int from = nextPage;
			int to = Math.Min(from + LoadMorePages - 1, totalPages);
			RepositoryStatusText = $"正在加载第 {from}-{to} 页（共 {totalPages} 页）…";
			string? slug = BeamNgRepositoryClient.ChineseToCategorySlug(SelectedCategory);
			List<BeamNgModInfo> items = await BeamNgRepositoryClient.FetchPageRangeAsync(slug, from, to, cts.Token).ConfigureAwait(true);
			cts.Token.ThrowIfCancellationRequested();
			AppendItems(items);
			nextPage = to + 1;

			RepositoryStatusText = nextPage > totalPages
				? $"已加载全部 {allRepositoryMods.Count} 个模组（BeamNG 官方仓库）"
				: $"已加载 {allRepositoryMods.Count} / 约 {FormatCount(totalPages * 100L)} 个模组 · 下拉到底自动继续加载";
			return true;
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		catch (Exception ex)
		{
			RepositoryStatusText = "继续加载失败：" + (ex.InnerException?.Message ?? ex.Message) + "（下拉可重试）";
			return false;
		}
		finally
		{
			IsLoadingMore = false;
		}
	}

	private void AppendItems(List<BeamNgModInfo> items)
	{
		if (items.Count == 0)
		{
			return;
		}
		var existingIds = new HashSet<long>(allRepositoryMods.Select(m => m.ResourceId));
		foreach (BeamNgModInfo info in items)
		{
			if (existingIds.Add(info.Id))
			{
				allRepositoryMods.Add(new RepositoryModItem(info));
			}
		}
	}

	private static string FormatCount(long n)
	{
		return n.ToString("##,###");
	}

	private void OpenModsFolder()
	{
		try
		{
			if (!Directory.Exists(ModsDirectory))
			{
				Directory.CreateDirectory(ModsDirectory);
			}
			Process.Start(new ProcessStartInfo { FileName = ModsDirectory, UseShellExecute = true });
		}
		catch (Exception ex)
		{
			MessageBox.Show("无法打开模组目录：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	/// <summary>在资源管理器中定位本机模组文件。</summary>
	private void RevealMod(ModItem? item)
	{
		if (item == null || !File.Exists(item.FilePath))
		{
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.FilePath}\"") { UseShellExecute = true });
		}
		catch (Exception ex)
		{
			MessageBox.Show("无法打开文件位置：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	/// <summary>下载模组：先解析详情页拿到 download?version=NNN 直链，再分段并行下载 zip 到 mods 目录。</summary>
	public async Task DownloadModAsync(RepositoryModItem? item)
	{
		if (item == null || IsDownloading)
		{
			return;
		}
		IsDownloading = true;
		DownloadProgress = 0;
		DownloadStatusText = $"正在获取 {item.Name} 的下载链接…";
		try
		{
			string? downloadUrl = await BeamNgRepositoryClient.ResolveDownloadUrlAsync(item.PageUrl, CancellationToken.None).ConfigureAwait(true);
			if (string.IsNullOrEmpty(downloadUrl))
			{
				DownloadStatusText = "未找到下载链接";
				MessageBox.Show("该模组页面没有可用的下载链接（可能需要登录 BeamNG 官网）。", "StartRide", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (!Directory.Exists(ModsDirectory))
			{
				Directory.CreateDirectory(ModsDirectory);
			}
			string fileName = item.Slug + ".zip";
			string target = Path.Combine(ModsDirectory, fileName);

			DownloadStatusText = $"正在下载 {item.Name}…";

			// 进度回调来自后台线程：节流 100ms 后再跳回 UI 线程刷新，避免每块数据都打断 UI（卡死根源）
			long[] lastUiTick = new long[1];
			await BeamNgRepositoryClient.DownloadToFileAsync(downloadUrl, target, p =>
			{
				long now = Environment.TickCount64;
				bool finished = p.TotalBytes.HasValue && p.Bytes >= p.TotalBytes.Value;
				if (!finished && now - Volatile.Read(ref lastUiTick[0]) < 100)
				{
					return;
				}
				Volatile.Write(ref lastUiTick[0], now);
				System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
				{
					if (!IsDownloading)
					{
						return;
					}
					if (p.TotalBytes.HasValue && p.TotalBytes.Value > 0)
					{
						DownloadProgress = p.Bytes * 100.0 / p.TotalBytes.Value;
						DownloadStatusText = $"正在下载 {item.Name}… {p.Bytes / 1048576.0:0.0} / {p.TotalBytes.Value / 1048576.0:0.0} MB";
					}
					else
					{
						DownloadStatusText = $"正在下载 {item.Name}… {p.Bytes / 1048576.0:0.0} MB";
					}
				});
			}, CancellationToken.None).ConfigureAwait(true);

			DownloadProgress = 100;
			DownloadStatusText = $"已下载 {item.Name} 到模组文件夹";
			LoadMods();

			// 云同步：把本次下载记录上报云端（下载历史）
			try
			{
				AppState.Current.CloudSync.PushDownloads(new[]
				{
					new { slug = item.Slug, name = item.Name, version = item.Version, author = item.Author, downloadedAt = DateTimeOffset.Now.ToString("o") }
				});
			}
			catch
			{
				// 云同步失败不影响下载结果
			}
		}
		catch (Exception ex)
		{
			DownloadStatusText = "下载失败";
			MessageBox.Show("下载模组失败：" + (ex.InnerException?.Message ?? ex.Message), "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			IsDownloading = false;
		}
	}

	/// <summary>按搜索词过滤在线列表（搜索只作用于已加载条目）。</summary>
	private void ApplyFilter()
	{
		IEnumerable<RepositoryModItem> query = allRepositoryMods;
		if (SelectedCategory != "全部")
		{
			// 拉取时已按官方分类过滤；本机映射里"车辆"覆盖 vehicles/land 两个官方分类
			query = query.Where(m => m.Category == SelectedCategory);
		}
		string? kw = SearchText?.Trim();
		if (!string.IsNullOrEmpty(kw))
		{
			query = query.Where(m =>
				(m.Name?.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
				|| (m.Author?.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
				|| (m.Description?.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0));
		}

		IList<RepositoryModItem> filtered = query.ToList();
		RepositoryMods.Clear();
		foreach (RepositoryModItem m in filtered)
		{
			RepositoryMods.Add(m);
		}
	}

	private void LoadMods()
	{
		Mods.Clear();
		if (!Directory.Exists(ModsDirectory))
		{
			StatusMessage = "未找到模组目录：" + ModsDirectory;
			return;
		}

		var entries = new List<ModItem>();
		foreach (string file in Directory.GetFiles(ModsDirectory, "*.zip").OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
		{
			string name = Path.GetFileNameWithoutExtension(file);
			string size;
			try
			{
				long bytes = new FileInfo(file).Length;
				size = bytes >= 1048576 ? (bytes / 1048576.0).ToString("0.0") + " MB" : (bytes / 1024.0).ToString("0") + " KB";
			}
			catch { size = ""; }
			entries.Add(new ModItem(name, file, size));
		}
		foreach (ModItem m in entries) { Mods.Add(m); }
		StatusMessage = entries.Count == 0
			? "还没有安装模组。点击上方「在线仓库」浏览下载，或在下方打开模组文件夹手动放入 zip。"
			: $"共 {entries.Count} 个模组 · {ModsDirectory}";
	}
}
