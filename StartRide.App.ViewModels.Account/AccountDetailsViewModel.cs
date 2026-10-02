using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Accounts;
using StartRide.App.Resources;
using StartRide.Core;

namespace StartRide.App.ViewModels.Account;

public sealed class AccountDetailsViewModel : ObservableObject
{
	private readonly AccountPageViewModel parent;

	/// <summary>本机还没有游玩记录时，时长一栏显示的占位符。</summary>
	private const string MachineStatsPlaceholder = "—";

	public AccountListViewModel AccountList => parent.AccountList;

	public AccountAppearanceViewModel Appearance => parent.Appearance;

	public AccountOfflineUuidViewModel OfflineUuid => parent.OfflineUuid;

	public LauncherAccount? SelectedAccount => parent.SelectedAccount;

	public bool CanRenameSelectedAccount
	{
		get
		{
			LauncherAccount selectedAccount = SelectedAccount;
			if (selectedAccount != null)
			{
				return !selectedAccount.IsThirdParty;
			}
			return false;
		}
	}

	public ICommand RequestRenameAccountCommand => parent.RequestRenameAccountCommand;

	/// <summary>当前选中的账户项（删除账户命令需要 AccountItemViewModel 而不是模型）。</summary>
	public AccountItemViewModel? SelectedAccountItem => AccountList.SelectedItem;

	public IRelayCommand<AccountItemViewModel> RequestDeleteAccountCommand => parent.RequestDeleteAccountCommand;

	/// <summary>本机是否已经有可展示的记录（从 StartRide 启动过游戏）。</summary>
	public bool HasMachineStats => AppSettings.Current.LaunchCount > 0;

	/// <summary>统计条右侧说明：时长取自 Steam 时明说来源，否则回到本机口径。</summary>
	public string MachineStatsBandText
	{
		get
		{
			if (HasSteamData)
			{
				return Strings.Account_StatsBandSteamText;
			}
			return HasMachineStats ? Strings.Account_StatsBandText : Strings.Account_StatsBandEmptyText;
		}
	}

	/// <summary>
	/// 游玩时长：以 Steam 记录的为准。
	/// Steam 记的是**全部**会话（从启动器启动的、直接在 Steam 启动的都算），是完整的那份账；
	/// 启动器自己的 TotalPlaytimeSeconds 只是本机计数。
	/// 没绑定 Steam / 还没同步过时，退回本机计数并显示占位符。
	/// </summary>
	public string PlaytimeText
	{
		get
		{
			if (HasSteamData)
			{
				return PlaytimeTracker.FormatDuration(
					TimeSpan.FromMinutes(Math.Max(0, AppSettings.Current.SteamPlaytimeMinutes)));
			}
			return HasMachineStats
				? PlaytimeTracker.FormatDuration(PlaytimeTracker.GetTotalPlaytime(AppSettings.Current))
				: MachineStatsPlaceholder;
		}
	}

	// ===================== Steam 同步 =====================
	//
	// 时长的权威账本在 Steam 那边。两个方向：
	//   Steam → 启动器：本页把 Steam 记录的时长/成就读回来（经服务器中转，客户端直连不通）。
	//   启动器 → Steam：GameLauncher 启动游戏前会确保 Steam 在运行，
	//                   这样从启动器启动的每一局都会被 Steam 记进时长。

	private bool steamSyncing;
	private string steamSyncMessage = "";
	private DateTimeOffset lastSteamSyncAt = DateTimeOffset.MinValue;

	/// <summary>本机云端登录账户绑定的 SteamID64；空字符串=没有绑定。</summary>
	private static string CloudSteamId => AppState.Current.CloudSync.Auth?.SteamId ?? "";

	/// <summary>
	/// 选中的本地账户是否就是云端已登录的那个 Steam 账户。
	/// 只有这种组合才该显示 Steam 数据 —— 否则会给本地离线账户看别人的时长。
	/// </summary>
	public bool HasSteamSync
	{
		get
		{
			string id = CloudSteamId;
			if (string.IsNullOrWhiteSpace(id))
			{
				return false;
			}
			LauncherAccount? account = SelectedAccount;
			if (account == null)
			{
				return false;
			}
			return (account.Id ?? "").IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}

	/// <summary>未绑定 Steam —— 给 XAML 用（项目里没有取反的可见性转换器）。</summary>
	public bool HasNoSteamSync => !HasSteamSync;

	/// <summary>本地已经存过一份 Steam 数据。</summary>
	public bool HasSteamData => HasSteamSync && !string.IsNullOrWhiteSpace(AppSettings.Current.SteamSyncedAt);

	public bool HasSteamSyncMessage => steamSyncMessage.Length > 0;

	public string SteamSyncMessageText => steamSyncMessage;

	public string SteamSyncedAtText
	{
		get
		{
			if (!HasSteamData || !TryParseSteamSyncedAt(out DateTimeOffset at))
			{
				return Strings.Account_SteamNoData;
			}
			return string.Format(Strings.Account_SteamSyncedAtFormat,
				at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture));
		}
	}

	public string SteamPlaytimeText => HasSteamData
		? PlaytimeTracker.FormatDuration(TimeSpan.FromMinutes(Math.Max(0, AppSettings.Current.SteamPlaytimeMinutes)))
		: MachineStatsPlaceholder;

	public string SteamRecentText => HasSteamData
		? PlaytimeTracker.FormatDuration(TimeSpan.FromMinutes(Math.Max(0, AppSettings.Current.SteamPlaytime2WeeksMinutes)))
		: MachineStatsPlaceholder;

	public string SteamLastPlayedText
	{
		get
		{
			long unix = AppSettings.Current.SteamLastPlayedUnix;
			if (!HasSteamData || unix <= 0)
			{
				return Strings.Account_NoneValue;
			}
			try
			{
				return DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime()
					.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
			}
			catch
			{
				return Strings.Account_NoneValue;
			}
		}
	}

	public string SteamDistanceText
	{
		get
		{
			long meters = AppSettings.Current.SteamMetersDriven;
			if (!HasSteamData || meters <= 0)
			{
				return MachineStatsPlaceholder;
			}
			return string.Format(Strings.Account_SteamDistanceValueFormat, meters / 1000);
		}
	}

	public string SteamAchievementProgressText => AppSettings.Current.SteamAchievementsTotal > 0
		? string.Format(Strings.Account_SteamAchievementProgressFormat,
			AppSettings.Current.SteamAchievementsUnlocked, AppSettings.Current.SteamAchievementsTotal)
		: MachineStatsPlaceholder;

	// ---- 成就明细（默认收起，展开后列出全部项）----
	private List<SteamAchievementRowViewModel> achievementRows = new List<SteamAchievementRowViewModel>();
	private bool achievementsExpanded;

	private RelayCommand? toggleAchievementsCommand;

	/// <summary>
	/// 启动游戏前是否自动拉起 Steam。
	/// 这是「启动器时长 → Steam 时长」那条方向的唯一开关：Steam 只记它自己在跑时起的游戏。
	/// </summary>
	public bool EnsureSteamOnLaunch
	{
		get
		{
			return AppSettings.Current.EnsureSteamBeforeLaunch;
		}
		set
		{
			if (AppSettings.Current.EnsureSteamBeforeLaunch != value)
			{
				AppSettings.Current.EnsureSteamBeforeLaunch = value;
				AppSettings.Current.Save();
				OnPropertyChanged("EnsureSteamOnLaunch");
			}
		}
	}

	public RelayCommand ToggleAchievementsCommand =>
		toggleAchievementsCommand ??= new RelayCommand(ToggleAchievements);

	/// <summary>有没有拿得到成就列表（拿不到就不显示那个开关按钮）。</summary>
	public bool HasAchievementList => achievementRows.Count > 0;

	/// <summary>内存里没有就先读上次落盘的快照，避免冷启动时那一屏是空的。</summary>
	private void LoadCachedAchievementRows()
	{
		if (achievementRows.Count > 0)
		{
			return;
		}
		string json = AppSettings.Current.SteamAchievementsJson;
		if (string.IsNullOrWhiteSpace(json))
		{
			return;
		}
		try
		{
			List<SteamAchievementItem>? items =
				JsonSerializer.Deserialize<List<SteamAchievementItem>>(json, CachedJsonOptions);
			achievementRows = BuildAchievementRows(items);
		}
		catch
		{
			achievementRows = new List<SteamAchievementRowViewModel>();
		}
	}

	private static readonly JsonSerializerOptions CachedJsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	public bool AchievementsExpanded => achievementsExpanded;

	public string AchievementsToggleText => achievementsExpanded
		? Strings.Account_SteamAchievementsCollapse
		: string.Format(Strings.Account_SteamAchievementsExpandFormat, achievementRows.Count);

	/// <summary>成就明细行；展开时才渲染。</summary>
	public IReadOnlyList<SteamAchievementRowViewModel> Achievements => achievementRows;

	private void ToggleAchievements()
	{
		achievementsExpanded = !achievementsExpanded;
		OnPropertyChanged("AchievementsExpanded");
		OnPropertyChanged("AchievementsToggleText");
	}

	/// <summary>进度条用（0~100）。</summary>
	public double SteamAchievementPercent
	{
		get
		{
			int total = AppSettings.Current.SteamAchievementsTotal;
			if (!HasSteamData || total <= 0)
			{
				return 0;
			}
			double pct = AppSettings.Current.SteamAchievementsUnlocked * 100.0 / total;
			return pct < 0 ? 0 : (pct > 100 ? 100 : pct);
		}
	}

	public string SteamAchievementPercentText => AppSettings.Current.SteamAchievementsTotal > 0
		? string.Format(Strings.Account_SteamAchievementPercentFormat,
			Math.Round(SteamAchievementPercent, 1).ToString("0.#", CultureInfo.CurrentCulture))
		: "";

	private IAsyncRelayCommand? syncSteamCommand;

	public IAsyncRelayCommand SyncSteamCommand =>
		syncSteamCommand ??= new AsyncRelayCommand(SyncSteamAsync);

	/// <summary>进页面时自动同步一次；60 秒内不重复打服务器。</summary>
	public void SyncSteamIfStale()
	{
		if (!HasSteamSync)
		{
			return;
		}
		if (DateTimeOffset.Now - lastSteamSyncAt < TimeSpan.FromSeconds(60))
		{
			return;
		}
		lastSteamSyncAt = DateTimeOffset.Now;
		_ = SyncSteamAsync();
	}

	/// <summary>把 Steam 的时长与成就读回本地缓存（写进 settings.json，下次进页面直接渲染）。</summary>
	public async Task SyncSteamAsync()
	{
		if (!HasSteamSync || steamSyncing)
		{
			return;
		}
		steamSyncing = true;
		steamSyncMessage = "";
		RaiseSteamProperties();
		try
		{
			ApiService api = AppState.Current.Api;
			AppSettings settings = AppSettings.Current;

			SteamProfileInfo? profile = await api.GetSteamProfileAsync();
			if (profile != null && profile.Bound && profile.Fresh != false)
			{
				settings.SteamPlaytimeMinutes = profile.PlaytimeMinutes;
				settings.SteamPlaytime2WeeksMinutes = profile.Playtime2WeeksMinutes;
				settings.SteamLastPlayedUnix = profile.LastPlayedUnix;
				settings.SteamAchievementsUnlocked = profile.AchievementsUnlocked;
				settings.SteamAchievementsTotal = profile.AchievementsTotal;
				settings.SteamSyncedAt = NowStamp();
				settings.Save();
			}
			else if (profile == null || profile.Fresh == false)
			{
				// 服务器这次没从 Steam 取到（链路抖动很常见）——如实说，别假装同步成功
				steamSyncMessage = string.Format(Strings.Account_SteamSyncFailedFormat, Strings.Account_SteamNoData);
			}

			SteamAchievementResult? achievements = await api.GetSteamAchievementsAsync(settings.Language);
			if (achievements != null && achievements.Available)
			{
				achievementRows = BuildAchievementRows(achievements.List);
				// 落盘一份，冷启动/链路抖动时也还有得看
				try
				{
					settings.SteamAchievementsJson = JsonSerializer.Serialize(achievements.List);
				}
				catch
				{
				}
				settings.SteamAchievementsUnlocked = achievements.Unlocked;
				settings.SteamAchievementsTotal = achievements.Total;
				if (achievements.Stats != null)
				{
					settings.SteamMetersDriven = achievements.Stats.MetersDriven;
				}
				settings.SteamSyncedAt = NowStamp();
				settings.Save();
			}
			else if (achievements != null && achievements.Bound && !string.IsNullOrWhiteSpace(achievements.Reason))
			{
				steamSyncMessage = string.Format(Strings.Account_SteamSyncFailedFormat, achievements.Reason);
			}
		}
		catch (Exception ex)
		{
			steamSyncMessage = string.Format(Strings.Account_SteamSyncFailedFormat, ex.Message);
		}
		finally
		{
			steamSyncing = false;
			RaiseSteamProperties();
			AppState.Current.Log("Steam 同步：" + (steamSyncMessage.Length > 0 ? steamSyncMessage : "完成"));
		}
	}

	/// <summary>
	/// 排成「已解锁在前（最近解锁优先）→ 未解锁（全球解锁率高的在前）」，
	/// 这样一眼先看到自己拿到了什么，再看到离下一项最近的几项。
	/// </summary>
	private static List<SteamAchievementRowViewModel> BuildAchievementRows(List<SteamAchievementItem>? items)
	{
		if (items == null || items.Count == 0)
		{
			return new List<SteamAchievementRowViewModel>();
		}
		return items
			.OrderByDescending(delegate (SteamAchievementItem item) { return item.Achieved; })
			.ThenByDescending(delegate (SteamAchievementItem item)
			{
				return item.Achieved ? item.UnlockedAt : (long)0;
			})
			.ThenByDescending(delegate (SteamAchievementItem item) { return item.Percent; })
			.Select(delegate (SteamAchievementItem item) { return new SteamAchievementRowViewModel(item); })
			.ToList();
	}

	private static string NowStamp() =>
		DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

	private static bool TryParseSteamSyncedAt(out DateTimeOffset value) =>
		DateTimeOffset.TryParse(AppSettings.Current.SteamSyncedAt, CultureInfo.InvariantCulture,
			DateTimeStyles.None, out value);

	/// <summary>Steam 相关值都不是可观察属性，只能手动通知。</summary>
	private void RaiseSteamProperties()
	{
		LoadCachedAchievementRows();
		OnPropertyChanged("HasSteamSync");
		OnPropertyChanged("HasNoSteamSync");
		OnPropertyChanged("HasSteamData");
		OnPropertyChanged("HasSteamSyncMessage");
		OnPropertyChanged("SteamSyncMessageText");
		OnPropertyChanged("SteamSyncedAtText");
		OnPropertyChanged("SteamPlaytimeText");
		OnPropertyChanged("SteamRecentText");
		OnPropertyChanged("SteamLastPlayedText");
		OnPropertyChanged("SteamDistanceText");
		OnPropertyChanged("SteamAchievementProgressText");
		OnPropertyChanged("SteamAchievementPercent");
		OnPropertyChanged("SteamAchievementPercentText");
		OnPropertyChanged("HasAchievementList");
		OnPropertyChanged("Achievements");
		OnPropertyChanged("AchievementsExpanded");
		OnPropertyChanged("AchievementsToggleText");
		OnPropertyChanged("EnsureSteamOnLaunch");
		OnPropertyChanged("PlaytimeText");
		OnPropertyChanged("MachineStatsBandText");
	}

	/// <summary>本机累计启动次数（不分账户）。</summary>
	public string LaunchCountText => AppSettings.Current.LaunchCount.ToString(CultureInfo.CurrentCulture);

	/// <summary>本机上次启动时间；从未启动过则显示「无」。</summary>
	public string LastLaunchText
	{
		get
		{
			string text = PlaytimeTracker.FormatLastLaunchAt(AppSettings.Current);
			return string.IsNullOrWhiteSpace(text) ? Strings.Account_NoneValue : text;
		}
	}

	/// <summary>账户页每次显示时刷新本机统计（统计值不是可观察属性，只能手动通知）。</summary>
	public void RefreshMachineStats()
	{
		OnPropertyChanged("SelectedAccountItem");
		OnPropertyChanged("PlaytimeText");
		OnPropertyChanged("HasMachineStats");
		OnPropertyChanged("MachineStatsBandText");
		OnPropertyChanged("LaunchCountText");
		OnPropertyChanged("LastLaunchText");
		RaiseSteamProperties();
		SyncSteamIfStale();
	}

	public AccountDetailsViewModel(AccountPageViewModel parent)
	{
		this.parent = parent;
		parent.PropertyChanged += OnParentPropertyChanged;
		parent.AccountList.PropertyChanged += OnChildPropertyChanged;
		parent.Appearance.PropertyChanged += OnChildPropertyChanged;
		parent.OfflineUuid.PropertyChanged += OnChildPropertyChanged;
	}

	private void OnParentPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		OnPropertyChanged(e.PropertyName);
	}

	private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender == parent.AccountList)
		{
			OnPropertyChanged("AccountList");
			if (e.PropertyName == "SelectedAccount")
			{
				OnPropertyChanged("SelectedAccount");
				OnPropertyChanged("CanRenameSelectedAccount");
				OnPropertyChanged("SelectedAccountItem");
				OnPropertyChanged("PlaytimeText");
		OnPropertyChanged("HasMachineStats");
		OnPropertyChanged("MachineStatsBandText");
				OnPropertyChanged("LaunchCountText");
				OnPropertyChanged("LastLaunchText");
				RaiseSteamProperties();
			}
		}
		else if (sender == parent.Appearance)
		{
			OnPropertyChanged("Appearance");
		}
		else if (sender == parent.OfflineUuid)
		{
			OnPropertyChanged("OfflineUuid");
		}
	}
}

/// <summary>成就列表里的一行。只读，数据来自服务器转发的 Steam 接口。</summary>
public sealed class SteamAchievementRowViewModel
{
	private readonly SteamAchievementItem item;

	public SteamAchievementRowViewModel(SteamAchievementItem item)
	{
		this.item = item;
	}

	public string Name => string.IsNullOrWhiteSpace(item.Name) ? item.Api : item.Name;

	public string Desc => item.Desc ?? "";

	public bool HasDesc => Desc.Length > 0;

	public bool Unlocked => item.Achieved;

	public bool Locked => !item.Achieved;

	/// <summary>已解锁的带上解锁日期；没拿到时间就只说「已解锁」。</summary>
	public string StateText
	{
		get
		{
			if (!item.Achieved)
			{
				return Strings.Account_SteamAchievementLocked;
			}
			if (item.UnlockedAt <= 0)
			{
				return Strings.Account_SteamAchievementUnlocked;
			}
			try
			{
				string when = DateTimeOffset.FromUnixTimeSeconds(item.UnlockedAt).ToLocalTime()
					.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture);
				return string.Format(Strings.Account_SteamAchievementUnlockedAtFormat, when);
			}
			catch
			{
				return Strings.Account_SteamAchievementUnlocked;
			}
		}
	}

	/// <summary>全局解锁率——一眼看出这项有多稀有。</summary>
	public string GlobalText => string.Format(Strings.Account_SteamAchievementGlobalFormat,
		(item.Percent ?? 0).ToString("0.#", CultureInfo.CurrentCulture));
}
