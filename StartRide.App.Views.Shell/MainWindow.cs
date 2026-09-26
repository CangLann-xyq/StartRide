using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using StartRide.App.Controls;
using StartRide.App.Diagnostics;
using StartRide.App.Models;
using StartRide.App.Resources;
using StartRide.App.Services;
using StartRide.App.ViewModels.Shell;
using StartRide.App.Views.Account.Dialogs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StartRide.App.Views.Shell;

public partial class MainWindow : Window, IComponentConnector
{
	private static readonly TimeSpan ShutdownTimeout;

	public static readonly DependencyProperty IsMenuExpandedProperty;

	private readonly NavigationMenuAnimationService navigationMenuService;

	private readonly IAccountDialogService accountDialogService;

	private readonly IFloatingMessageService floatingMessageService;

	private readonly LauncherStateSyncService stateSyncService;

	private readonly LauncherShutdownService shutdownService;

	private readonly MainWindowPlacementService windowPlacementService;

	private readonly PageTransitionService pageTransitionService;

	private readonly MainViewModel viewModel;

	private readonly ILogger<MainWindow> logger;

	private IDataObject? cachedThirdPartyAccountDropData;

	private AuthlibInjectorServerDropResult cachedThirdPartyAccountDropResult;

	private bool cachedThirdPartyAccountDropBlocked;

	private DispatcherOperation? pendingDragStateReset;

	private DialogHost[]? cachedDialogHosts;

	private bool isShutdownInProgress;

	private bool isShutdownComplete;

	private readonly IThemeService themeService;

	private bool isFullscreen;

	private WindowState preFullscreenState = WindowState.Normal;

	private ResizeMode preFullscreenResizeMode = ResizeMode.CanResize;

	private WindowStyle preFullscreenWindowStyle = WindowStyle.SingleBorderWindow;

	/// <summary>最大化尺寸的钳制钩子。见 <see cref="ClampMaximizedSize"/>。</summary>
	private const int WmGetMinMaxInfo = 0x0024;

	private const uint MonitorDefaultToNearest = 2u;

	public FrameworkElement LauncherPreblurredBackdropSourceElement => AmbientBackdropRoot;

	public bool IsMenuExpanded
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsMenuExpandedProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsMenuExpandedProperty, (object)value);
		}
	}

	public MainWindow(MainViewModel viewModel, IWindowService windowService, IAccountDialogService accountDialogService, IFloatingMessageService floatingMessageService, LauncherStateSyncService stateSyncService, LauncherShutdownService shutdownService, MainWindowPlacementService windowPlacementService, IThemeService themeService, ILogger<MainWindow>? logger = null)
	{
		InitializeComponent();
		this.viewModel = viewModel;
		this.themeService = themeService;
		this.accountDialogService = accountDialogService;
		this.floatingMessageService = floatingMessageService;
		this.stateSyncService = stateSyncService;
		this.shutdownService = shutdownService;
		this.windowPlacementService = windowPlacementService;
		this.logger = logger ?? NullLogger<MainWindow>.Instance;
		navigationMenuService = new NavigationMenuAnimationService(MenuColumn);
		pageTransitionService = new PageTransitionService(((DispatcherObject)this).Dispatcher, ResolvePageRoot, viewModel.CurrentPage);
		base.DataContext = viewModel;
		windowService.Attach(this);
		AddAccountDialogView addAccountView = (AddAccountDialogHost.DialogContent as AddAccountDialogView) ?? throw new InvalidOperationException("The add-account dialog content is not initialized.");
		accountDialogService.Attach(viewModel.AccountPage, AddAccountDialogHost, addAccountView, DeleteAccountDialogHost, RenameAccountDialogHost, SkinModelDialogHost, SkinManagerDialogHost);
		viewModel.PropertyChanged += ViewModel_PropertyChanged;
		LauncherWindowBackdrop.Attach(this, themeService);
		NativeCaptionButtons.Hide(this);
		base.SourceInitialized += MainWindow_OnSourceInitialized;
		base.Loaded += MainWindow_Loaded;
		// StartRide：托盘图标 + 启动自检（见 MainWindow.StartRide.Tray.cs）
		base.Loaded += StartRideStartup_OnLoaded;
		base.Closing += Window_OnClosing;
		base.Closed += delegate
		{
			stateSyncService.Stop();
		};
	}

	/// <summary>
	/// 窗口句柄一建好就挂消息钩子。
	///
	/// ⚠️ 为什么必须钩 WM_GETMINMAXINFO：本窗口是 WindowStyle=None + WindowChrome，
	/// 最大化时系统按「显示器 + 8px 边框」给尺寸，实测 1920x1080 上窗口变成
	/// 1936x1096、原点 (-8,-8) —— 界面比屏幕高 16px，顶部切 8px、
	/// 底部状态条被推到任务栏底下压住（用户报的"全屏的时候有错位"）。
	/// 全屏（F11，WindowStyle=None + NoResize）不受这个 bug 影响，保持钳到显示器。
	/// </summary>
	private void MainWindow_OnSourceInitialized(object? sender, EventArgs e)
	{
		try
		{
			IntPtr handle = new WindowInteropHelper(this).Handle;
			HwndSource? source = handle == IntPtr.Zero ? null : HwndSource.FromHwnd(handle);
			source?.AddHook(WindowMessageHook);
		}
		catch (Exception exception)
		{
			// 钩不上只是错位，不该让窗口起不来。
			logger.LogWarning(exception, "Failed to hook the window message loop.");
		}
	}

	private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (message == WmGetMinMaxInfo)
		{
			// ⚠️ 全屏（F11）也走 WindowState.Maximized，但它要盖住任务栏 ——
			// 所以全屏钳「显示器」，普通最大化钳「工作区」，两者不能混。
			ClampMaximizedSize(hwnd, lParam, isFullscreen);
		}
		// 不设 handled：这只是"改个数字"，系统的既有处理继续走。
		return IntPtr.Zero;
	}

	private static void ClampMaximizedSize(IntPtr hwnd, IntPtr lParam, bool useWholeMonitor)
	{
		try
		{
			IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
			if (monitor == IntPtr.Zero)
			{
				return;
			}
			MonitorInformation info = default(MonitorInformation);
			info.size = Marshal.SizeOf<MonitorInformation>();
			if (!GetMonitorInfo(monitor, ref info))
			{
				return;
			}
			NativeRectangle target = useWholeMonitor ? info.monitor : info.work;
			MinMaxInformation value = Marshal.PtrToStructure<MinMaxInformation>(lParam);
			// ptMaxPosition / ptMaxSize 都是相对「显示器原点」的坐标：
			// 任务栏在下面时工作区高度少 48px，最大化就该只占这 1032。
			value.maxPosition.x = target.left - info.monitor.left;
			value.maxPosition.y = target.top - info.monitor.top;
			value.maxSize.x = target.right - target.left;
			value.maxSize.y = target.bottom - target.top;
			Marshal.StructureToPtr(value, lParam, false);
		}
		catch (Exception)
		{
			// 钳不住就退回系统默认：宁可错位一点，也不能在这里抛异常。
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct NativePoint
	{
		public int x;

		public int y;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct NativeRectangle
	{
		public int left;

		public int top;

		public int right;

		public int bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MinMaxInformation
	{
		public NativePoint reserved;

		public NativePoint maxSize;

		public NativePoint maxPosition;

		public NativePoint minTrackSize;

		public NativePoint maxTrackSize;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MonitorInformation
	{
		public int size;

		public NativeRectangle monitor;

		public NativeRectangle work;

		public uint flags;
	}

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

	[DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInformation info);

	private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is MainViewModel mainViewModel)
		{
			if (e.PropertyName == "CurrentPage")
			{
				pageTransitionService.MoveTo(mainViewModel.CurrentPage);
			}
			else if (e.PropertyName == "IsMenuExpanded")
			{
				IsMenuExpanded = mainViewModel.IsMenuExpanded;
			}
		}
	}

	private void TitleBarDragArea_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ClickCount >= 2)
		{
			ToggleWindowMaximizedState();
			e.Handled = true;
		}
		else if (e.LeftButton == MouseButtonState.Pressed)
		{
			DragMove();
			e.Handled = true;
		}
	}

	private void ToggleWindowMaximizedState()
	{
		if (isFullscreen)
		{
			return;
		}
		ResizeMode resizeMode = base.ResizeMode;
		if ((uint)resizeMode > 1u)
		{
			base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
		}
	}

	/// <summary>
	/// F11 进出全屏；Esc 只负责退出。全屏时把 WindowStyle 换成 None（否则盖不住任务栏），
	/// 同时把顶上的标题行收成 0 高、隐藏导航条 —— 窗口按钮是浮层（RowSpan=2 + ZIndex），
	/// 所以仍然浮在内容右上角，用户不会被"关在里面出不来"。
	/// </summary>
	private void ToggleFullscreen()
	{
		if (!isFullscreen)
		{
			preFullscreenState = base.WindowState;
			preFullscreenResizeMode = base.ResizeMode;
			preFullscreenWindowStyle = base.WindowStyle;
			// ⚠️ WindowStyle / ResizeMode 在「已最大化」状态下改会被系统忽略，必须先回 Normal。
			base.WindowState = WindowState.Normal;
			base.WindowStyle = WindowStyle.None;
			base.ResizeMode = ResizeMode.NoResize;
			// ⚠️ isFullscreen 必须在改 WindowState 之前置位：最大化会触发
			// WM_GETMINMAXINFO，钩子要靠这个字段决定钳「显示器」还是「工作区」。
			isFullscreen = true;
			base.WindowState = WindowState.Maximized;
		}
		else
		{
			isFullscreen = false;
			base.WindowState = WindowState.Normal;
			base.WindowStyle = preFullscreenWindowStyle;
			base.ResizeMode = preFullscreenResizeMode;
			// ⚠️ 必须在这里、在恢复 WindowState 之前重剥一次原生标题栏样式位。
			// WindowStyle 从 None 改回 SingleBorderWindow 时，WPF 会把整套原生样式写回去，
			// 把 NativeCaptionButtons 在 SourceInitialized 时剥掉的边框又装回来；装回来之后
			// 这次最大化就会按「工作区 + 每边 8px 边框」算尺寸 —— 实测 Esc 退出全屏后窗口
			// 变成 (-8,-8)-(1928,1040)，四边都探出屏幕，底部还被任务栏压住。
			// 放在 WindowState 之前是因为这条路径上"恢复最大化"本身就要重算一次尺寸。
			NativeCaptionButtons.Reapply(this);
			base.WindowState = preFullscreenState;
		}
		ApplyFullscreenChrome();
		// WindowStyle 变化会让 WindowChrome 的圆角/边框失效，DWM 属性也可能被重置，重新下发一次。
		LauncherWindowBackdrop.Reapply(this, themeService);
		UpdateCaptionButtons();
	}

	private void ApplyFullscreenChrome()
	{
		// 最大化时也算"贴满屏幕"：窗口矩形被钳到工作区之后，四角再留 12px 圆角
		// 就会直接透出后面的桌面（以前是靠系统多给 8px 把圆角推到屏幕外盖住的）。
		bool square = isFullscreen || base.WindowState == WindowState.Maximized;
		WindowChrome windowChrome = WindowChrome.GetWindowChrome(this);
		if (windowChrome != null)
		{
			windowChrome.CornerRadius = (square ? new CornerRadius(0.0) : new CornerRadius(12.0));
			windowChrome.ResizeBorderThickness = (isFullscreen ? new Thickness(0.0) : new Thickness(8.0));
		}
		if (WindowRootBorder != null)
		{
			WindowRootBorder.CornerRadius = (square ? new CornerRadius(0.0) : new CornerRadius(12.0));
		}
		if (TitleBarRow != null)
		{
			TitleBarRow.Height = (isFullscreen ? new GridLength(0.0) : new GridLength(52.0));
		}
		if (ShellNavView != null)
		{
			ShellNavView.Visibility = (isFullscreen ? Visibility.Collapsed : Visibility.Visible);
		}
		if (TitleBarDragArea != null)
		{
			TitleBarDragArea.Visibility = (isFullscreen ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private void UpdateCaptionButtons()
	{
		bool flag = base.WindowState == WindowState.Maximized;
		if (MaximizeWindowButton != null)
		{
			MaximizeWindowButton.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		}
		if (RestoreWindowButton != null)
		{
			RestoreWindowButton.Visibility = (flag ? Visibility.Visible : Visibility.Collapsed);
		}
	}

	// XAML 里挂的 StateChanged：最大化状态一变就换图标。
	private void Window_OnStateChanged(object? sender, EventArgs e)
	{
		// 最大化/还原会改变"要不要圆角"，得跟着重算一次。
		ApplyFullscreenChrome();
		UpdateCaptionButtons();
	}

	private void MaximizeWindowButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (!isFullscreen)
		{
			base.WindowState = WindowState.Maximized;
		}
	}

	private void RestoreWindowButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (isFullscreen)
		{
			ToggleFullscreen();
		}
		else
		{
			base.WindowState = WindowState.Normal;
		}
	}

	private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.F11)
		{
			ToggleFullscreen();
			e.Handled = true;
		}
		else if (e.Key == Key.Escape && isFullscreen)
		{
			ToggleFullscreen();
			e.Handled = true;
		}
	}

	private FrameworkElement? ResolvePageRoot(string page)
	{
		if (string.Equals(page, "Home", StringComparison.OrdinalIgnoreCase))
		{
			return HomePageView.RootElement;
		}
		if (string.Equals(page, "Account", StringComparison.OrdinalIgnoreCase))
		{
			return AccountPageView.RootElement;
		}
		if (string.Equals(page, "Download", StringComparison.OrdinalIgnoreCase))
		{
			return VehiclesPageView.RootElement;
		}
		if (string.Equals(page, "Install", StringComparison.OrdinalIgnoreCase))
		{
			return ReplaysPageView.RootElement;
		}
		if (string.Equals(page, "GameSettings", StringComparison.OrdinalIgnoreCase))
		{
			return GameSettingsPageView.RootElement;
		}
		if (string.Equals(page, "Multiplayer", StringComparison.OrdinalIgnoreCase))
		{
			return MultiplayerPageView.RootElement;
		}
		if (string.Equals(page, "Resources", StringComparison.OrdinalIgnoreCase))
		{
			return ModsPageView.RootElement;
		}
		if (string.Equals(page, "Settings", StringComparison.OrdinalIgnoreCase))
		{
			return SettingsPageView.RootElement;
		}
		return GeneralPageView.RootElement;
	}

	private void PrewarmTransientUi()
	{
		accountDialogService.Prewarm();
		foreach (AnimatedComboBox item in FindVisualChildren<AnimatedComboBox>((DependencyObject)(object)this))
		{
			item.ApplyTemplate();
		}
		PrewarmPageContent(GetPrewarmPages(), 0);
	}

	private FrameworkElement[] GetPrewarmPages()
	{
		return new FrameworkElement[7] { SettingsPageView, VehiclesPageView, GameSettingsPageView, ModsPageView, AccountPageView, MultiplayerPageView, ReplaysPageView };
	}

	private void PrewarmPageContent(IReadOnlyList<FrameworkElement> pages, int index)
	{
		if (index >= pages.Count)
		{
			return;
		}
		FrameworkElement page = pages[index];
		if (page.Visibility == Visibility.Visible)
		{
			PrewarmPageContent(pages, index + 1);
			return;
		}
		if (UiTransitionGate.IsTransitionActive)
		{
			UiTransitionGate.RunWhenIdle(delegate
			{
				PrewarmPageContent(pages, index);
			});
			return;
		}
		long startedAt = Stopwatch.GetTimestamp();
		BindingBase visibilityBinding = PageContentPrewarm.Begin(page);
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			UiPerformanceLog.LogPageSurface(page, page.Name);
			if (!PageContentPrewarm.End(page, visibilityBinding))
			{
				logger.LogError("Failed to restore the page visibility binding after prewarming. Page={PageName}", page.Name);
			}
			logger.LogDebug("Page content prewarmed. Page={PageName} ElapsedMs={ElapsedMs:F1}", page.Name, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
			PrewarmPageContent(pages, index + 1);
		}, (DispatcherPriority)3, Array.Empty<object>());
	}

	private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
	{
		_ = 1;
		try
		{
			if (await viewModel.WaitForUserAgreementDecisionAsync())
			{
				await viewModel.InitializeCommand.ExecuteAsync(null);
				stateSyncService.Start(() => viewModel.Settings, viewModel.SyncExternalInstanceCatalogAsync);
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(PrewarmTransientUi), (DispatcherPriority)3, Array.Empty<object>());
			}
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Failed to initialize the main window.");
		}
	}

	private async void Window_OnClosing(object? sender, CancelEventArgs e)
	{
		if (isShutdownComplete)
		{
			return;
		}

		// StartRide：开了「关闭窗口时收进托盘」的话，点 X 只隐藏窗口，从托盘菜单才能真正退出。
		// 这段必须放在 CanCloseWindow 之前：托盘常驻是用户的显式选择，不该被"有任务在跑"挡住。
		if (StartRideTryMinimizeToTray())
		{
			e.Cancel = true;
			return;
		}

		if (!viewModel.CanCloseWindow())
		{
			e.Cancel = true;
			return;
		}
		e.Cancel = true;
		if (isShutdownInProgress)
		{
			return;
		}
		isShutdownInProgress = true;
		MainWindowPlacementSnapshot snapshot = windowPlacementService.Capture(this);
		Hide();
		using CancellationTokenSource shutdownCancellation = new CancellationTokenSource(ShutdownTimeout);
		try
		{
			await windowPlacementService.SaveAsync(snapshot, shutdownCancellation.Token);
		}
		catch (OperationCanceledException) when (shutdownCancellation.IsCancellationRequested)
		{
			logger.LogWarning("Timed out saving the main window placement during launcher exit.");
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to save the main window placement during launcher exit.");
		}
		try
		{
			await shutdownService.PrepareForExitAsync(ShutdownTimeout, shutdownCancellation.Token);
		}
		catch (Exception exception2)
		{
			logger.LogWarning(exception2, "Unexpected failure while preparing the launcher to exit.");
		}
		finally
		{
			isShutdownInProgress = false;
			isShutdownComplete = true;
			Close();
		}
	}

	private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
	{
		int childCount = VisualTreeHelper.GetChildrenCount(root);
		for (int index = 0; index < childCount; index++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, index);
			T val = (T)(object)((child is T) ? child : null);
			if (val != null)
			{
				yield return val;
			}
			foreach (T item in FindVisualChildren<T>(child))
			{
				yield return item;
			}
		}
	}

	private void Window_OnPreviewDragEnter(object sender, DragEventArgs e)
	{
		CancelPendingDragStateReset();
		HandleDragPreview(e, refreshDialogState: true);
	}

	private void Window_OnPreviewDragOver(object sender, DragEventArgs e)
	{
		CancelPendingDragStateReset();
		HandleDragPreview(e, refreshDialogState: false);
	}

	private void HandleDragPreview(DragEventArgs e, bool refreshDialogState)
	{
		try
		{
			if (!HandleThirdPartyAccountDropPreview(e, refreshDialogState) && !HandleDownloadLocalImportPreview(e) && !HandleLocalImportPagePreview(e))
			{
				HandleFileDropPreview(e);
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to evaluate a drag preview over the main window.");
			ResetDragState();
			e.Effects = DragDropEffects.None;
			e.Handled = true;
		}
	}

	private void Window_OnPreviewDragLeave(object sender, DragEventArgs e)
	{
		ScheduleDragStateReset();
	}

	private void ScheduleDragStateReset()
	{
		DispatcherOperation? obj = pendingDragStateReset;
		if (obj != null)
		{
			obj.Abort();
		}
		pendingDragStateReset = ((DispatcherObject)this).Dispatcher.BeginInvoke((DispatcherPriority)4, (Delegate)new Action(ResetDragState));
	}

	private void CancelPendingDragStateReset()
	{
		DispatcherOperation? obj = pendingDragStateReset;
		if (obj != null)
		{
			obj.Abort();
		}
		pendingDragStateReset = null;
	}

	private void ResetDragState()
	{
		pendingDragStateReset = null;
		ClearThirdPartyAccountDropState();
		viewModel.DownloadPage.LocalImportDialog.ClearDropState();
		viewModel.DownloadPage.ClearLocalImportDropState();
		viewModel.GameSettingsPage.ClearImportDropState();
		floatingMessageService.ClearDragHint();
	}

	private async void Window_OnPreviewDrop(object sender, DragEventArgs e)
	{
		CancelPendingDragStateReset();
		floatingMessageService.ClearDragHint();
		try
		{
			if (!HandleThirdPartyAccountDrop(e) && !HandleDownloadLocalImportDrop(e) && !(await HandleLocalImportPageDropAsync(e)))
			{
				string[] array = TryGetDroppedPaths(e);
				if (array != null)
				{
					e.Handled = true;
					e.Effects = DragDropEffects.None;
					await viewModel.GameSettingsPage.HandleImportDropAsync(array);
				}
			}
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to handle files dropped onto the main window.");
		}
		finally
		{
			floatingMessageService.ClearDragHint();
		}
	}

	private bool HandleThirdPartyAccountDropPreview(DragEventArgs e, bool refreshDialogState)
	{
		if (refreshDialogState || cachedThirdPartyAccountDropData != e.Data)
		{
			cachedThirdPartyAccountDropData = e.Data;
			cachedThirdPartyAccountDropResult = AuthlibInjectorServerDropParser.Parse(e.Data);
			cachedThirdPartyAccountDropBlocked = cachedThirdPartyAccountDropResult.Status != AuthlibInjectorServerDropStatus.NotRecognized && IsThirdPartyAccountDropBlocked();
		}
		if (cachedThirdPartyAccountDropResult.Status == AuthlibInjectorServerDropStatus.NotRecognized)
		{
			ClearThirdPartyAccountDropHint();
			return false;
		}
		e.Handled = true;
		bool flag = cachedThirdPartyAccountDropResult.Status == AuthlibInjectorServerDropStatus.Valid && !cachedThirdPartyAccountDropBlocked;
		e.Effects = (flag ? DragDropEffects.Copy : DragDropEffects.None);
		SetThirdPartyAccountDropHint((cachedThirdPartyAccountDropResult.Status == AuthlibInjectorServerDropStatus.Invalid) ? Strings.Account_ThirdPartyDropInvalidServer : (cachedThirdPartyAccountDropBlocked ? Strings.Account_ThirdPartyDropDialogBusy : Strings.Account_ThirdPartyDropReleaseToAdd));
		return true;
	}

	private bool HandleThirdPartyAccountDrop(DragEventArgs e)
	{
		AuthlibInjectorServerDropResult authlibInjectorServerDropResult = AuthlibInjectorServerDropParser.Parse(e.Data);
		if (authlibInjectorServerDropResult.Status == AuthlibInjectorServerDropStatus.NotRecognized)
		{
			ClearThirdPartyAccountDropState();
			return false;
		}
		e.Handled = true;
		e.Effects = DragDropEffects.None;
		if (authlibInjectorServerDropResult.Status != AuthlibInjectorServerDropStatus.Valid || IsThirdPartyAccountDropBlocked())
		{
			logger.LogDebug("Discarded an authlib-injector authentication server drop that the preview stage had already rejected. Status={Status}", authlibInjectorServerDropResult.Status);
			ClearThirdPartyAccountDropState();
			return true;
		}
		string authenticationServer = authlibInjectorServerDropResult.AuthenticationServer;
		logger.LogInformation("Accepted an authlib-injector authentication server drop. AuthenticationServerHost={AuthenticationServerHost}", new Uri(authenticationServer).Host);
		e.Effects = DragDropEffects.Copy;
		ClearThirdPartyAccountDropState();
		accountDialogService.ShowThirdPartyAddAccountDialog(authenticationServer);
		return true;
	}

	private bool IsThirdPartyAccountDropBlocked()
	{
		if (viewModel.AccountPage.Dialog.IsAddAccountDialogBusy)
		{
			return true;
		}
		bool flag = CanApplyThirdPartyAccountDropToOpenDialog();
		DialogHost[] dialogHosts = GetDialogHosts();
		foreach (DialogHost dialogHost in dialogHosts)
		{
			if (dialogHost.IsOpen && (!flag || dialogHost != AddAccountDialogHost))
			{
				return true;
			}
		}
		return false;
	}

	private DialogHost[] GetDialogHosts()
	{
		DialogHost[] array = cachedDialogHosts;
		if (array != null && array.Length > 0)
		{
			return cachedDialogHosts;
		}
		cachedDialogHosts = FindVisualChildren<DialogHost>((DependencyObject)(object)this).ToArray();
		return cachedDialogHosts;
	}

	private bool CanApplyThirdPartyAccountDropToOpenDialog()
	{
		if (AddAccountDialogHost.IsOpen && viewModel.AccountPage.Dialog.IsAddAccountDialogOpen)
		{
			if (!viewModel.AccountPage.Dialog.IsAccountTypeStep)
			{
				return viewModel.AccountPage.Dialog.IsThirdPartyCredentialsStep;
			}
			return true;
		}
		return false;
	}

	private void SetThirdPartyAccountDropHint(string message)
	{
		floatingMessageService.ShowDragHint(this, message);
	}

	private void ClearThirdPartyAccountDropHint()
	{
		floatingMessageService.ClearDragHint(this);
	}

	private void ClearThirdPartyAccountDropState()
	{
		cachedThirdPartyAccountDropData = null;
		cachedThirdPartyAccountDropResult = default(AuthlibInjectorServerDropResult);
		cachedThirdPartyAccountDropBlocked = false;
		ClearThirdPartyAccountDropHint();
	}

	private void HandleFileDropPreview(DragEventArgs e)
	{
		string[] array = TryGetDroppedPaths(e);
		if (array == null)
		{
			viewModel.GameSettingsPage.ClearImportDropState();
			return;
		}
		bool flag = viewModel.GameSettingsPage.UpdateImportDropState(array);
		e.Effects = (flag ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private bool HandleLocalImportPagePreview(DragEventArgs e)
	{
		if (!IsLocalImportDropPage())
		{
			return false;
		}
		string[] array = TryGetDroppedPaths(e);
		bool flag = false;
		if (array == null)
		{
			viewModel.DownloadPage.ClearLocalImportDropState();
		}
		else
		{
			flag = viewModel.DownloadPage.UpdateLocalImportDropState(array);
		}
		e.Effects = (flag ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
		return true;
	}

	private async Task<bool> HandleLocalImportPageDropAsync(DragEventArgs e)
	{
		if (!IsLocalImportDropPage())
		{
			return false;
		}
		string[] array = TryGetDroppedPaths(e);
		e.Handled = true;
		e.Effects = DragDropEffects.None;
		try
		{
			if (array != null)
			{
				await viewModel.DownloadPage.HandleLocalImportDropAsync(array);
			}
		}
		finally
		{
			viewModel.DownloadPage.ClearLocalImportDropState();
		}
		return true;
	}

	private bool HandleDownloadLocalImportPreview(DragEventArgs e)
	{
		if (!viewModel.DownloadPage.LocalImportDialog.IsOpen)
		{
			return false;
		}
		string[] array = TryGetDroppedPaths(e);
		if (array == null)
		{
			viewModel.DownloadPage.LocalImportDialog.ClearDropState();
			e.Effects = DragDropEffects.None;
			e.Handled = true;
			return true;
		}
		bool flag = viewModel.DownloadPage.LocalImportDialog.PreviewDroppedFiles(array);
		e.Effects = (flag ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
		return true;
	}

	private bool HandleDownloadLocalImportDrop(DragEventArgs e)
	{
		if (!viewModel.DownloadPage.LocalImportDialog.IsOpen)
		{
			return false;
		}
		string[] array = TryGetDroppedPaths(e);
		if (array != null)
		{
			viewModel.DownloadPage.LocalImportDialog.ApplyDroppedFiles(array);
		}
		else
		{
			viewModel.DownloadPage.LocalImportDialog.ClearDropState();
		}
		e.Handled = true;
		e.Effects = DragDropEffects.None;
		return true;
	}

	private static string[]? TryGetDroppedPaths(DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			return null;
		}
		return e.Data.GetData(DataFormats.FileDrop) as string[];
	}

	private bool IsLocalImportDropPage()
	{
		return NavigationCatalog.UsesLocalModpackDrop(viewModel.CurrentPage, viewModel.GameSettingsPage.IsListStep);
	}


	static MainWindow()
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		ShutdownTimeout = TimeSpan.FromSeconds(5.0);
		IsMenuExpandedProperty = DependencyProperty.Register("IsMenuExpanded", typeof(bool), typeof(MainWindow), new PropertyMetadata((object)false));
	}
}
