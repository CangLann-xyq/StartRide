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

	private FrameworkElement? lastResolvedPageRoot;

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
		lastResolvedPageRoot = ResolvePageRoot(viewModel.CurrentPage);
		AttachReplaysViewModeBridge();
		LauncherWindowBackdrop.Attach(this, themeService);
		NativeCaptionButtons.Hide(this);
		base.SourceInitialized += MainWindow_OnSourceInitialized;
		base.Loaded += MainWindow_Loaded;
		base.Loaded += StartRideStartup_OnLoaded;
		base.Closing += Window_OnClosing;
		base.Closed += delegate
		{
			stateSyncService.Stop();
		};
	}

	private void MainWindow_OnSourceInitialized(object? sender, EventArgs e)
	{
		try
		{
			IntPtr handle = new WindowInteropHelper(this).Handle;
			HwndSource? source = handle == IntPtr.Zero ? null : HwndSource.FromHwnd(handle);
			source?.AddHook(WindowMessageHook);

			LauncherWindowBackdrop.Reapply(this, themeService);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to hook the window message loop.");
		}
	}

	private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (message == WmGetMinMaxInfo)
		{

			ClampMaximizedSize(hwnd, lParam, isFullscreen);
		}
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

			value.maxPosition.x = target.left - info.monitor.left;
			value.maxPosition.y = target.top - info.monitor.top;
			value.maxSize.x = target.right - target.left;
			value.maxSize.y = target.bottom - target.top;
			Marshal.StructureToPtr(value, lParam, false);
		}
		catch (Exception)
		{
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
				FrameworkElement? pageRoot = ResolvePageRoot(mainViewModel.CurrentPage);
				bool sameHost = pageRoot != null && ReferenceEquals(pageRoot, lastResolvedPageRoot);
				lastResolvedPageRoot = pageRoot;
				if (sameHost)
				{
					pageTransitionService.SyncTo(mainViewModel.CurrentPage);
				}
				else
				{
					pageTransitionService.MoveTo(mainViewModel.CurrentPage);
				}
				SyncReplaysViewMode(mainViewModel.CurrentPage);
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

	private void ToggleFullscreen()
	{
		if (!isFullscreen)
		{
			preFullscreenState = base.WindowState;
			preFullscreenResizeMode = base.ResizeMode;
			preFullscreenWindowStyle = base.WindowStyle;
			base.WindowState = WindowState.Normal;
			base.WindowStyle = WindowStyle.None;
			base.ResizeMode = ResizeMode.NoResize;

			isFullscreen = true;
			base.WindowState = WindowState.Maximized;
		}
		else
		{
			isFullscreen = false;
			base.WindowState = WindowState.Normal;
			base.WindowStyle = preFullscreenWindowStyle;
			base.ResizeMode = preFullscreenResizeMode;

			NativeCaptionButtons.Reapply(this);
			base.WindowState = preFullscreenState;
		}
		ApplyFullscreenChrome();
		LauncherWindowBackdrop.Reapply(this, themeService);
		UpdateCaptionButtons();
	}

	private void ApplyFullscreenChrome()
	{

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

	private void Window_OnStateChanged(object? sender, EventArgs e)
	{
		ApplyFullscreenChrome();

		LauncherWindowBackdrop.Reapply(this, themeService);
		UpdateCaptionButtons();
	}

	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
	{
		base.OnDpiChanged(oldDpi, newDpi);
		try
		{
			LauncherWindowBackdrop.Reapply(this, themeService);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Reapplying the window backdrop after a DPI change failed.");
		}
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

	private void AttachReplaysViewModeBridge()
	{
		try
		{
			if (ReplaysPageView?.DataContext is not StartRide.App.ViewModels.Replays.ReplaysPageViewModel replaysViewModel)
			{
				return;
			}
			replaysViewModel.ViewModeChanged += OnReplaysViewModeChanged;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to attach the replays view-mode bridge.");
		}
	}

	private void OnReplaysViewModeChanged(int mode)
	{
		string page = ((mode == 1) ? NavigationCatalog.HighlightsPage : NavigationCatalog.InstallPage);
		if (NavigationCatalog.IsPage(viewModel.CurrentPage, page))
		{
			return;
		}
		foreach (NavigationItem item in viewModel.NavigationItems)
		{
			if (NavigationCatalog.IsPage(item.Page, page))
			{
				viewModel.SelectNavigationItem(item);
				return;
			}
		}
	}

	private void SyncReplaysViewMode(string? page)
	{
		try
		{
			if (ReplaysPageView?.DataContext is not StartRide.App.ViewModels.Replays.ReplaysPageViewModel replaysViewModel)
			{
				return;
			}
			if (NavigationCatalog.IsPage(page, NavigationCatalog.HighlightsPage))
			{
				replaysViewModel.ApplyShellViewMode(1);
			}
			else if (NavigationCatalog.IsPage(page, NavigationCatalog.InstallPage))
			{
				replaysViewModel.ApplyShellViewMode(0);
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to sync the replays view mode.");
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
		if (string.Equals(page, "Install", StringComparison.OrdinalIgnoreCase) || string.Equals(page, "Highlights", StringComparison.OrdinalIgnoreCase))
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
			LauncherWindowBackdrop.Reapply(this, themeService);
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Reapplying the window backdrop on load failed.");
		}
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

		ShutdownTimeout = TimeSpan.FromSeconds(5.0);
		IsMenuExpandedProperty = DependencyProperty.Register("IsMenuExpanded", typeof(bool), typeof(MainWindow), new PropertyMetadata((object)false));
	}
}
