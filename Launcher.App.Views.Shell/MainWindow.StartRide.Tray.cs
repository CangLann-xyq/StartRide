using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using StartRide.Core;

namespace Launcher.App.Views.Shell;

/// <summary>
/// 主窗口的托盘集成（partial 扩展，不动反编译主体）。
///
/// 背景：设置里一直有「最小化到托盘」这个开关，但整个工程里没有任何托盘代码，
/// 勾了也没有任何变化。这里补齐：
///   · 启动完成时装一个托盘图标，双击回到主界面
///   · 右键菜单：打开主界面 / 启动 BeamNG.drive / 打开游戏目录 / 退出启动器
///   · 「关闭窗口时收进托盘」开启后，点 X 只隐藏；从托盘菜单退出才真的退
/// </summary>
public partial class MainWindow
{
	private TrayIcon? startRideTray;

	/// <summary>托盘菜单里点了「退出启动器」——此时不再拦截关闭。</summary>
	private bool startRideTrayExitRequested;

	private async void StartRideStartup_OnLoaded(object? sender, RoutedEventArgs e)
	{
		EnsureStartRideTray();

		// 启动自检：自动探测游戏目录 / 检查车辆模组完整性
		// （设置页「启动器启动时」那两个开关以前没有任何代码读，这里接上）
		try
		{
			var service = new StartupTaskService(AppSettings.Current);
			var result = await service.RunAsync().ConfigureAwait(true);

			if (result.HasNotice)
			{
				floatingMessageService.Show(result.Notice);
			}

			// 自动修正了游戏目录 → 让设置页/首页立刻显示新值
			if (result.GameDirectoryApplied)
			{
				viewModel.SettingsPage.General.RefreshBeamNgDirectoryState();
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Startup self-check failed.");
		}
	}

	/// <summary>装托盘图标（幂等）。窗口句柄没好就等下一次。</summary>
	private void EnsureStartRideTray()
	{
		try
		{
			if (startRideTray != null) return;

			var tray = new TrayIcon(this, "StartRide 启动器");
			tray.OnActivate = StartRideRestoreFromTray;
			tray.OnLaunchGame = StartRideTrayLaunchGame;
			tray.OnOpenGameFolder = StartRideTrayOpenGameFolder;
			tray.OnExit = StartRideTrayExit;

			if (tray.Install())
			{
				startRideTray = tray;
				logger.LogInformation("StartRide tray icon installed. CloseToTray={CloseToTray}",
					StartRide.Core.AppSettings.Current.CloseToTray);
			}
			else
			{
				tray.Dispose();
				logger.LogWarning("StartRide tray icon install rejected by shell.");
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Install tray icon failed.");
		}
	}

	/// <summary>把窗口从托盘叫回来（隐藏状态下 Show 会一并恢复任务栏按钮）。</summary>
	private void StartRideRestoreFromTray()
	{
		Dispatcher.BeginInvoke(new Action(() =>
		{
			try
			{
				if (!IsVisible) Show();
				if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
				Activate();
				Topmost = true;
				Topmost = false;
				Focus();
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Restore main window from tray failed.");
			}
		}));
	}

	/// <summary>托盘菜单直接启动游戏（不必先回到主界面）。</summary>
	private void StartRideTrayLaunchGame()
	{
		try
		{
			var app = AppSettings.Current;
			if (!app.AutoDetectGame && !AppSettings.IsBeamNgInstall(app.GameDirectory))
			{
				StartRideRestoreFromTray();
				return;
			}

			var launcher = new GameLauncher(app);
			if (!launcher.IsInstalled)
			{
				StartRideRestoreFromTray();
				return;
			}

			string? error = launcher.Launch(withMod: app.AutoInstallMod);
			if (error != null)
			{
				// 启动失败就回到界面让用户看到原因，别静默失败
				StartRideRestoreFromTray();
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Launch game from tray failed.");
			StartRideRestoreFromTray();
		}
	}

	private void StartRideTrayOpenGameFolder()
	{
		try
		{
			string dir = AppSettings.Current.GameDirectory ?? "";
			if (Directory.Exists(dir))
			{
				Process.Start(new ProcessStartInfo("explorer.exe", "\"" + dir + "\""));
			}
			else
			{
				StartRideRestoreFromTray();
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Open game folder from tray failed.");
		}
	}

	private void StartRideTrayExit()
	{
		Dispatcher.BeginInvoke(new Action(() =>
		{
			startRideTrayExitRequested = true;
			try
			{
				if (!IsVisible) Show();
				Close();
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Exit launcher from tray failed.");
			}
		}));
	}

	/// <summary>
	/// 点关闭时是否收进托盘。返回 true 表示「已拦下，只隐藏窗口」。
	/// </summary>
	internal bool StartRideTryMinimizeToTray()
	{
		try
		{
			if (startRideTrayExitRequested) return false;
			if (!AppSettings.Current.CloseToTray) return false;

			EnsureStartRideTray();
			Hide();
			return true;
		}
		catch
		{
			// 出任何问题都按原来的"真关闭"走，不能让用户关不掉窗口
			return false;
		}
	}

	/// <summary>游戏运行状态变化时刷一下托盘提示（由主页 VM 调用）。</summary>
	internal void StartRideTrayUpdateTooltip(string tooltip)
	{
		try
		{
			startRideTray?.UpdateTooltip(tooltip);
		}
		catch { }
	}

	protected override void OnClosed(EventArgs e)
	{
		try
		{
			startRideTray?.Dispose();
			startRideTray = null;
		}
		catch { }
		base.OnClosed(e);
	}
}
