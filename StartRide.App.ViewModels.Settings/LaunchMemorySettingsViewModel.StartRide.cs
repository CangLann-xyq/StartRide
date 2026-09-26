using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.Utilities;
using Launcher.Domain.Models;
using StartRide.Core;

namespace StartRide.App.ViewModels.Settings;

/// <summary>
/// 「内存与启动」页的 StartRide 扩展：
///   1. 展示本机每根内存条（容量/代数/频率/插槽/型号）——数据来自 WMI
///   2. 内存分配值直接写进 StartRide 设置，启动时用 Windows 作业对象作用到游戏进程上（真生效）
/// </summary>
public sealed partial class LaunchMemorySettingsViewModel
{
	private readonly StartRideHardwareService hardwareService = new StartRideHardwareService();

	private bool limitGameMemory = true;

	private string memorySlotSummaryText = string.Empty;

	private string memoryTotalSummaryText = string.Empty;

	private bool isHardwareLoading;

	private AsyncRelayCommand? refreshMemoryHardwareCommand;

	/// <summary>本机内存条列表。</summary>
	public ObservableCollection<MemoryModuleItem> MemoryModules { get; } = new ObservableCollection<MemoryModuleItem>();

	/// <summary>「共 4 个插槽，已用 2 个」</summary>
	public string MemorySlotSummaryText
	{
		get => memorySlotSummaryText;
		set
		{
			if (memorySlotSummaryText != value)
			{
				memorySlotSummaryText = value;
				OnPropertyChanged("MemorySlotSummaryText");
			}
		}
	}

	/// <summary>「物理内存 32 GB · 可用 18.4 GB」</summary>
	public string MemoryTotalSummaryText
	{
		get => memoryTotalSummaryText;
		set
		{
			if (memoryTotalSummaryText != value)
			{
				memoryTotalSummaryText = value;
				OnPropertyChanged("MemoryTotalSummaryText");
			}
		}
	}

	public bool IsHardwareLoading
	{
		get => isHardwareLoading;
		set
		{
			if (isHardwareLoading != value)
			{
				isHardwareLoading = value;
				OnPropertyChanged("IsHardwareLoading");
			}
		}
	}

	public bool HasMemoryModules => MemoryModules.Count > 0;

	/// <summary>
	/// 限制游戏内存（真实生效）：开启后启动游戏时会给 BeamNG.drive 进程套一个
	/// Windows 作业对象内存上限，超出就分配失败。
	/// </summary>
	public bool LimitGameMemory
	{
		get => limitGameMemory;
		set
		{
			if (limitGameMemory != value)
			{
				limitGameMemory = value;
				OnPropertyChanged("LimitGameMemory");
				SyncStartRideMemorySettings();
			}
		}
	}

	public IAsyncRelayCommand RefreshMemoryHardwareCommand =>
		refreshMemoryHardwareCommand ?? (refreshMemoryHardwareCommand = new AsyncRelayCommand(RefreshMemoryHardwareAsync));

	/// <summary>设置加载时初始化（WMI 查询放后台，避免拖慢启动）。</summary>
	private void InitializeStartRideMemory()
	{
		try
		{
			LimitGameMemory = AppSettings.Current.LimitGameMemory;
		}
		catch { }

		// 「内存与启动」页里的那些启动开关，真正的执行者是 StartRide 侧的启动流程。
		// LauncherSettings 里那份 Default* 只是镜像，如果只从它取值，
		// 用户在别处（StartRide 设置）改过的开关就会被旧值覆盖掉 → 这里以 AppSettings 为准。
		SyncLaunchBehaviorFromStartRide();

		UpdateMemorySummaryFromSystemService();
		_ = RefreshMemoryHardwareAsync();
	}

	/// <summary>AppSettings → 界面（用 LoadState 包住，避免回写触发持久化）。</summary>
	private void SyncLaunchBehaviorFromStartRide()
	{
		try
		{
			var app = AppSettings.Current;
			LoadState(delegate
			{
				DefaultCheckFilesBeforeLaunch = app.CheckFilesBeforeLaunch;
				DefaultAutoRepairMissingFiles = app.AutoRepairGameConfig;
				DefaultMinimizeLauncherAfterLaunch = app.MinimizeToTray;
				DefaultLaunchFullScreen = app.LaunchFullScreen;
				DefaultPreLaunchCommand = app.PreLaunchCommand;
				DefaultWaitForPreLaunchCommand = app.WaitForPreLaunchCommand;
				DefaultPostExitCommand = app.PostExitCommand;
				DefaultGameArguments = app.GameArguments;
			});
		}
		catch { }
	}

	/// <summary>
	/// 界面 → AppSettings。这是让「内存与启动」页里那些开关真正生效的关键一步：
	/// 以前它们只写进 LauncherSettings，而启动 BeamNG 的代码从来不读那份配置。
	/// </summary>
	internal void SyncStartRideLaunchBehaviorSettings()
	{
		try
		{
			var app = AppSettings.Current;
			app.CheckFilesBeforeLaunch = DefaultCheckFilesBeforeLaunch;
			// 「自动修复缺失文件」原来只写 LauncherSettings.DefaultAutoRepairMissingFiles，
			// 而真正调用 ConfigRepairService 的是 StartRideLaunchService（读 AppSettings.AutoRepairGameConfig）
			// → 开关是死的。现在接到同一个键上。
			app.AutoRepairGameConfig = DefaultAutoRepairMissingFiles;
			app.MinimizeToTray = DefaultMinimizeLauncherAfterLaunch;
			app.LaunchFullScreen = DefaultLaunchFullScreen;
			app.PreLaunchCommand = NormalizeText(DefaultPreLaunchCommand) ?? "";
			app.WaitForPreLaunchCommand = DefaultWaitForPreLaunchCommand;
			app.PostExitCommand = NormalizeText(DefaultPostExitCommand) ?? "";
			app.GameArguments = NormalizeText(DefaultGameArguments) ?? "";
			app.Save();
		}
		catch { }
	}

	/// <summary>用系统内存服务先填一版总数，WMI 结果到了再覆盖。</summary>
	private void UpdateMemorySummaryFromSystemService()
	{
		try
		{
			var snapshot = systemMemoryService.GetSnapshot();
			int totalMb = (int)(snapshot.TotalMemoryBytes / 1024L / 1024L);
			int freeMb = (int)(snapshot.AvailableMemoryBytes / 1024L / 1024L);
			MemoryTotalSummaryText = string.Format(
				Strings.Settings_MemoryTotalSummaryFormat,
				MemorySizeTextFormatter.Format(totalMb),
				MemorySizeTextFormatter.FormatGb(freeMb));
		}
		catch { }
	}

	private async Task RefreshMemoryHardwareAsync()
	{
		if (IsHardwareLoading) return;
		IsHardwareLoading = true;
		try
		{
			var snapshot = await Task.Run(() => hardwareService.GetMemorySnapshot()).ConfigureAwait(true);

			MemoryModules.Clear();
			foreach (var module in snapshot.Modules)
			{
				MemoryModules.Add(new MemoryModuleItem(module));
			}

			if (MemoryModules.Count > 0)
			{
				MemorySlotSummaryText = string.Format(
					Strings.Settings_MemorySlotSummaryFormat, snapshot.TotalSlots, snapshot.PopulatedSlots);
			}
			else
			{
				MemorySlotSummaryText = string.IsNullOrWhiteSpace(snapshot.Error)
					? Strings.Settings_MemoryNoModule
					: Strings.Settings_MemoryNoModule + "（" + snapshot.Error + "）";
			}
			OnPropertyChanged("HasMemoryModules");
		}
		catch (Exception)
		{
			MemorySlotSummaryText = Strings.Settings_MemoryNoModule;
		}
		finally
		{
			IsHardwareLoading = false;
		}
	}

	/// <summary>
	/// 把「内存模式 + 数值」换算成真实要施加的上限，写进 StartRide 设置。
	/// 自动模式用系统算出来的推荐值，手动模式用滑杆值。
	/// </summary>
	internal void SyncStartRideMemorySettings()
	{
		try
		{
			var app = AppSettings.Current;
			bool automatic = SelectedMemoryModeOption?.Mode != MemorySettingsMode.Manual;
			int mb = automatic ? AutomaticMemoryMb : NormalizeMemoryValue(DefaultMemoryMb);
			if (mb <= 0) mb = 4096;

			app.LimitGameMemory = LimitGameMemory;
			app.MaxMemoryMB = mb;
			app.Save();
		}
		catch { }
	}
}

/// <summary>内存条列表项。</summary>
public sealed class MemoryModuleItem
{
	public string Title { get; }

	public string Subtitle { get; }

	public MemoryModuleItem(MemoryModuleInfo module)
	{
		Title = string.Format(
			Strings.Settings_MemoryModuleFormat,
			module.CapacityText,
			string.IsNullOrWhiteSpace(module.Generation) ? Strings.Settings_MemoryUnknownValue : module.Generation,
			module.SpeedText,
			module.SlotText);
		Subtitle = module.ModelText;
	}
}
