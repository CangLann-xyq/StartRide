using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StartRide.Core;

namespace StartRide.App.ViewModels.Mods;

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

public sealed class RepositoryModItem : ObservableObject
{
	private ImageSource? iconSource;
	private readonly Action<RepositoryModItem>? requestIcon;
	private bool iconRequested;
	private string fullDescription = "";
	private string exactDownloadsText = "";
	private string fileSizeText = "";
	private string detailVersion = "";
	private bool isExpanded;
	private bool isDetailLoading;
	private bool detailLoaded;
	private bool isDownloaded;
	private bool isDownloading;

	public long ResourceId { get; }
	public string Slug { get; }
	public string Name { get; }
	public string Author { get; }
	public string Category { get; }

	public string Summary { get; }

	public string Version { get; }
	public string RatingText { get; }

	public string ListDownloadsText { get; }

	public string IconUrl { get; }

	public string PageUrl { get; }

	public RepositoryModItem(BeamNgModInfo info, Action<RepositoryModItem>? requestIcon = null)
	{
		this.requestIcon = requestIcon;
		ResourceId = info.Id;
		Slug = info.Slug;
		Name = info.Name;
		Author = info.Author;
		Category = info.CategoryName;
		Summary = info.Description;
		Version = info.Version;
		RatingText = info.RatingText;
		ListDownloadsText = info.DownloadsText;
		IconUrl = info.IconUrl;
		PageUrl = info.PageUrl;
	}

	public ImageSource? IconSource
	{
		get
		{
			if (iconSource == null && !iconRequested && IconUrl.Length > 0 && requestIcon != null)
			{
				iconRequested = true;
				requestIcon(this);
			}
			return iconSource;
		}
		set
		{
			if (SetProperty(ref iconSource, value))
			{
				OnPropertyChanged(nameof(HasIcon));
				OnPropertyChanged(nameof(NoIcon));
			}
		}
	}

	internal void AllowIconRetry() => iconRequested = false;

	public bool HasIcon => iconSource != null;

	public bool NoIcon => iconSource == null;

	public string FullDescription
	{
		get => fullDescription;
		internal set
		{
			if (SetProperty(ref fullDescription, value))
			{
				OnPropertyChanged(nameof(HasFullDescription));
				OnPropertyChanged(nameof(DescriptionText));
			}
		}
	}

	public bool HasFullDescription => fullDescription.Length > 0;

	public string DescriptionText => fullDescription.Length > 0 ? fullDescription : Summary;

	public string DownloadsText => exactDownloadsText.Length > 0 ? exactDownloadsText : ListDownloadsText;

	public string FileSizeText => fileSizeText;

	public bool HasFileSize => fileSizeText.Length > 0;

	public string VersionText => detailVersion.Length > 0 ? detailVersion : Version;

	public bool IsExpanded
	{
		get => isExpanded;
		set
		{
			if (SetProperty(ref isExpanded, value))
			{
				OnPropertyChanged(nameof(DetailButtonText));
			}
		}
	}

	public string DetailButtonText => isExpanded ? "收起" : "详情";

	public bool IsDetailLoading
	{
		get => isDetailLoading;
		set => SetProperty(ref isDetailLoading, value);
	}

	public bool DetailLoaded
	{
		get => detailLoaded;
		set => SetProperty(ref detailLoaded, value);
	}

	public bool IsDownloaded
	{
		get => isDownloaded;
		set
		{
			if (SetProperty(ref isDownloaded, value))
			{
				OnPropertyChanged(nameof(DownloadButtonText));
			}
		}
	}

	public string DownloadButtonText => isDownloaded ? "重新下载" : "下载";

	public bool IsDownloading
	{
		get => isDownloading;
		set
		{
			if (SetProperty(ref isDownloading, value))
			{
				OnPropertyChanged(nameof(DownloadButtonText));
			}
		}
	}

	public void ApplyDetail(BeamNgResourceDetail detail)
	{
		if (!string.IsNullOrWhiteSpace(detail.Description))
		{
			FullDescription = detail.Description;
		}
		if (!string.IsNullOrWhiteSpace(detail.DownloadsText))
		{
			exactDownloadsText = detail.DownloadsText.Trim();
			OnPropertyChanged(nameof(DownloadsText));
		}
		if (!string.IsNullOrWhiteSpace(detail.FileSizeText))
		{
			fileSizeText = detail.FileSizeText.Trim();
			OnPropertyChanged(nameof(FileSizeText));
			OnPropertyChanged(nameof(HasFileSize));
		}
		if (!string.IsNullOrWhiteSpace(detail.Version))
		{
			detailVersion = detail.Version.Trim();
			OnPropertyChanged(nameof(VersionText));
		}
		DetailLoaded = true;
	}
}

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
	private string? downloadingItemName;
	private RelayCommand? showOnlineCommand;
	private RelayCommand? showLocalCommand;
	private RelayCommand? openModsFolderCommand;
	private RelayCommand? refreshRepositoryCommand;
	private RelayCommand? loadMoreCommand;
	private RelayCommand? hideOnlineCommand;
	private RelayCommand<ModItem?>? revealModCommand;
	private RelayCommand<RepositoryModItem?>? toggleDetailCommand;
	private RelayCommand<RepositoryModItem?>? openModPageCommand;

	private readonly List<RepositoryModItem> allRepositoryMods = new();
	private CancellationTokenSource? loadCts;
	private readonly CancellationTokenSource lifetimeCts = new();
	private int nextPage = 1;

	private int totalPages;

	private bool reachedRepositoryEnd;

	private bool restoredFromDisk;

	private const int LoadMorePages = 12;

	private const int UiChunkSize = 120;

	private static readonly SemaphoreSlim IconGate = new(6);

	private static readonly string[] Categories = { "全部", "车辆", "地图", "涂装", "场景", "界面应用", "模组扩展", "音效", "其他" };

	public ObservableCollection<ModItem> Mods { get; } = new();
	public ObservableCollection<RepositoryModItem> RepositoryMods { get; } = new();
	public ObservableCollection<string> CategoryOptions { get; } = new();

	public bool HasMods => Mods.Count > 0;

	public bool HasRepositoryMods => RepositoryMods.Count > 0;

	public bool HasMoreRepositoryPages => !reachedRepositoryEnd && RepositoryMods.Count > 0;

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
		private set
		{
			if (SetProperty(ref isLoadingRepository, value))
			{
				OnPropertyChanged(nameof(IsRepositoryBusy));
			}
		}
	}

	public bool IsLoadingMore
	{
		get => isLoadingMore;
		private set
		{
			if (SetProperty(ref isLoadingMore, value))
			{
				OnPropertyChanged(nameof(IsRepositoryBusy));
			}
		}
	}

	public bool IsRepositoryBusy => IsLoadingRepository || IsLoadingMore;

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

		_ = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(1500, lifetimeCts.Token).ConfigureAwait(false);
				await BeamNgRepositoryClient.WarmUpAsync(null, lifetimeCts.Token).ConfigureAwait(false);
			}
			catch
			{
			}
		});
	}

	public IRelayCommand ShowOnlineCommand =>
		showOnlineCommand ?? (showOnlineCommand = new RelayCommand(ShowOnline));

	public IRelayCommand ShowLocalCommand =>
		showLocalCommand ?? (showLocalCommand = new RelayCommand(ShowLocal));

	public IRelayCommand HideOnlineCommand =>
		hideOnlineCommand ?? (hideOnlineCommand = new RelayCommand(ShowLocal));

	public IRelayCommand OpenModsFolderCommand =>
		openModsFolderCommand ?? (openModsFolderCommand = new RelayCommand(OpenModsFolder));

	public IRelayCommand RefreshRepositoryCommand =>
		refreshRepositoryCommand ?? (refreshRepositoryCommand = new RelayCommand(() =>
		{
			BeamNgRepositoryClient.InvalidatePageCache();
			StartLoadRepository();
		}));

	public IRelayCommand LoadMoreCommand =>
		loadMoreCommand ?? (loadMoreCommand = new RelayCommand(() => _ = TryLoadMoreAsync()));

	public IRelayCommand<ModItem?> RevealModCommand =>
		revealModCommand ?? (revealModCommand = new RelayCommand<ModItem?>(RevealMod));

	public IRelayCommand<RepositoryModItem?> ToggleDetailCommand =>
		toggleDetailCommand ?? (toggleDetailCommand = new RelayCommand<RepositoryModItem?>(ToggleDetail));

	public IRelayCommand<RepositoryModItem?> OpenModPageCommand =>
		openModPageCommand ?? (openModPageCommand = new RelayCommand<RepositoryModItem?>(OpenModPage));

	private void ShowOnline()
	{
		IsOnlineView = true;
	}

	private void ShowLocal()
	{
		IsOnlineView = false;
	}

	private void OpenModPage(RepositoryModItem? item)
	{
		if (item == null)
		{
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo(item.PageUrl) { UseShellExecute = true });
		}
		catch (Exception ex)
		{
			MessageBox.Show("无法打开模组页面：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void ToggleDetail(RepositoryModItem? item)
	{
		if (item == null)
		{
			return;
		}
		item.IsExpanded = !item.IsExpanded;
		if (item.IsExpanded && !item.DetailLoaded && !item.IsDetailLoading)
		{
			_ = LoadDetailAsync(item);
		}
	}

	private async Task LoadDetailAsync(RepositoryModItem item)
	{
		item.IsDetailLoading = true;
		try
		{
			CancellationToken ct = lifetimeCts.Token;
			BeamNgResourceDetail? detail = await BeamNgRepositoryClient
				.FetchResourceDetailAsync(item.PageUrl, ct)
				.ConfigureAwait(true);
			if (detail != null)
			{
				item.ApplyDetail(detail);
				if (!string.IsNullOrEmpty(detail.IconUrl) && !item.HasIcon)
				{
					ImageSource? icon = await ModIconCache.GetAsync(item.ResourceId, detail.IconUrl, ct).ConfigureAwait(true);
					if (icon != null)
					{
						item.IconSource = icon;
					}
				}
			}
			else
			{
				item.DetailLoaded = true;
				item.FullDescription = "（读取模组介绍失败，可稍后重试或点「官网页面」查看）";
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			item.DetailLoaded = true;
			item.FullDescription = "（读取模组介绍失败：" + ex.Message + "）";
		}
		finally
		{
			item.IsDetailLoading = false;
		}
	}

	private async void StartLoadRepository()
	{
		if (IsLoadingRepository)
		{
			return;
		}
		try
		{
			loadCts?.Cancel();
			loadCts?.Dispose();
			loadCts = new CancellationTokenSource();
			await LoadRepositoryAsync(loadCts.Token).ConfigureAwait(true);
		}
		catch (OperationCanceledException)
		{

			IsLoadingRepository = false;
			if (RepositoryStatusText.StartsWith("正在拉取", StringComparison.Ordinal))
			{
				RepositoryStatusText = "拉取官方仓库超时（网络不稳或被限流）· 点「刷新」重试";
			}
		}
		catch (Exception ex)
		{
			IsLoadingRepository = false;
			RepositoryStatusText = "无法连接 BeamNG 官方仓库：" + FriendlyNetworkError(ex) + " · 点「刷新」重试";
		}
	}

	private static string FriendlyNetworkError(Exception ex)
	{
		string msg = ex.InnerException?.Message ?? ex.Message;
		if (msg.Contains("502", StringComparison.Ordinal) || msg.Contains("proxy", StringComparison.OrdinalIgnoreCase))
		{
			return "代理返回 502（代理节点已失效，或代理规则把 beamng.com 判成了直连）";
		}
		if (ex is OperationCanceledException
			|| msg.Contains("timed out", StringComparison.OrdinalIgnoreCase)
			|| msg.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
			|| msg.Contains("10060"))
		{
			return "连接超时（BeamNG 官网在国内直连很不稳定，建议开启代理后重试）";
		}
		return msg;
	}

	private HashSet<string> LocalModFileNames()
	{
		var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			if (Directory.Exists(ModsDirectory))
			{
				foreach (string f in Directory.GetFiles(ModsDirectory, "*.zip"))
				{
					set.Add(Path.GetFileName(f));
					set.Add(Path.GetFileNameWithoutExtension(f));
				}
			}
		}
		catch
		{
		}
		return set;
	}

	private async Task LoadRepositoryAsync(CancellationToken ct)
	{
		IsLoadingRepository = true;
		IsLoadingMore = false;
		nextPage = 1;
		totalPages = 0;
		reachedRepositoryEnd = false;
		allRepositoryMods.Clear();
		RepositoryMods.Clear();
		OnPropertyChanged(nameof(HasRepositoryMods));
		restoredFromDisk = false;

		if (await TryRestoreFromDiskAsync(ct).ConfigureAwait(true))
		{
			restoredFromDisk = true;
			IsLoadingRepository = false;
			UpdateRepositoryStatus();
			return;
		}

		RepositoryStatusText = "正在拉取 BeamNG 官方仓库（第 1 页）…";
		try
		{
			string? slug = BeamNgRepositoryClient.ChineseToCategorySlug(SelectedCategory);
			var (firstPage, total) = await BeamNgRepositoryClient.FetchFirstPageAsync(slug, ct).ConfigureAwait(true);
			ct.ThrowIfCancellationRequested();
			totalPages = Math.Max(total, 1);
			await AppendItemsAsync(firstPage, ct).ConfigureAwait(true);
			nextPage = 2;
			UpdateRepositoryStatus();

			IsLoadingRepository = false;
			if (IsOnlineView && !reachedRepositoryEnd)
			{
				_ = TryLoadMoreAsync();
			}
		}
		finally
		{
			IsLoadingRepository = false;
		}
	}

	private void UpdateRepositoryStatus()
	{
		if (reachedRepositoryEnd)
		{

			RepositoryStatusText = $"已加载全部 {FormatCount(allRepositoryMods.Count)} 个模组（已到官方仓库末尾）";
			OnPropertyChanged(nameof(HasMoreRepositoryPages));
			return;
		}
		if (restoredFromDisk)
		{
			RepositoryStatusText = $"已从本地缓存载入 {RepositoryMods.Count} 个模组（滚到底或点「加载更多」继续拉，点「刷新」取最新）";
			return;
		}
		RepositoryStatusText =
			$"已加载 {FormatCount(allRepositoryMods.Count)} 个 · 滚到底或点「加载更多」继续（每批 {LoadMorePages * 100} 个）";
		OnPropertyChanged(nameof(HasMoreRepositoryPages));
	}

	public async Task<bool> TryLoadMoreAsync()
	{
		if (!IsOnlineView || IsLoadingRepository || IsLoadingMore || IsDownloading)
		{
			return false;
		}
		if (reachedRepositoryEnd)
		{

			RepositoryStatusText = $"已加载全部 {FormatCount(allRepositoryMods.Count)} 个模组 · 点「刷新」检查官方仓库有没有新增";
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

			if (restoredFromDisk)
			{
				RepositoryStatusText = "正在回到线上继续加载…";
				string? reSlug = BeamNgRepositoryClient.ChineseToCategorySlug(SelectedCategory);
				var (head, total) = await BeamNgRepositoryClient
					.FetchFirstPageAsync(reSlug, cts.Token)
					.ConfigureAwait(true);
				cts.Token.ThrowIfCancellationRequested();
				totalPages = Math.Max(total, 1);
				nextPage = Math.Max(2, allRepositoryMods.Count / 100 + 1);
				if (head.Count > 0)
				{
					await AppendItemsAsync(head, cts.Token).ConfigureAwait(true);
				}
				restoredFromDisk = false;
			}

			if (nextPage < 1)
			{
				nextPage = 1;
			}

			int from = nextPage;
			int to = from + LoadMorePages - 1;
			RepositoryStatusText = $"正在加载第 {from}-{to} 页…";
			string? slug = BeamNgRepositoryClient.ChineseToCategorySlug(SelectedCategory);
			BeamNgRepositoryClient.ListPageBatch batch = await BeamNgRepositoryClient
				.FetchPageRangeAsync(slug, from, to, cts.Token)
				.ConfigureAwait(true);
			cts.Token.ThrowIfCancellationRequested();

			await AppendItemsAsync(batch.Items, cts.Token).ConfigureAwait(true);
			SaveCacheToDisk();

			if (batch.ReachedEnd)
			{
				reachedRepositoryEnd = true;
				nextPage = batch.LastNonEmptyPage + 1;
				UpdateRepositoryStatus();
				return true;
			}

			if (batch.LastNonEmptyPage == 0)
			{

				nextPage = from;
				RepositoryStatusText = $"第 {from}-{to} 页暂时没取到（网络波动，已自动重试）· 再滚一次继续";
				return true;
			}

			int advance = batch.LastNonEmptyPage + 1;
			if (batch.FailedPages.Count > 0)
			{
				advance = Math.Min(advance, batch.FailedPages[0]);
			}
			nextPage = Math.Max(from, advance);
			UpdateRepositoryStatus();
			return true;
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		catch (Exception ex)
		{
			RepositoryStatusText = "继续加载失败：" + FriendlyNetworkError(ex) + " · 滚到底或点「加载更多」可重试";
			return false;
		}
		finally
		{
			IsLoadingMore = false;
		}
	}

	private async Task AppendItemsAsync(IEnumerable<BeamNgModInfo> items, CancellationToken ct)
	{
		var existing = new HashSet<long>(allRepositoryMods.Select(m => m.ResourceId));
		HashSet<string> local = LocalModFileNames();
		int added = 0;
		int sinceYield = 0;
		foreach (BeamNgModInfo info in items)
		{
			ct.ThrowIfCancellationRequested();
			if (!existing.Add(info.Id))
			{
				continue;
			}
			var item = new RepositoryModItem(info, QueueIconLoad);
			item.IsDownloaded = local.Contains(item.Slug + ".zip") || local.Contains(item.Slug);
			allRepositoryMods.Add(item);

			if (PassesFilter(item))
			{
				RepositoryMods.Add(item);
				added++;
			}
			if (++sinceYield >= UiChunkSize)
			{
				sinceYield = 0;
				await System.Windows.Threading.Dispatcher
						.Yield(System.Windows.Threading.DispatcherPriority.Background);
			}
		}
		if (added > 0)
		{
			OnPropertyChanged(nameof(HasRepositoryMods));
		}
	}

	private void QueueIconLoad(RepositoryModItem item) => _ = LoadIconAsync(item);

	private async Task LoadIconAsync(RepositoryModItem item)
	{
		if (string.IsNullOrWhiteSpace(item.IconUrl) || item.HasIcon)
		{
			return;
		}
		await IconGate.WaitAsync(lifetimeCts.Token).ConfigureAwait(false);
		try
		{
			ImageSource? icon = await ModIconCache.GetAsync(item.ResourceId, item.IconUrl, lifetimeCts.Token).ConfigureAwait(false);
			if (icon == null)
			{
				item.AllowIconRetry();
				return;
			}
			System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() => item.IconSource = icon);
		}
		catch
		{
			item.AllowIconRetry();
		}
		finally
		{
			IconGate.Release();
		}
	}

	private bool PassesFilter(RepositoryModItem m)
	{
		if (SelectedCategory != "全部" && m.Category != SelectedCategory)
		{
			return false;
		}
		string? kw = SearchText?.Trim();
		if (!string.IsNullOrEmpty(kw))
		{
			bool hit = (m.Name?.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
				|| (m.Author?.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
				|| (m.Summary?.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
				|| (m.FullDescription?.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
			if (!hit)
			{
				return false;
			}
		}
		return true;
	}

	private static string FormatCount(long n)
	{
		return n.ToString("##,###");
	}

	private static string CacheRoot => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
		"StartRide",
		"cache");

	private static string RepoCachePath(string? categorySlug) =>
		Path.Combine(CacheRoot, "repo-" + (categorySlug ?? "all") + ".json");

	private sealed class RepoCacheDto
	{
		public int TotalPages { get; set; }

		public bool ReachedEnd { get; set; }

		public List<RepoCacheRow> Items { get; set; } = new();
	}

	private sealed class RepoCacheRow
	{
		public long Id { get; set; }
		public string Slug { get; set; } = "";
		public string Name { get; set; } = "";
		public string Author { get; set; } = "";
		public string Category { get; set; } = "";
		public string Summary { get; set; } = "";
		public string Version { get; set; } = "";
		public string Rating { get; set; } = "";
		public string Downloads { get; set; } = "";
		public string Icon { get; set; } = "";
	}

	private async Task<bool> TryRestoreFromDiskAsync(CancellationToken ct)
	{
		try
		{
			string path = RepoCachePath(BeamNgRepositoryClient.ChineseToCategorySlug(SelectedCategory));
			RepoCacheDto? dto = await Task.Run(() =>
			{
				try
				{
					if (!File.Exists(path))
					{
						return null;
					}
					if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > TimeSpan.FromHours(12))
					{
						return null;
					}
					RepoCacheDto? d = JsonSerializer.Deserialize<RepoCacheDto>(File.ReadAllText(path));
					return d?.Items == null || d.Items.Count == 0 ? null : d;
				}
				catch
				{
					return null;
				}
			}, ct).ConfigureAwait(true);
			if (dto?.Items == null || dto.Items.Count == 0)
			{
				return false;
			}
			totalPages = Math.Max(dto.TotalPages, 1);
			nextPage = dto.Items.Count / 100 + 1;
			reachedRepositoryEnd = dto.ReachedEnd;
			await AppendItemsAsync(dto.Items.Select(r => new BeamNgModInfo
			{
				Id = r.Id,
				Slug = r.Slug,
				Name = r.Name,
				Author = r.Author,
				CategoryName = r.Category,
				Description = r.Summary,
				Version = r.Version,
				RatingText = r.Rating,
				DownloadsText = r.Downloads,
				IconUrl = r.Icon,
			}), ct).ConfigureAwait(true);
			UpdateRepositoryStatus();
			return RepositoryMods.Count > 0;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			return false;
		}
	}

	private void SaveCacheToDisk()
	{
		try
		{
			if (allRepositoryMods.Count == 0)
			{
				return;
			}
			var dto = new RepoCacheDto
			{
				TotalPages = totalPages,
				ReachedEnd = reachedRepositoryEnd,
				Items = allRepositoryMods.Select(m => new RepoCacheRow
				{
					Id = m.ResourceId,
					Slug = m.Slug,
					Name = m.Name,
					Author = m.Author,
					Category = m.Category,
					Summary = m.Summary,
					Version = m.Version,
					Rating = m.RatingText,
					Downloads = m.ListDownloadsText,
					Icon = m.IconUrl,
				}).ToList(),
			};
			string path = RepoCachePath(BeamNgRepositoryClient.ChineseToCategorySlug(SelectedCategory));
			int seq = ++cacheSnapshotSeq;
			_ = Task.Run(() => WriteCacheFile(path, dto, seq));
		}
		catch
		{
		}
	}

	private static readonly object CacheWriteGate = new();
	private int cacheSnapshotSeq;
	private int cacheWrittenSeq;

	private void WriteCacheFile(string path, RepoCacheDto dto, int seq)
	{
		lock (CacheWriteGate)
		{
			if (seq < cacheWrittenSeq)
			{
				return;
			}
			try
			{
				Directory.CreateDirectory(CacheRoot);
				string tmp = path + ".part";
				File.WriteAllText(tmp, JsonSerializer.Serialize(dto));
				File.Move(tmp, path, overwrite: true);
				cacheWrittenSeq = seq;
			}
			catch
			{
			}
		}
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

	public async Task DownloadModAsync(RepositoryModItem? item)
	{
		if (item == null || IsDownloading)
		{
			return;
		}
		IsDownloading = true;
		item.IsDownloading = true;
		downloadingItemName = item.Name;
		DownloadProgress = 0;
		DownloadStatusText = $"正在获取 {item.Name} 的下载链接…";
		try
		{
			BeamNgResourceDetail? detail = await BeamNgRepositoryClient
				.FetchResourceDetailAsync(item.PageUrl, CancellationToken.None)
				.ConfigureAwait(true);
			if (detail != null)
			{
				item.ApplyDetail(detail);
			}
			string? downloadUrl = detail?.DownloadUrl;

			if (string.IsNullOrEmpty(downloadUrl))
			{
				DownloadStatusText = "未找到下载链接";
				MessageBox.Show(
					"该模组页面没有可用的下载链接（部分模组需要登录 BeamNG 官网才能下载）。",
					"StartRide", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (!Directory.Exists(ModsDirectory))
			{
				Directory.CreateDirectory(ModsDirectory);
			}
			string fileName = item.Slug + ".zip";
			string target = Path.Combine(ModsDirectory, fileName);

			DownloadStatusText = detail != null && !string.IsNullOrEmpty(detail.FileSizeText)
				? $"正在下载 {item.Name}（{detail.FileSizeText}）…"
				: $"正在下载 {item.Name}…";

			int threads = Math.Clamp(AppSettings.Current.DownloadThreads, 1, 16);

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
						DownloadStatusText = $"正在下载 {downloadingItemName}… {p.Bytes / 1048576.0:0.0} / {p.TotalBytes.Value / 1048576.0:0.0} MB（{threads} 线程）";
					}
					else
					{
						DownloadStatusText = $"正在下载 {downloadingItemName}… {p.Bytes / 1048576.0:0.0} MB（{threads} 线程）";
					}
				});
			}, CancellationToken.None, threads).ConfigureAwait(true);

			DownloadProgress = 100;
			DownloadStatusText = $"已下载 {item.Name} 到模组文件夹：{target}";
			item.IsDownloaded = true;
			LoadMods();

			try
			{
				AppState.Current.CloudSync.PushDownloads(new[]
				{
					new { slug = item.Slug, name = item.Name, version = item.VersionText, author = item.Author, downloadedAt = DateTimeOffset.Now.ToString("o") }
				});
			}
			catch
			{
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
			item.IsDownloading = false;
			downloadingItemName = null;
			SaveCacheToDisk();
		}
	}

	private void ApplyFilter()
	{

		int version = ++filterVersion;
		_ = ApplyFilterAsync(version);
	}

	private int filterVersion;

	private async Task ApplyFilterAsync(int version)
	{
		List<RepositoryModItem> filtered = await Task.Run(
			() => allRepositoryMods.Where(PassesFilter).ToList()).ConfigureAwait(true);
		if (version != filterVersion)
		{
			return;
		}
		RepositoryMods.Clear();
		int sinceYield = 0;
		foreach (RepositoryModItem m in filtered)
		{
			if (version != filterVersion)
			{
				return;
			}
			RepositoryMods.Add(m);
			if (++sinceYield >= UiChunkSize)
			{
				sinceYield = 0;
				await System.Windows.Threading.Dispatcher
						.Yield(System.Windows.Threading.DispatcherPriority.Background);
			}
		}
		OnPropertyChanged(nameof(HasRepositoryMods));
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
		OnPropertyChanged(nameof(HasMods));
	}
}
