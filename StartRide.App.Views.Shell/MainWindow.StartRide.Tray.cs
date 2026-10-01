using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using StartRide.App.Services;
using StartRide.Core;

namespace StartRide.App.Views.Shell;

public partial class MainWindow
{
	private TrayIcon? startRideTray;

	private bool startRideTrayExitRequested;

	private async void StartRideStartup_OnLoaded(object? sender, RoutedEventArgs e)
	{
		EnsureStartRideTray();

		try
		{
			var service = new StartupTaskService(AppSettings.Current);
			var result = await service.RunAsync().ConfigureAwait(true);

			if (result.HasNotice)
			{
				floatingMessageService.Show(result.Notice);
			}

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
				LauncherWindowBackdrop.Reapply(this, themeService);
				Focus();
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Restore main window from tray failed.");
			}
		}));
	}

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

			string? error = launcher.Launch(withMod: app.PreInstallMod);
			if (error != null)
			{
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

	internal bool StartRideTryMinimizeToTray()
	{
		try
		{
			if (startRideTrayExitRequested) return false;

			bool inRoom = StartRideMultiplayerRuntime.IsInRoom;
			bool closeToTray = AppSettings.Current.CloseToTray;
			if (!closeToTray && !inRoom) return false;

			EnsureStartRideTray();
			Hide();

			if (inRoom && !closeToTray)
			{

				logger.LogInformation("联机进行中，关闭窗口改为收进托盘以保持本地桥与中继连接。");
				StartRideTrayNotify("StartRide 仍在后台保持联机",
					"关闭窗口不会退出联机。要真正退出请右键托盘图标选择「退出启动器」。");
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	private void StartRideTrayNotify(string title, string text)
	{
		try
		{
			startRideTray?.ShowBalloon(title, text);
		}
		catch
		{
		}
	}

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
