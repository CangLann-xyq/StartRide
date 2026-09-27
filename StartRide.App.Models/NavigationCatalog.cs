using System;
using System.Collections.Generic;
using StartRide.App.Resources;
using StartRide.App.Utilities;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace StartRide.App.Models;

internal static class NavigationCatalog
{
	public const string AccountPage = "Account";

	public const string HomePage = "Home";

	public const string DownloadPage = "Download";

	public const string InstallPage = "Install";

	public const string GameSettingsPage = "GameSettings";

	public const string MultiplayerPage = "Multiplayer";

	public const string ResourcesPage = "Resources";

	public const string SettingsPage = "Settings";

	/// <summary>
	/// 「高光时刻」是一级导航项，但它复用回放页的 View/VM —— 只是进页时把视图模式切到"高光"。
	/// 它必须有自己的 Page 名：否则导航栏两项的 Page 都叫 "Install"，
	/// 选中判据（IsPage(item.Page, CurrentPage)）会让两项同时高亮。
	/// </summary>
	public const string HighlightsPage = "Highlights";

	public static readonly string[] PageOrder = new string[9] { "Account", "Home", "Multiplayer", "Download", "GameSettings", "Resources", "Settings", "Install", "Highlights" };

	public static NavigationItem CreateDownloadTasksItem()
	{
		return new NavigationItem
		{
			Page = "GameSettings",
			Title = "游戏设置",
			Icon = "\ue713",
			IconKey = "beamng/gear"
		};
	}

	public static IEnumerable<NavigationItem> CreatePrimaryItems()
	{
		// StartRide 主导航：BeamNG 真实功能页。
		return new global::_003C_003Ez__ReadOnlyArray<NavigationItem>(new NavigationItem[8]
		{
			new NavigationItem
			{
				Page = "Account",
				Title = "账户",
				Icon = "\ue77b",
				// 必须写成 目录/文件名 双前缀：SvgIcon 会按 /Assets/Icons/<key>.svg 找，
				// 写 general/person 会找不到文件（图标空白）。
				IconKey = "general/general_person"
			},
			new NavigationItem
			{
				Page = "Home",
				Title = "启动",
				Icon = "\ue80f",
				IconKey = "general/general_home"
			},
			new NavigationItem
			{
				Page = "Multiplayer",
				Title = "联机",
				Icon = "\ue701",
				IconKey = "beamng/online"
			},
			new NavigationItem
			{
				Page = "Download",
				Title = "车辆管理",
				Icon = "\ue7c3",
				IconKey = "beamng/car"
			},
			new NavigationItem
			{
				Page = "Resources",
				Title = "模组仓库",
				Icon = "\ue8f1",
				IconKey = "beamng/mod"
			},
			new NavigationItem
			{
				Page = "Install",
				Title = "回放",
				Icon = "\ue768",
				IconKey = "beamng/replay"
			},
			new NavigationItem
			{
				// 与「回放」共用 ReplaysPageView/VM，只是进页时切到高光视图模式。
				// 图标 beamng/record 是摄像机（stroke-width=4，与相邻导航项同规格）。
				Page = "Highlights",
				Title = "高光时刻",
				Icon = "\ue722",
				IconKey = "beamng/record"
			},
			new NavigationItem
			{
				Page = "Settings",
				Title = "全局设置",
				Icon = "\ue713",
				IconKey = "beamng/gear"
			}
		});
	}

	public static IEnumerable<NavigationItem> CreateSecondaryItems(string currentPage)
	{
		return currentPage switch
		{
			"GameSettings" => new global::_003C_003Ez__ReadOnlyArray<NavigationItem>(new NavigationItem[3]
			{
				new NavigationItem
				{
					Page = "GameSettings",
					Title = "已装车辆",
					Icon = "\ue8a5"
				},
				new NavigationItem
				{
					Page = "GameSettings",
					Title = "内存与画质",
					Icon = "\ue950"
				},
				new NavigationItem
				{
					Page = "GameSettings",
					Title = "游戏目录",
					Icon = "\ue8b7"
				}
			}),
			"Resources" => new global::_003C_003Ez__ReadOnlyArray<NavigationItem>(new NavigationItem[5]
			{
				new NavigationItem
				{
					Page = "Resources",
					Title = "车辆模组",
					Icon = "\ue8f1"
				},
				new NavigationItem
				{
					Page = "Resources",
					Title = "地图模组",
					Icon = "\ue8a5"
				},
				new NavigationItem
				{
					Page = "Resources",
					Title = "涂装与贴图",
					Icon = "\ue790"
				},
				new NavigationItem
				{
					Page = "Resources",
					Title = "联机脚本",
					Icon = "\ue707"
				},
				new NavigationItem
				{
					Page = "Resources",
					Title = "推荐合集",
					Icon = "\ue8f1"
				}
			}),
			"Settings" => new global::_003C_003Ez__ReadOnlyArray<NavigationItem>(new NavigationItem[5]
			{
				new NavigationItem
				{
					Page = "Settings",
					Title = "通用",
					Icon = "\ue713"
				},
				new NavigationItem
				{
					Page = "Settings",
					Title = "内存与启动",
					Icon = "\ue768"
				},
				new NavigationItem
				{
					Page = "Settings",
					Title = "中继服务器",
					Icon = "\ue950"
				},
				new NavigationItem
				{
					Page = "Settings",
					Title = "外观",
					Icon = "\ue790"
				},
				new NavigationItem
				{
					Page = "Settings",
					Title = "关于",
					Icon = "\ue946"
				}
			}),
			_ => Array.Empty<NavigationItem>(),
		};
	}

	public static NavigationItem CreateLoaderItem(ILoaderProvider provider)
	{
		return new NavigationItem
		{
			Page = provider.Kind.ToString(),
			Title = LoaderDisplayNameProvider.GetDisplayName(provider.Kind),
			Icon = ((provider.Kind == LoaderKind.Vanilla) ? "\ue7c3" : "\ue8b7"),
			Loader = provider.Kind
		};
	}

	public static bool IsPage(string? left, string? right)
	{
		return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 这个页面名有没有**专属宿主页面**（MainWindow.ResolvePageRoot 里有一个对应分支）。
	/// 兜底页 GeneralPageView 靠它决定自己要不要显示 —— 见 FallbackPageVisibilityConverter。
	/// ⚠️ 以后新增一级页面，把它加进 PageOrder 就行，兜底页会自动让位；
	/// 忘了加 = 兜底页盖在真页面上（叠标题 + 页内点不动），而且**不会报任何错**。
	/// </summary>
	public static bool HasDedicatedView(string? page)
	{
		if (string.IsNullOrEmpty(page))
		{
			return false;
		}
		foreach (string candidate in PageOrder)
		{
			if (IsPage(candidate, page))
			{
				return true;
			}
		}
		return false;
	}

	public static bool UsesLocalModpackDrop(string? currentPage, bool isGameSettingsListStep)
	{
		if (IsPage(currentPage, "GameSettings"))
		{
			return isGameSettingsListStep;
		}
		if (!IsPage(currentPage, "Account") && !IsPage(currentPage, "Home") && !IsPage(currentPage, "Download") && !IsPage(currentPage, "Install") && !IsPage(currentPage, "Highlights") && !IsPage(currentPage, "Resources"))
		{
			return IsPage(currentPage, "Settings");
		}
		return true;
	}
}
