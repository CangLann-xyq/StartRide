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

/// <summary>
/// 在线仓库的模组条目。
/// 列表页能直接给的东西（名称/作者/分类/版本/评分/下载量/tagline/**作者上传的图标**）立刻可见；
/// 完整介绍、安装包体积、详情页精确下载量在用户点「详情」时才拉（和下载直链共用同一次请求）。
/// </summary>
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

	/// <summary>列表页的 tagline（一句话简介），不点详情也能看到。</summary>
	public string Summary { get; }

	public string Version { get; }
	public string RatingText { get; }

	/// <summary>列表页给出的下载量（真实值，来自官方仓库列表）。</summary>
	public string ListDownloadsText { get; }

	/// <summary>模组作者上传的图标地址（绝对地址）。</summary>
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

	/// <summary>
	/// 作者上传的图标位图（异步填充；为空时界面显示默认图标）。
	///
	/// ⚠️ 这里是**懒加载**：只有当界面真的绑定到这一项（进了列表可视区/缓冲区）时才去下载。
	/// 旧实现在 AppendItems 里对每一条都 `_ = LoadIconAsync(item)`，全库 8800+ 条就会打出
	/// 8800+ 个图标请求，既拖慢列表又抢走列表页本身的带宽。虚拟化列表只会实例化可视区
	/// 那十几个容器，所以懒加载后图标请求量下降两个数量级。
	/// </summary>
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

	/// <summary>图标没取到时允许下次绑定再试一次（避免一次网络抖动就永久没图标）。</summary>
	internal void AllowIconRetry() => iconRequested = false;

	public bool HasIcon => iconSource != null;

	public bool NoIcon => iconSource == null;

	/// <summary>作者写的完整介绍（点详情后才有）。</summary>
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

	/// <summary>界面上展示的介绍：有完整版用完整版，否则退回 tagline。</summary>
	public string DescriptionText => fullDescription.Length > 0 ? fullDescription : Summary;

	/// <summary>详情页上的精确下载量（列表页的是同一来源的近似快照）。</summary>
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

	/// <summary>本机 mods 目录里是否已经有这个模组。</summary>
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

	/// <summary>正在下载这一条（按钮显示进度占位）。</summary>
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

	/// <summary>把详情页解析结果填进条目。</summary>
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

/// <summary>
/// 模组仓库页：在线数据实时拉取 BeamNG 官方资源库（www.beamng.com/resources）。
///
/// 拉取策略（实测过的时间数字见注释）：
///   ① 启动器起来时就在后台**预热**第 1 页 —— 首次请求含 DNS+TLS，实测要 12.9s，
///      之后每页只要 ~0.7s，把这 12.9s 挪到用户点进「在线仓库」之前；
///   ② 先把第 1 页（100 条）画出来，再后台续拉；
///   ③ 列表页 HTML 有 10 分钟内存缓存（切分类来回点 / 点刷新不再重复下载 283KB/页）；
///   ④ 拉完的结果落盘，下次启动直接秒开（标明是本地缓存，点刷新可更新）。
///
/// 下载：先拉模组自己的详情页（顺带拿到介绍/图标/体积/精确下载量 + 下载直链），
/// 再走客户端分段并行下载（实测单流 424KB/s、4 段 4.0MB/s）。
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

	/// <summary>
	/// 站点 pageNav 给的 data-last（**不可信**，实测全库给 1577 而真实只有 88 页）。
	/// 只用来做粗略夹逼与文案，绝不用它判定"拉完了"。
	/// </summary>
	private int totalPages;

	/// <summary>已经确认拉到全库末尾（某页解析出 0 条）。到位置位后不再发任何续拉请求。</summary>
	private bool reachedRepositoryEnd;

	private bool restoredFromDisk;

	/// <summary>续拉每批页数。共 ~89 页，12 页/批 ≈ 8 批拉完全库。</summary>
	private const int LoadMorePages = 12;

	/// <summary>
	/// 一次往界面集合里加多少条就让出一次 UI 线程。
	/// 一批 1200 条、每条一次 CollectionChanged，一口气加完窗口会几百毫秒~数秒不响应
	/// （用户看到的"程序未响应"）。分批 + Dispatcher.Yield(Background) 之后，
	/// 渲染与输入始终优先，列表是"一条条长出来"的。
	/// </summary>
	private const int UiChunkSize = 120;

	/// <summary>图标下载并发上限（图标很小，但别把仓库列表的带宽抢光）。</summary>
	private static readonly SemaphoreSlim IconGate = new(6);

	/// <summary>在线视图分类选项（与 BeamNgRepositoryClient 的映射对应）。</summary>
	private static readonly string[] Categories = { "全部", "车辆", "地图", "涂装", "场景", "界面应用", "模组扩展", "音效", "其他" };

	public ObservableCollection<ModItem> Mods { get; } = new();
	public ObservableCollection<RepositoryModItem> RepositoryMods { get; } = new();
	public ObservableCollection<string> CategoryOptions { get; } = new();

	public bool HasMods => Mods.Count > 0;

	public bool HasRepositoryMods => RepositoryMods.Count > 0;

	/// <summary>在线列表还没到全库末尾（用来隐藏/禁用「加载更多」按钮）。</summary>
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

	/// <summary>
	/// 在线仓库正在忙（首次拉取 / 续拉）—— 加载动画唯一的绑定源。
	/// 单独立一个属性是为了让 XAML 只盯一个信号：上面两个 bool 变化时都会通知它。
	/// </summary>
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

		// 后台预热：把第 1 页那次 12.9s 的冷启动开销提前花掉
		_ = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(1500, lifetimeCts.Token).ConfigureAwait(false);
				await BeamNgRepositoryClient.WarmUpAsync(null, lifetimeCts.Token).ConfigureAwait(false);
			}
			catch
			{
				// 预热失败无所谓
			}
		});
	}

	public IRelayCommand ShowOnlineCommand =>
		showOnlineCommand ?? (showOnlineCommand = new RelayCommand(ShowOnline));

	public IRelayCommand ShowLocalCommand =>
		showLocalCommand ?? (showLocalCommand = new RelayCommand(ShowLocal));

	/// <summary>「← 返回本机模组」：在线列表占满整页时用它退回去。</summary>
	public IRelayCommand HideOnlineCommand =>
		hideOnlineCommand ?? (hideOnlineCommand = new RelayCommand(ShowLocal));

	public IRelayCommand OpenModsFolderCommand =>
		openModsFolderCommand ?? (openModsFolderCommand = new RelayCommand(OpenModsFolder));

	/// <summary>刷新：清掉页面缓存后重新拉第 1 页，保证拿到的是最新数据。</summary>
	public IRelayCommand RefreshRepositoryCommand =>
		refreshRepositoryCommand ?? (refreshRepositoryCommand = new RelayCommand(() =>
		{
			BeamNgRepositoryClient.InvalidatePageCache();
			StartLoadRepository();
		}));

	/// <summary>滚动到底部触发：续拉下一波页面（内部有防重入守卫）。</summary>
	public IRelayCommand LoadMoreCommand =>
		loadMoreCommand ?? (loadMoreCommand = new RelayCommand(() => _ = TryLoadMoreAsync()));

	/// <summary>点击本机模组行：在资源管理器中定位该 zip。</summary>
	public IRelayCommand<ModItem?> RevealModCommand =>
		revealModCommand ?? (revealModCommand = new RelayCommand<ModItem?>(RevealMod));

	/// <summary>展开/收起某个模组：首次展开时拉它自己的详情页（介绍/体积/精确下载量）。</summary>
	public IRelayCommand<RepositoryModItem?> ToggleDetailCommand =>
		toggleDetailCommand ?? (toggleDetailCommand = new RelayCommand<RepositoryModItem?>(ToggleDetail));

	/// <summary>在浏览器里打开该模组在 BeamNG 官网的页面。</summary>
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
				// 用 HasIcon（读字段）而不是 IconSource（getter 会触发一次懒加载请求）
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
			// ⚠️ HttpClient 的**超时**抛的也是 TaskCanceledException，所以这里不能一吞了之：
			// 否则状态栏会永远停在"正在拉取…"，表现就是"拉不出来"。
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

	/// <summary>
	/// 把网络异常翻译成人话。
	/// 真实例子：`The proxy tunnel request to proxy 'http://127.0.0.1:1786/' failed with status code '502'`
	/// —— 直接甩给用户等于没提示，所以在这里归类成可操作的说明。
	/// </summary>
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

	/// <summary>本机已有模组的文件名集合（用于判断在线条目是否已下载）。</summary>
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

		// ① 有本地缓存先秒开（读盘 + 反序列化都在后台线程，见 TryRestoreFromDiskAsync）
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

			// 首屏渲染后再自动续拉一波，之后由滚动触发。
			// 注意先解除 IsLoadingRepository 守卫，否则续拉会被防重入检查直接拦掉。
			IsLoadingRepository = false;
			if (IsOnlineView && !reachedRepositoryEnd)
			{
				// 首屏只自动拉一波，其余交给滚动/「加载更多」，免得一进页面就朝站点打满
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
			// 文案里绝不出现 totalPages * 100 那种估算 —— 站点给的 data-last 是假的
			//（1577 页 / 157700 条），显示出来只会让用户以为"还剩十几万拉不完"。
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

	/// <summary>续拉下一波页面（滚动到底 / 点「加载更多」触发）。返回是否真的启动了加载。</summary>
	public async Task<bool> TryLoadMoreAsync()
	{
		if (!IsOnlineView || IsLoadingRepository || IsLoadingMore || IsDownloading)
		{
			return false;
		}
		if (reachedRepositoryEnd)
		{
			// 已经拉到全库末尾：不要静默什么都不做（用户会以为按钮坏了），
			// 明确告诉他到底了、想看有没有新增请点「刷新」。
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
			// ⚠️ 本地缓存模式下用户仍要更多时：只回线上锚一次总页数（第 1 页很便宜），
			// 再从缓存覆盖到的下一页续拉——绝不能因为 restoredFromDisk 就静默什么都不做，
			// 否则用户点「加载更多」/滚到底毫无反应，只能靠「刷新」把 89 页整套重拉。
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
					await AppendItemsAsync(head, cts.Token).ConfigureAwait(true); // 按 ResourceId 去重，重复的不会进列表
				}
				restoredFromDisk = false;
			}

			if (nextPage < 1)
			{
				nextPage = 1;
			}

			// ⚠️ 绝不拿 totalPages 当硬门：它来自站点 pageNav 的 data-last，实测全库给 1577
			// 而真实只有 88 页（8834 条）。以它为上限就会出现"拉到第 89 页后一直往空页拉"，
			// 状态栏永远显示"已加载 8834 / 约 157700"——用户看到的就是「剩下的拉不出来」。
			// 真正的终点只有一个信号：某一页解析出 0 条（ListPageBatch.ReachedEnd）。
			int from = nextPage;
			int to = from + LoadMorePages - 1;
			RepositoryStatusText = $"正在加载第 {from}-{to} 页…";
			string? slug = BeamNgRepositoryClient.ChineseToCategorySlug(SelectedCategory);
			BeamNgRepositoryClient.ListPageBatch batch = await BeamNgRepositoryClient
				.FetchPageRangeAsync(slug, from, to, cts.Token)
				.ConfigureAwait(true);
			cts.Token.ThrowIfCancellationRequested();

			await AppendItemsAsync(batch.Items, cts.Token).ConfigureAwait(true);
			SaveCacheToDisk(); // 每批落一次盘（写盘在后台，不占 UI 线程）：下次启动就能秒开

			if (batch.ReachedEnd)
			{
				// 已越过全库末尾：钉住终点，之后一个请求都不再发
				reachedRepositoryEnd = true;
				nextPage = batch.LastNonEmptyPage + 1;
				UpdateRepositoryStatus();
				return true;
			}

			if (batch.LastNonEmptyPage == 0)
			{
				// 整批 12 页全部请求失败（网络波动）：原地重试，
				// 绝不能把 nextPage 推走，否则这一段数据永久缺失、且看起来"再也拉不出来"。
				nextPage = from;
				RepositoryStatusText = $"第 {from}-{to} 页暂时没取到（网络波动，已自动重试）· 再滚一次继续";
				return true;
			}

			// 有失败页时不能越过它；重复拉是幂等的，AppendItems 会按 ResourceId 去重
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

	/// <summary>
	/// 追加条目。⚠️ 这里同时写入 allRepositoryMods（后台全集）**和 RepositoryMods（界面绑定的集合）** ——
	/// 旧实现只写前者，于是状态栏"已加载 1087 个"而列表永远停在 100 条
	/// （用户报的"拉取第一页后其他页拉取不出来"就是这个）。
	/// 用增量 Add 而不是 Clear+重建，避免每次续拉都把滚动位置弹回顶部。
	///
	/// ⚠️ 必须分批 + 让出 UI 线程：一批 1200 条、每条一次 CollectionChanged，
	/// 一口气加完窗口会几百毫秒~数秒不响应（用户看到的"程序未响应"）。
	/// 每 UiChunkSize 条 await 一次 Dispatcher.Yield(Background)：渲染与输入优先。
	///
	/// ⚠️ 图标**不在这里预取**：交给 RepositoryModItem.IconSource 的懒加载
	/// （虚拟化列表只实例化可视区那十几个容器，图标请求量下降两个数量级）。
	/// 旧实现在这里对每一条都 `_ = LoadIconAsync(item)`，把 8800+ 条全打出去，
	/// 既拖慢列表又抢走列表页本身的带宽 —— 用户感觉到的"拉取慢"有一部分就是它。
	/// </summary>
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

	/// <summary>图标懒加载回调：条目首次被界面绑定时调用（滚动时按需触发，限额 6 并发）。</summary>
	private void QueueIconLoad(RepositoryModItem item) => _ = LoadIconAsync(item);

	/// <summary>拉作者上传的图标（本地已有缓存则直接命中）。</summary>
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
			// 图标拿不到就用默认图标，不打扰用户；下次重新绑定时再试一次
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

	// ── 本地磁盘缓存 ────────────────────────────────────────────────────────────
	// 目的：第二次打开启动器点进「在线仓库」应当是"秒开"。
	// 缓存只存列表页能拿到的字段；介绍等详情字段仍然按需现拉。

	private static string CacheRoot => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
		"StartRide",
		"cache");

	private static string RepoCachePath(string? categorySlug) =>
		Path.Combine(CacheRoot, "repo-" + (categorySlug ?? "all") + ".json");

	private sealed class RepoCacheDto
	{
		public int TotalPages { get; set; }

		/// <summary>上次拉取时是否已确认到全库末尾 —— 是的话下次载入直接显示"已加载全部"，不再空跑一批。</summary>
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

	/// <summary>
	/// 从磁盘缓存恢复列表。
	/// ⚠️ 读盘 + 反序列化必须离开 UI 线程：12 小时内的缓存实测 1 MB 量级、1000+ 条，
	/// 在 UI 线程做 `File.ReadAllText` + `JsonSerializer.Deserialize` 就是
	/// "点进在线仓库先卡一下"（和"程序未响应"是同一类问题）。
	/// 这里只把"读 + 解"丢给线程池，真正要碰界面集合的 AppendItemsAsync 仍在 UI 线程跑。
	/// </summary>
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
					// 只认 12 小时内的缓存，太旧就别拿出来误导人
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
			throw;   // 切分类/刷新导致的取消要照常向上抛，别当成"没有缓存"
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// 把当前列表落盘（下次启动秒开）。
	/// ⚠️ 序列化 + 写盘同样必须离开 UI 线程：1000+ 条每批序列化一次是几十毫秒，
	/// 再加写 1 MB 文件，每批都这么干 = 每批卡一下。
	/// 这里只在 UI 线程取"快照"（读属性），序列化与磁盘 IO 丢给线程池；
	/// 用递增序号保证"只有最新快照才落盘"，避免后台写乱序互相覆盖。
	/// </summary>
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
			// 缓存写失败不影响使用
		}
	}

	private static readonly object CacheWriteGate = new();
	private int cacheSnapshotSeq;
	private int cacheWrittenSeq;

	/// <summary>后台写缓存（顺序保护见 SaveCacheToDisk）。</summary>
	private void WriteCacheFile(string path, RepoCacheDto dto, int seq)
	{
		lock (CacheWriteGate)
		{
			if (seq < cacheWrittenSeq)
			{
				return; // 已经有更新的快照落过盘了
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
				// 缓存写失败不影响使用
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

	/// <summary>
	/// 下载模组：先拉它自己的详情页（拿到 download?version=NNN 直链 + 介绍/体积/精确下载量），
	/// 再分段并行下载 zip 到 mods 目录。分段数取设置里的「下载线程数」。
	/// </summary>
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

			// 云同步：把本次下载记录上报云端（下载历史）
			try
			{
				AppState.Current.CloudSync.PushDownloads(new[]
				{
					new { slug = item.Slug, name = item.Name, version = item.VersionText, author = item.Author, downloadedAt = DateTimeOffset.Now.ToString("o") }
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
			item.IsDownloading = false;
			downloadingItemName = null;
			SaveCacheToDisk();
		}
	}

	/// <summary>按搜索词过滤在线列表（搜索作用于已加载条目，不重新拉取）。</summary>
	private void ApplyFilter()
	{
		// ⚠️ 这里也是"卡一下"的来源：全库 1000+ 条时 Clear + 逐条 Add 全在 UI 线程做，
		// 每敲一个字就重建一次列表。改成后台算 + 分批让出 UI 线程（见 ApplyFilterAsync）。
		int version = ++filterVersion;
		_ = ApplyFilterAsync(version);
	}

	private int filterVersion;

	/// <summary>过滤的实际执行体：筛选在后台算，界面集合改动分批回 UI 线程。</summary>
	private async Task ApplyFilterAsync(int version)
	{
		List<RepositoryModItem> filtered = await Task.Run(
			() => allRepositoryMods.Where(PassesFilter).ToList()).ConfigureAwait(true);
		if (version != filterVersion)
		{
			return; // 用户又敲了一个字，这一轮作废
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
