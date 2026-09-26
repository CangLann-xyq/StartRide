using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
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
		base.Loaded += MainWindow_Loaded;
		// StartRide：托盘图标 + 启动自检（见 MainWindow.StartRide.Tray.cs）
		base.Loaded += StartRideStartup_OnLoaded;
		base.Closing += Window_OnClosing;
		base.Closed += delegate
		{
			stateSyncService.Stop();
		};
	}

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
			base.WindowState = WindowState.Maximized;
			isFullscreen = true;
		}
		else
		{
			base.WindowState = WindowState.Normal;
			base.WindowStyle = preFullscreenWindowStyle;
			base.ResizeMode = preFullscreenResizeMode;
			base.WindowState = preFullscreenState;
			isFullscreen = false;
		}
		ApplyFullscreenChrome();
		// WindowStyle 变化会让 WindowChrome 的圆角/边框失效，DWM 属性也可能被重置，重新下发一次。
		LauncherWindowBackdrop.Reapply(this, themeService);
		UpdateCaptionButtons();
	}

	private void ApplyFullscreenChrome()
	{
		WindowChrome windowChrome = WindowChrome.GetWindowChrome(this);
		if (windowChrome != null)
		{
			windowChrome.CornerRadius = (isFullscreen ? new CornerRadius(0.0) : new CornerRadius(12.0));
			windowChrome.ResizeBorderThickness = (isFullscreen ? new Thickness(0.0) : new Thickness(8.0));
		}
		if (WindowRootBorder != null)
		{
			WindowRootBorder.CornerRadius = (isFullscreen ? new CornerRadius(0.0) : new CornerRadius(12.0));
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
