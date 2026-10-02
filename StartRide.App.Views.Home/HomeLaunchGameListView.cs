using System;
using System.CodeDom.Compiler;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StartRide.App.Behaviors;
using StartRide.App.Controls;
using StartRide.App.Utilities;
using StartRide.App.ViewModels.Home;

namespace StartRide.App.Views.Home;

public partial class HomeLaunchGameListView : UserControl, IComponentConnector
{
	public static readonly DependencyProperty SuppressSelectedItemBackgroundProperty;

	public static readonly DependencyProperty IsProgressiveBlurEnabledProperty;

	private const double FallbackPanelWidth = 224.0;

	private const double MenuPanelCornerRadius = 14.0;

	private const double MenuShadowSideInset = 28.0;

	private const double MenuPanelBottomOverhang = 2.0;

	private const double FallbackCollapsedHeight = 72.0;

	private const double FallbackItemHeight = 54.0;

	// 头部高度取自 SecondaryMenuHeaderOverlayStyle(Height=76)；
	// 列表上下留白取自 SecondaryMenuVirtualizedListBoxStyle 的 ItemsPanel Margin="14,74,14,18"。
	private const double FallbackHeaderHeight = 76.0;

	private const double FallbackListTopSpacer = 74.0;

	private const double FallbackListBottomSpacer = 18.0;

	private const double FallbackAnimationDurationMilliseconds = 380.0;

	private const double FallbackAnimationEasePower = 3.2;

	private const double FallbackCollapseDelayMilliseconds = 110.0;

	private static readonly Thickness FallbackPanelMargin;

	private HomeLaunchGameListViewModel? attachedViewModel;

	private bool isApplyQueued;

	private bool isPointerExpanded;

	private bool pendingAnimate;

	private int animationGeneration;

	private int measureRetryCount;

	private bool? appliedExpandedState;

	private bool isProgressiveBlurActive;

	private readonly ProgressiveBlurBandController? progressiveBlurController;

	private DispatcherTimer? collapseDelayTimer;

	internal FrameworkElement FloatingLayerElement => HomeLaunchFloatingLayer;

	internal FrameworkElement MenuPanelShadowElement => HomeLaunchMenuPanelShadow;

	internal FrameworkElement HeaderOverlayElement => HomeLaunchHeaderOverlay;

	internal FrameworkElement EmptyStateTextElement => HomeLaunchEmptyStateText;

	internal ToggleButton PinButtonElement => HomeLaunchMenuPinButton;

	internal FrameworkElement MenuViewportElement => HomeLaunchMenuViewport;

	internal ListBox LaunchInstanceListBox => HomeLaunchInstanceListBox;

	internal TranslateTransform ListTranslateTransform => HomeLaunchListTranslate;

	internal TranslateTransform EmptyStateTranslateTransform => HomeLaunchEmptyStateTranslate;

	internal bool IsMenuExpanded => ShouldUseExpandedState();

	internal bool IsSelectedItemBackgroundSuppressed => SuppressSelectedItemBackground;

	internal double CollapsedMenuHeight => GetResourceDouble("HomeLaunchMenuCollapsedHeight", 72.0);

	public bool SuppressSelectedItemBackground
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(SuppressSelectedItemBackgroundProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SuppressSelectedItemBackgroundProperty, (object)value);
		}
	}

	public bool IsProgressiveBlurEnabled
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsProgressiveBlurEnabledProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsProgressiveBlurEnabledProperty, (object)value);
		}
	}

	public HomeLaunchGameListView()
	{

		InitializeComponent();
		progressiveBlurController = new ProgressiveBlurBandController(new ProgressiveBlurVisualParts(this, HomeLaunchProgressiveBlurLayer, HomeLaunchProgressiveBlurVisualSource, HomeLaunchProgressiveBlurDirectHost, HomeLaunchProgressiveBlurViewport, HomeLaunchProgressiveBlurUpscaleHost, HomeLaunchProgressiveBlurUpscaleTransform, HomeLaunchProgressiveBlurHorizontalHost, HomeLaunchProgressiveBlurVerticalHost, HomeLaunchProgressiveBlurBrush), () => base.IsVisible && isProgressiveBlurActive);
		SetResourceReference(IsProgressiveBlurEnabledProperty, "Is.ProgressiveBlur.Enabled");
		base.Loaded += OnLoaded;
		base.Unloaded += OnUnloaded;
		base.DataContextChanged += new DependencyPropertyChangedEventHandler(OnDataContextChanged);
		base.SizeChanged += delegate
		{
			QueueApplyMenuState(base.IsLoaded, (DispatcherPriority)6);
		};
		AttachHoverInput();
	}

	internal void SetPointerExpandedForTest(bool value)
	{
		DispatcherTimer? obj = collapseDelayTimer;
		if (obj != null)
		{
			obj.Stop();
		}
		if (isPointerExpanded != value)
		{
			isPointerExpanded = value;
			QueueApplyMenuState(base.IsLoaded, (DispatcherPriority)6);
		}
	}

	private TimeSpan GetCollapseDelay()
	{
		return TimeSpan.FromMilliseconds(Math.Max(0.0, GetResourceDouble("HomeLaunchMenuCollapseDelayMilliseconds", 110.0)));
	}

	private void ScheduleDelayedCollapse()
	{

		if (collapseDelayTimer == null)
		{
			collapseDelayTimer = new DispatcherTimer((DispatcherPriority)5, ((DispatcherObject)this).Dispatcher);
		}
		collapseDelayTimer.Interval = GetCollapseDelay();
		collapseDelayTimer.Tick -= CollapseDelayTimer_Tick;
		collapseDelayTimer.Tick += CollapseDelayTimer_Tick;
		collapseDelayTimer.Start();
	}

	private void CollapseDelayTimer_Tick(object? sender, EventArgs e)
	{
		DispatcherTimer? obj = collapseDelayTimer;
		if (obj != null)
		{
			obj.Stop();
		}
		if (!isPointerExpanded)
		{
			return;
		}
		if (IsPointerOverMenu())
		{
			DispatcherTimer? obj2 = collapseDelayTimer;
			if (obj2 != null)
			{
				obj2.Start();
			}
		}
		else
		{
			isPointerExpanded = false;
			QueueApplyMenuState(base.IsLoaded, (DispatcherPriority)6);
		}
	}

	private bool IsPointerOverMenu()
	{

		if (HomeLaunchMenuPanelShadow.IsMouseOver)
		{
			return true;
		}
		if (base.IsLoaded && !(HomeLaunchMenuClipHost.ActualWidth <= 0.0))
		{
			Window window = Window.GetWindow((DependencyObject)(object)this);
			if (window != null && window.IsMouseOver)
			{
				Point position = Mouse.GetPosition(HomeLaunchMenuClipHost);
				if (position.X >= 0.0 && position.X <= HomeLaunchMenuClipHost.ActualWidth && position.Y >= HomeLaunchMenuPanelTranslate.Y)
				{
					return position.Y <= HomeLaunchMenuClipHost.ActualHeight;
				}
				return false;
			}
		}
		return false;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		AttachViewModel(base.DataContext as HomeLaunchGameListViewModel);
		HomeLaunchMenuClipHost.Width = GetResourceDouble("HomeLaunchMenuPanelWidth", 224.0);
		HomeLaunchMenuShadowHost.Width = HomeLaunchMenuClipHost.Width;
		HomeLaunchMenuShadowHost.Margin = GetPanelMargin();
		HomeLaunchMenuClipHost.Height = GetCollapsedHeight();
		HomeLaunchMenuClipHost.Margin = GetPanelMargin();
		progressiveBlurController?.OnLoaded();
		QueueApplyMenuState(animate: false, (DispatcherPriority)6);
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		isProgressiveBlurActive = false;
		VerticalEdgeOpacityMask.SetIsEnabled((DependencyObject)(object)HomeLaunchProgressiveBlurLayer, value: false);
		progressiveBlurController?.OnUnloaded();
		DetachViewModel(attachedViewModel);
		DispatcherTimer? obj = collapseDelayTimer;
		if (obj != null)
		{
			obj.Stop();
		}
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		DetachViewModel(e.OldValue as HomeLaunchGameListViewModel);
		AttachViewModel(e.NewValue as HomeLaunchGameListViewModel);
		QueueApplyMenuState(base.IsLoaded, (DispatcherPriority)6);
	}

	private void AttachViewModel(HomeLaunchGameListViewModel? viewModel)
	{
		if (viewModel != null && attachedViewModel != viewModel)
		{
			attachedViewModel = viewModel;
			viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
			viewModel.LaunchInstances.CollectionChanged += LaunchInstances_OnCollectionChanged;
		}
	}

	private void DetachViewModel(HomeLaunchGameListViewModel? viewModel)
	{
		if (viewModel != null && attachedViewModel == viewModel)
		{
			viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
			viewModel.LaunchInstances.CollectionChanged -= LaunchInstances_OnCollectionChanged;
			attachedViewModel = null;
		}
	}

	private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		bool flag;
		switch (e.PropertyName)
		{
		case "SelectedLaunchInstanceItem":
		case "HasSelectedLaunchInstance":
		case "HasLaunchInstances":
		case "HasNoLaunchInstances":
		case "IsLaunchMenuPinned":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			measureRetryCount = 0;
			QueueApplyMenuState(base.IsLoaded, (DispatcherPriority)6);
		}
	}

	private void LaunchInstances_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		measureRetryCount = 0;
		QueueApplyMenuState(base.IsLoaded, (DispatcherPriority)6);
	}

	private void SetPointerExpanded(bool expanded)
	{
		DispatcherTimer? obj = collapseDelayTimer;
		if (obj != null)
		{
			obj.Stop();
		}
		if (isPointerExpanded != expanded)
		{
			if (!expanded && base.IsLoaded && GetCollapseDelay() > TimeSpan.Zero)
			{
				ScheduleDelayedCollapse();
				return;
			}
			isPointerExpanded = expanded;
			QueueApplyMenuState(base.IsLoaded, (DispatcherPriority)6);
		}
	}

	private void AttachHoverInput()
	{
		HomeLaunchMenuPanelShadow.MouseEnter += HoverInputElement_OnMouseEnter;
		HomeLaunchMenuPanelShadow.MouseLeave += HoverInputElement_OnMouseLeave;
	}

	private void HoverInputElement_OnMouseEnter(object sender, MouseEventArgs e)
	{
		SetPointerExpanded(expanded: true);
	}

	private void HoverInputElement_OnMouseLeave(object sender, MouseEventArgs e)
	{
		SetPointerExpanded(expanded: false);
	}

	private void QueueApplyMenuState(bool animate, DispatcherPriority priority = (DispatcherPriority)6)
	{

		pendingAnimate |= animate;
		if (isApplyQueued || !((DispatcherObject)this).Dispatcher.CheckAccess())
		{
			if (!((DispatcherObject)this).Dispatcher.CheckAccess())
			{
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					QueueApplyMenuState(animate, priority);
				}, priority, Array.Empty<object>());
			}
			return;
		}
		isApplyQueued = true;
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			isApplyQueued = false;
			bool animate2 = pendingAnimate;
			pendingAnimate = false;
			ApplyMenuState(animate2);
		}, priority, Array.Empty<object>());
	}

	private void ApplyMenuState(bool animate)
	{
		double expandedHeight = GetExpandedHeight();
		HomeLaunchMenuViewport.Height = expandedHeight;
		UpdateMenuClipHost(expandedHeight);
		bool flag = ShouldUseExpandedState();
		bool wasVisible = false;
		SuppressSelectedItemBackground = !flag;
		HomeLaunchHeaderOverlay.IsHitTestVisible = flag;
		bool flag2 = flag & animate;
		UpdateProgressiveBlurState(flag && !flag2);
		bool num = !flag && attachedViewModel?.SelectedLaunchInstanceItem != null;
		bool flag3 = false;
		if (num)
		{
			flag3 = PrepareSelectedItemForMeasurement(out wasVisible);
		}
		if (num && !flag3)
		{
			if (measureRetryCount++ < 4)
			{
				QueueApplyMenuState(animate, (DispatcherPriority)2);
				return;
			}
		}
		else
		{
			measureRetryCount = 0;
		}
		if ((!flag & animate) && appliedExpandedState == true && !wasVisible)
		{
			NormalizeSelectedItemCollapseStart();
		}
		appliedExpandedState = flag;
		int generation = ++animationGeneration;
		double num2 = (flag ? expandedHeight : GetCollapsedHeight());
		double to = Math.Max(0.0, expandedHeight - num2);
		double to2 = (flag ? 0.0 : CalculateCollapsedListTranslate());
		double to3 = CalculateEmptyStateTranslate(flag, expandedHeight);
		int num3 = (flag ? 1 : 0);
		bool num4 = AnimateDouble((DependencyObject)(object)HomeLaunchMenuPanelTranslate, TranslateTransform.YProperty, to, animate, generation, OnMenuHeightAnimationCompleted);
		AnimateDouble((DependencyObject)(object)HomeLaunchListTranslate, TranslateTransform.YProperty, to2, animate, generation);
		AnimateDouble((DependencyObject)(object)HomeLaunchEmptyStateTranslate, TranslateTransform.YProperty, to3, animate, generation);
		AnimateDouble((DependencyObject)(object)HomeLaunchHeaderOverlay, UIElement.OpacityProperty, num3, animate, generation);
		AnimateDouble((DependencyObject)(object)HomeLaunchMenuShadowTopTranslate, TranslateTransform.YProperty, to, animate, generation);
		double to4 = CalculateShadowSideScale(num2, expandedHeight);
		AnimateDouble((DependencyObject)(object)HomeLaunchMenuShadowLeftScale, ScaleTransform.ScaleYProperty, to4, animate, generation);
		AnimateDouble((DependencyObject)(object)HomeLaunchMenuShadowRightScale, ScaleTransform.ScaleYProperty, to4, animate, generation);
		if (!num4)
		{
			OnMenuHeightAnimationCompleted();
		}
	}

	private static double CalculateShadowSideScale(double targetHeight, double expandedHeight)
	{
		double num = expandedHeight - 28.0;
		if (num <= 0.0)
		{
			return 0.0;
		}
		return Math.Clamp((targetHeight - 28.0) / num, 0.0, 1.0);
	}

	private void UpdateMenuClipHost(double expandedHeight)
	{
		double resourceDouble = GetResourceDouble("HomeLaunchMenuPanelWidth", 224.0);
		if (HomeLaunchMenuClipHost.Clip == null || !(Math.Abs(HomeLaunchMenuClipHost.Width - resourceDouble) < 0.1) || !(Math.Abs(HomeLaunchMenuClipHost.Height - expandedHeight) < 0.1))
		{
			HomeLaunchMenuClipHost.Width = resourceDouble;
			HomeLaunchMenuClipHost.Height = expandedHeight;
			HomeLaunchMenuClipHost.Margin = GetPanelMargin();
			HomeLaunchMenuShadowHost.Width = resourceDouble;
			HomeLaunchMenuShadowHost.Height = expandedHeight;
			HomeLaunchMenuShadowHost.Margin = GetPanelMargin();
			HomeLaunchMenuPanelShadow.Height = expandedHeight + 2.0;
			HomeLaunchMenuBottomEdge.Width = resourceDouble;
			HomeLaunchMenuBottomEdge.Margin = GetPanelMargin();
			RectangleGeometry rectangleGeometry = new RectangleGeometry(new Rect(0.0, 0.0, resourceDouble, expandedHeight), 14.0, 14.0);
			((Freezable)rectangleGeometry).Freeze();
			HomeLaunchMenuClipHost.Clip = rectangleGeometry;
		}
	}

	private void OnMenuHeightAnimationCompleted()
	{
		UpdateProgressiveBlurState(ShouldUseExpandedState());
	}

	private static void OnProgressiveBlurEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is HomeLaunchGameListView homeLaunchGameListView)
		{
			bool num = !(bool)e.OldValue && (bool)e.NewValue;
			homeLaunchGameListView.UpdateProgressiveBlurState(homeLaunchGameListView.ShouldUseExpandedState());
			if (num)
			{
				homeLaunchGameListView.progressiveBlurController?.OnEnabledChanged(becameEnabled: true);
			}
		}
	}

	private void UpdateProgressiveBlurState(bool shouldExpand)
	{
		isProgressiveBlurActive = (IsProgressiveBlurEnabled & shouldExpand) && (attachedViewModel?.HasLaunchInstances ?? false);
		VerticalEdgeOpacityMask.SetIsEnabled((DependencyObject)(object)HomeLaunchProgressiveBlurLayer, isProgressiveBlurActive);
		progressiveBlurController?.Update();
	}

	private bool ShouldUseExpandedState()
	{
		if (!CanUseCollapsedState())
		{
			return true;
		}
		HomeLaunchGameListViewModel? homeLaunchGameListViewModel = attachedViewModel;
		if (homeLaunchGameListViewModel != null && homeLaunchGameListViewModel.IsLaunchMenuPinned)
		{
			return true;
		}
		if (isPointerExpanded)
		{
			return attachedViewModel?.HasLaunchInstances ?? false;
		}
		return false;
	}

	private bool CanUseCollapsedState()
	{
		if (attachedViewModel?.SelectedLaunchInstanceItem == null)
		{
			return attachedViewModel?.HasNoLaunchInstances ?? false;
		}
		return true;
	}

	private bool PrepareSelectedItemForMeasurement(out bool wasVisible)
	{
		wasVisible = false;
		HomeLaunchInstanceItem homeLaunchInstanceItem = attachedViewModel?.SelectedLaunchInstanceItem;
		if (homeLaunchInstanceItem == null)
		{
			return false;
		}
		HomeLaunchInstanceListBox.ApplyTemplate();
		HomeLaunchInstanceListBox.UpdateLayout();
		SmoothScrollBehavior.CancelAnimationFromDescendant((DependencyObject)(object)HomeLaunchInstanceListBox);
		wasVisible = IsWithinScrollViewport(GetSelectedItemContainer(homeLaunchInstanceItem));
		if (!wasVisible)
		{
			HomeLaunchInstanceListBox.ScrollIntoView(homeLaunchInstanceItem);
			HomeLaunchInstanceListBox.UpdateLayout();
		}
		FrameworkElement selectedItemContainer = GetSelectedItemContainer(homeLaunchInstanceItem);
		if (selectedItemContainer != null)
		{
			return selectedItemContainer.ActualHeight > 0.0;
		}
		return false;
	}

	private bool IsWithinScrollViewport(FrameworkElement? container)
	{

		if (container != null && !(container.ActualHeight <= 0.0))
		{
			ScrollViewer scrollViewer = VisualTreeSearch.FindDescendant((DependencyObject)(object)HomeLaunchInstanceListBox, (ScrollViewer _) => true);
			if (scrollViewer != null && scrollViewer.ActualHeight > 0.0)
			{
				try
				{
					Point val = container.TransformToAncestor(scrollViewer).Transform(new Point(0.0, 0.0));
					double y = val.Y;
					return y + container.ActualHeight > 0.0 && y < scrollViewer.ActualHeight;
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			}
		}
		return false;
	}

	private void NormalizeSelectedItemCollapseStart()
	{

		HomeLaunchInstanceItem homeLaunchInstanceItem = attachedViewModel?.SelectedLaunchInstanceItem;
		FrameworkElement frameworkElement = ((homeLaunchInstanceItem == null) ? null : GetSelectedItemContainer(homeLaunchInstanceItem));
		if (frameworkElement == null)
		{
			return;
		}
		try
		{
			Point val = frameworkElement.TransformToAncestor(HomeLaunchMenuPanel).Transform(new Point(0.0, 0.0));
			double y = val.Y;
			double height = HomeLaunchHeaderOverlay.Height;
			double num = ((HomeLaunchHeaderOverlay.ActualHeight > 0.0) ? HomeLaunchHeaderOverlay.ActualHeight : (double.IsNaN(height) ? 0.0 : Math.Max(0.0, height)));
			double num2 = HomeLaunchMenuPanel.BorderThickness.Top + num;
			double y2 = HomeLaunchListTranslate.Y + num2 - y;
			HomeLaunchListTranslate.BeginAnimation(TranslateTransform.YProperty, null);
			HomeLaunchListTranslate.Y = y2;
		}
		catch (InvalidOperationException)
		{
		}
	}

	private double CalculateCollapsedListTranslate()
	{

		HomeLaunchInstanceItem homeLaunchInstanceItem = attachedViewModel?.SelectedLaunchInstanceItem;
		FrameworkElement frameworkElement = ((homeLaunchInstanceItem == null) ? null : GetSelectedItemContainer(homeLaunchInstanceItem));
		if (frameworkElement == null)
		{
			return 0.0;
		}
		try
		{
			Point val = frameworkElement.TransformToAncestor(HomeLaunchMenuPanel).Transform(new Point(0.0, 0.0));
			double num = val.Y - HomeLaunchListTranslate.Y;
			double num2 = ((frameworkElement.ActualHeight > 0.0) ? frameworkElement.ActualHeight : GetItemHeight());
			return Math.Max(0.0, (GetCollapsedHeight() - num2) / 2.0) - num;
		}
		catch (InvalidOperationException)
		{
			return 0.0;
		}
	}

	private double CalculateEmptyStateTranslate(bool shouldExpand, double expandedHeight)
	{
		double emptyStateTextHeight = GetEmptyStateTextHeight();
		double num = (shouldExpand ? expandedHeight : GetCollapsedHeight());
		return Math.Max(0.0, (num - emptyStateTextHeight) / 2.0);
	}

	private double GetEmptyStateTextHeight()
	{

		if (HomeLaunchEmptyStateText.ActualHeight > 0.0)
		{
			return HomeLaunchEmptyStateText.ActualHeight;
		}
		double num = Math.Max(0.0, GetResourceDouble("HomeLaunchMenuPanelWidth", 224.0) - HomeLaunchEmptyStateText.Margin.Left - HomeLaunchEmptyStateText.Margin.Right);
		HomeLaunchEmptyStateText.Measure(new Size(num, double.PositiveInfinity));
		Size desiredSize = HomeLaunchEmptyStateText.DesiredSize;
		return desiredSize.Height;
	}

	private FrameworkElement? GetSelectedItemContainer(HomeLaunchInstanceItem selectedItem)
	{
		return HomeLaunchInstanceListBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as FrameworkElement;
	}

	private bool AnimateDouble(DependencyObject target, DependencyProperty property, double to, bool animate, int generation, Action? onCompleted = null)
	{
		IAnimatable animatable = target as IAnimatable;
		if (animatable == null)
		{
			target.SetValue(property, (object)to);
			return false;
		}
		double currentDouble = GetCurrentDouble(target, property);
		animatable.BeginAnimation(property, null);
		target.SetValue(property, (object)currentDouble);
		if (!animate || Math.Abs(currentDouble - to) < 0.1)
		{
			target.SetValue(property, (object)to);
			return false;
		}
		DoubleAnimation doubleAnimation = new DoubleAnimation
		{
			From = currentDouble,
			To = to,
			Duration = GetAnimationDuration(),
			FillBehavior = FillBehavior.Stop,
			EasingFunction = CreateAnimationEasing()
		};
		doubleAnimation.Completed += delegate
		{
			if (generation == animationGeneration)
			{
				animatable.BeginAnimation(property, null);
				target.SetValue(property, (object)to);
				onCompleted?.Invoke();
			}
		};
		animatable.BeginAnimation(property, doubleAnimation, HandoffBehavior.SnapshotAndReplace);
		return true;
	}

	private static double GetCurrentDouble(DependencyObject target, DependencyProperty property)
	{
		double num = (double)target.GetValue(property);
		if (!double.IsNaN(num))
		{
			return num;
		}
		if (!(target is FrameworkElement frameworkElement))
		{
			return 0.0;
		}
		return frameworkElement.ActualHeight;
	}

	private double GetExpandedHeight()
	{
		// 只按内容撑高：头部 + n 个条目 + 底部留白。
		// 早先直接返回浮动层整高（= 主页可用高度），那是 MC 启动器"版本列表"侧栏的做法；
		// StartRide 只有 1~2 个 BeamNG 安装，展开后会拖出一整块空白面板。
		Thickness panelMargin = GetPanelMargin();
		double availableHeight = ((HomeLaunchFloatingLayer.ActualHeight > 0.0) ? HomeLaunchFloatingLayer.ActualHeight : base.ActualHeight) - panelMargin.Top - panelMargin.Bottom;
		double collapsedHeight = GetCollapsedHeight();
		if (availableHeight <= collapsedHeight)
		{
			return collapsedHeight;
		}
		int launchInstanceCount = attachedViewModel?.LaunchInstances?.Count ?? 0;
		double contentHeight;
		if (launchInstanceCount > 0)
		{
			contentHeight = FallbackListTopSpacer + (launchInstanceCount * GetItemHeight()) + FallbackListBottomSpacer;
		}
		else
		{
			// 空列表：空状态文字在整块面板里居中，所以高度要留成"头部两倍 + 文字高"，否则会被头部遮住。
			contentHeight = (2.0 * FallbackHeaderHeight) + GetEmptyStateTextHeight();
		}
		return Math.Clamp(contentHeight, collapsedHeight, availableHeight);
	}

	private double GetCollapsedHeight()
	{
		return GetResourceDouble("HomeLaunchMenuCollapsedHeight", 72.0);
	}

	private double GetItemHeight()
	{
		return GetResourceDouble("HomeLaunchMenuItemHeight", 54.0);
	}

	private Duration GetAnimationDuration()
	{
		return new Duration(TimeSpan.FromMilliseconds(GetResourceDouble("HomeLaunchMenuAnimationDurationMilliseconds", 380.0)));
	}

	private IEasingFunction CreateAnimationEasing()
	{
		return new PowerEase
		{
			Power = GetResourceDouble("HomeLaunchMenuAnimationEasePower", 3.2),
			EasingMode = EasingMode.EaseOut
		};
	}

	private Thickness GetPanelMargin()
	{
		object obj = TryFindResource("HomeLaunchMenuPanelMargin");
		if (obj is Thickness)
		{
			return (Thickness)obj;
		}
		return FallbackPanelMargin;
	}

	private double GetResourceDouble(string key, double fallback)
	{
		object obj = TryFindResource(key);
		if (obj is double)
		{
			return (double)obj;
		}
		return fallback;
	}


	static HomeLaunchGameListView()
	{

		SuppressSelectedItemBackgroundProperty = DependencyProperty.Register("SuppressSelectedItemBackground", typeof(bool), typeof(HomeLaunchGameListView), new PropertyMetadata((object)false));
		IsProgressiveBlurEnabledProperty = DependencyProperty.Register("IsProgressiveBlurEnabled", typeof(bool), typeof(HomeLaunchGameListView), new PropertyMetadata((object)false, new PropertyChangedCallback(OnProgressiveBlurEnabledChanged)));
		FallbackPanelMargin = new Thickness(24.0, 24.0, 0.0, 24.0);
	}
}
