using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StartRide.App.Behaviors;

namespace StartRide.App.Controls;

public class AnimatedComboBox : ComboBox
{
	private struct CursorPoint
	{
		public int X;

		public int Y;
	}

	private sealed class PopupWheelIsolationState
	{
		public WeakReference<AnimatedComboBox>? ActiveComboBox { get; set; }
	}

	private const double PopupGap = 6.0;

	private const double PopupShadowPadding = 14.0;

	private const double DefaultDropDownItemHeightEstimate = 38.0;

	private const double PopupVerticalPaddingEstimate = 10.0;

	private static readonly Duration OpenDuration;

	private static readonly Duration CloseDuration;

	private static readonly IEasingFunction OpenEasing;

	private static readonly IEasingFunction CloseEasing;

	private static readonly ConditionalWeakTable<Dispatcher, PopupWheelIsolationState> PopupWheelIsolationStates;

	public static readonly DependencyProperty IsPopupOpenProperty;

	public static readonly DependencyProperty IsDropDownClosingProperty;

	public static readonly DependencyProperty DropDownItemContainerStyleProperty;

	public static readonly DependencyProperty SelectionItemTemplateProperty;

	public static readonly DependencyProperty SelectionItemTemplateSelectorProperty;

	private readonly DependencyPropertyDescriptor dropDownDescriptor;

	private DispatcherTimer? closeTimer;

	private Popup? popup;

	private ListBox? popupListBox;

	private FrameworkElement? popupSurface;

	private TextBlock? selectionTextBlock;

	private ContentPresenter? selectionContentPresenter;

	private ScaleTransform? scaleTransform;

	private TranslateTransform? translateTransform;

	private InputManager? popupInputManager;

	private bool opensAbove;

	private bool isDropDownDescriptorAttached;

	private long popupOpenGeneration;

	private long popupOpenAnimationStartedGeneration = -1L;

	public bool IsPopupOpen
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsPopupOpenProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsPopupOpenProperty, (object)value);
		}
	}

	public bool IsDropDownClosing
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsDropDownClosingProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsDropDownClosingProperty, (object)value);
		}
	}

	public Style? DropDownItemContainerStyle
	{
		get
		{
			return (Style)((DependencyObject)this).GetValue(DropDownItemContainerStyleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(DropDownItemContainerStyleProperty, (object)value);
		}
	}

	public DataTemplate? SelectionItemTemplate
	{
		get
		{
			return (DataTemplate)((DependencyObject)this).GetValue(SelectionItemTemplateProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SelectionItemTemplateProperty, (object)value);
		}
	}

	public DataTemplateSelector? SelectionItemTemplateSelector
	{
		get
		{
			return (DataTemplateSelector)((DependencyObject)this).GetValue(SelectionItemTemplateSelectorProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SelectionItemTemplateSelectorProperty, (object)value);
		}
	}

	static AnimatedComboBox()
	{
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected O, but got Unknown
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Expected O, but got Unknown
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Expected O, but got Unknown
		OpenDuration = TimeSpan.FromMilliseconds(210.0);
		CloseDuration = TimeSpan.FromMilliseconds(180.0);
		OpenEasing = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		CloseEasing = new CubicEase
		{
			EasingMode = EasingMode.EaseInOut
		};
		PopupWheelIsolationStates = new ConditionalWeakTable<Dispatcher, PopupWheelIsolationState>();
		IsPopupOpenProperty = DependencyProperty.Register("IsPopupOpen", typeof(bool), typeof(AnimatedComboBox), new PropertyMetadata((object)false));
		IsDropDownClosingProperty = DependencyProperty.Register("IsDropDownClosing", typeof(bool), typeof(AnimatedComboBox), new PropertyMetadata((object)false));
		DropDownItemContainerStyleProperty = DependencyProperty.Register("DropDownItemContainerStyle", typeof(Style), typeof(AnimatedComboBox), new PropertyMetadata((PropertyChangedCallback)null));
		SelectionItemTemplateProperty = DependencyProperty.Register("SelectionItemTemplate", typeof(DataTemplate), typeof(AnimatedComboBox), new PropertyMetadata((PropertyChangedCallback)null));
		SelectionItemTemplateSelectorProperty = DependencyProperty.Register("SelectionItemTemplateSelector", typeof(DataTemplateSelector), typeof(AnimatedComboBox), new PropertyMetadata((PropertyChangedCallback)null));
		EventManager.RegisterClassHandler(typeof(ScrollViewer), Mouse.PreviewMouseWheelEvent, new MouseWheelEventHandler(ScrollViewer_PreviewMouseWheelIsolation), handledEventsToo: true);
	}

	public AnimatedComboBox()
	{
		dropDownDescriptor = DependencyPropertyDescriptor.FromProperty(ComboBox.IsDropDownOpenProperty, typeof(ComboBox));
		AttachDropDownDescriptor();
		base.Loaded += AnimatedComboBox_Loaded;
		base.Unloaded += AnimatedComboBox_Unloaded;
	}

	public override void OnApplyTemplate()
	{
		DetachPopupListBox();
		DetachPopupSurface();
		DetachPopup();
		base.OnApplyTemplate();
		popup = GetTemplateChild("PART_Popup") as Popup;
		popupListBox = base.Template.FindName("PART_DropDownList", this) as ListBox;
		popupSurface = base.Template.FindName("PopupSurface", this) as FrameworkElement;
		selectionTextBlock = GetTemplateChild("SelectionTextBlock") as TextBlock;
		selectionContentPresenter = GetTemplateChild("SelectionContentPresenter") as ContentPresenter;
		AttachPopup();
		AttachPopupSurface();
		AttachPopupListBox();
		if (IsPopupOpen)
		{
			ActivatePopupWheelIsolation();
			AttachPopupInputGuard();
		}
		if (popupSurface != null)
		{
			popupSurface.CacheMode = new BitmapCache();
			popupSurface.IsHitTestVisible = IsPopupOpen;
		}
		UpdateSelectionPresenterMode();
		EnsurePopupTransforms();
		if (IsPopupOpen && popupSurface != null)
		{
			RestorePopupOpenVisualState();
		}
		else
		{
			SetPopupVisualState(0.0, -10.0, 0.92);
		}
	}

	private void OnDropDownOpenChanged(object? sender, EventArgs e)
	{
		if (base.IsDropDownOpen)
		{
			BeginOpenAnimation();
		}
		else
		{
			BeginCloseAnimation();
		}
	}

	private void BeginOpenAnimation()
	{
		DispatcherTimer? obj = closeTimer;
		if (obj != null)
		{
			obj.Stop();
		}
		closeTimer = null;
		IsDropDownClosing = false;
		ResolvePopupContentParts();
		EnsurePopupTransforms();
		UpdatePopupPlacement();
		if (popupSurface != null)
		{
			popupSurface.BeginAnimation(UIElement.OpacityProperty, null);
			scaleTransform?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			translateTransform?.BeginAnimation(TranslateTransform.YProperty, null);
			popupSurface.IsHitTestVisible = false;
			SetPopupVisualState(0.0, GetOpenTranslateOffset(), 0.92);
		}
		long generation = ++popupOpenGeneration;
		ActivatePopupWheelIsolation();
		AttachPopupInputGuard();
		IsPopupOpen = true;
		SchedulePopupOpenAnimation(generation);
	}

	private void BeginCloseAnimation()
	{
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Expected O, but got Unknown
		if (!IsPopupOpen)
		{
			return;
		}
		popupOpenGeneration++;
		DispatcherTimer? obj = closeTimer;
		if (obj != null)
		{
			obj.Stop();
		}
		IsDropDownClosing = true;
		if (popupSurface != null)
		{
			EnsurePopupTransforms();
			popupSurface.IsHitTestVisible = false;
			popupSurface.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(popupSurface.Opacity, 0.0, CloseDuration)
			{
				EasingFunction = CloseEasing
			});
			scaleTransform?.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scaleTransform?.ScaleY ?? 1.0, 0.92, CloseDuration)
			{
				EasingFunction = CloseEasing
			});
			translateTransform?.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translateTransform?.Y ?? 0.0, GetCloseTranslateOffset(), CloseDuration)
			{
				EasingFunction = CloseEasing
			});
		}
		closeTimer = new DispatcherTimer
		{
			Interval = CloseDuration.TimeSpan
		};
		closeTimer.Tick += delegate
		{
			DispatcherTimer? obj2 = closeTimer;
			if (obj2 != null)
			{
				obj2.Stop();
			}
			closeTimer = null;
			IsPopupOpen = false;
			DeactivatePopupWheelIsolation();
			DetachPopupInputGuard();
			IsDropDownClosing = false;
			if (popupSurface != null)
			{
				SetPopupVisualState(0.0, GetCloseTranslateOffset(), 0.92);
			}
		};
		closeTimer.Start();
	}

	private void EnsurePopupTransforms()
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (popupSurface != null)
		{
			popupSurface.RenderTransformOrigin = (opensAbove ? new Point(0.5, 1.0) : new Point(0.5, 0.0));
			if (popupSurface.RenderTransform is TransformGroup { Children: { Count: 2 } } transformGroup && transformGroup.Children[0] is ScaleTransform scaleTransform && transformGroup.Children[1] is TranslateTransform translateTransform)
			{
				this.scaleTransform = scaleTransform;
				this.translateTransform = translateTransform;
				return;
			}
			this.scaleTransform = new ScaleTransform(1.0, 1.0);
			this.translateTransform = new TranslateTransform(0.0, 0.0);
			popupSurface.RenderTransform = new TransformGroup
			{
				Children = new TransformCollection { this.scaleTransform, this.translateTransform }
			};
		}
	}

	private void SetPopupVisualState(double opacity, double translateY, double scaleY)
	{
		if (popupSurface != null)
		{
			popupSurface.Opacity = opacity;
			if (scaleTransform != null)
			{
				scaleTransform.ScaleY = scaleY;
			}
			if (translateTransform != null)
			{
				translateTransform.Y = translateY;
			}
		}
	}

	private void RestorePopupOpenVisualState()
	{
		if (popupSurface != null)
		{
			FrameworkElement frameworkElement = popupSurface;
			if (frameworkElement.CacheMode == null)
			{
				CacheMode cacheMode = (frameworkElement.CacheMode = new BitmapCache());
			}
			EnsurePopupTransforms();
			popupSurface.BeginAnimation(UIElement.OpacityProperty, null);
			scaleTransform?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			translateTransform?.BeginAnimation(TranslateTransform.YProperty, null);
			popupSurface.IsHitTestVisible = true;
			SetPopupVisualState(1.0, 0.0, 1.0);
		}
	}

	private void SchedulePopupOpenAnimation(long generation)
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			if (IsPopupOpen && generation == popupOpenGeneration)
			{
				ResolvePopupContentParts();
				FrameworkElement frameworkElement = popupSurface;
				if (frameworkElement != null && frameworkElement.IsLoaded && popupOpenAnimationStartedGeneration != generation)
				{
					popupOpenAnimationStartedGeneration = generation;
					EnsurePopupTransforms();
					popupSurface.IsHitTestVisible = true;
					popupSurface.BeginAnimation(UIElement.OpacityProperty, null);
					scaleTransform?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
					translateTransform?.BeginAnimation(TranslateTransform.YProperty, null);
					SetPopupVisualState(0.0, GetOpenTranslateOffset(), 0.92);
					DoubleAnimation animation = new DoubleAnimation(0.0, 1.0, OpenDuration)
					{
						EasingFunction = OpenEasing
					};
					DoubleAnimation animation2 = new DoubleAnimation(0.92, 1.0, OpenDuration)
					{
						EasingFunction = OpenEasing
					};
					DoubleAnimation animation3 = new DoubleAnimation(GetOpenTranslateOffset(), 0.0, OpenDuration)
					{
						EasingFunction = OpenEasing
					};
					popupSurface.BeginAnimation(UIElement.OpacityProperty, animation);
					scaleTransform?.BeginAnimation(ScaleTransform.ScaleYProperty, animation2);
					translateTransform?.BeginAnimation(TranslateTransform.YProperty, animation3);
				}
			}
		}, (DispatcherPriority)4, Array.Empty<object>());
	}

	private void ResolvePopupContentParts()
	{
		if (popupSurface == null)
		{
			popupSurface = base.Template.FindName("PopupSurface", this) as FrameworkElement;
			AttachPopupSurface();
		}
		if (popupListBox == null)
		{
			popupListBox = base.Template.FindName("PART_DropDownList", this) as ListBox;
			AttachPopupListBox();
		}
	}

	private void UpdatePopupPlacement()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		if (popup != null)
		{
			double num = GetPopupHeightEstimate() + 28.0;
			Point val = PointToScreen(new Point(0.0, 0.0));
			double y = val.Y;
			double num2 = val.Y + base.ActualHeight;
			Rect workArea = SystemParameters.WorkArea;
			double num3 = workArea.Bottom - num2;
			workArea = SystemParameters.WorkArea;
			double num4 = y - workArea.Top;
			opensAbove = num3 < num + 6.0 && num4 > num3;
			popup.Placement = (opensAbove ? PlacementMode.Top : PlacementMode.Bottom);
			popup.HorizontalOffset = 0.0;
			popup.VerticalOffset = (opensAbove ? 8.0 : (-8.0));
			EnsurePopupTransforms();
		}
	}

	private double GetPopupHeightEstimate()
	{
		double num = ((double.IsNaN(base.MaxDropDownHeight) || base.MaxDropDownHeight <= 0.0) ? 260.0 : base.MaxDropDownHeight);
		if (base.Items.Count <= 0)
		{
			return num;
		}
		return Math.Min((double)base.Items.Count * GetDropDownItemHeightEstimate() + 10.0, num);
	}

	private double GetOpenTranslateOffset()
	{
		return opensAbove ? 10 : (-10);
	}

	private double GetCloseTranslateOffset()
	{
		return opensAbove ? 8 : (-8);
	}

	private double GetDropDownItemHeightEstimate()
	{
		if (base.ItemContainerGenerator.ContainerFromIndex(0) is FrameworkElement { ActualHeight: >0.0 } frameworkElement)
		{
			return frameworkElement.ActualHeight;
		}
		return Math.Max(38.0, base.FontSize + 22.0);
	}

	private void AttachPopupListBox()
	{
		if (popupListBox != null)
		{
			popupListBox.SelectionChanged += PopupListBox_SelectionChanged;
			popupListBox.PreviewMouseLeftButtonUp += PopupListBox_PreviewMouseLeftButtonUp;
			popupListBox.PreviewKeyDown += PopupListBox_PreviewKeyDown;
			popupListBox.PreviewMouseWheel += PopupDropDown_PreviewMouseWheel;
		}
	}

	private void AttachPopupSurface()
	{
		if (popupSurface != null)
		{
			popupSurface.Loaded += PopupSurface_Loaded;
			popupSurface.PreviewMouseWheel += PopupDropDown_PreviewMouseWheel;
		}
	}

	private void DetachPopupListBox()
	{
		if (popupListBox != null)
		{
			popupListBox.SelectionChanged -= PopupListBox_SelectionChanged;
			popupListBox.PreviewMouseLeftButtonUp -= PopupListBox_PreviewMouseLeftButtonUp;
			popupListBox.PreviewKeyDown -= PopupListBox_PreviewKeyDown;
			popupListBox.PreviewMouseWheel -= PopupDropDown_PreviewMouseWheel;
			popupListBox = null;
		}
	}

	private void PopupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		object obj = e.AddedItems.Cast<object>().FirstOrDefault((object item) => item != null);
		if (obj != null)
		{
			((DependencyObject)this).SetCurrentValue(Selector.SelectedItemProperty, obj);
		}
	}

	private void AnimatedComboBox_Loaded(object sender, RoutedEventArgs e)
	{
		AttachDropDownDescriptor();
	}

	private void AnimatedComboBox_Unloaded(object sender, RoutedEventArgs e)
	{
		DispatcherTimer? obj = closeTimer;
		if (obj != null)
		{
			obj.Stop();
		}
		DetachPopupListBox();
		DetachPopupSurface();
		DeactivatePopupWheelIsolation();
		DetachPopupInputGuard();
		DetachPopup();
		DetachDropDownDescriptor();
	}

	private void AttachDropDownDescriptor()
	{
		if (!isDropDownDescriptorAttached)
		{
			((PropertyDescriptor)(object)dropDownDescriptor).AddValueChanged((object)this, (EventHandler)OnDropDownOpenChanged);
			isDropDownDescriptorAttached = true;
		}
	}

	private void DetachDropDownDescriptor()
	{
		if (isDropDownDescriptorAttached)
		{
			((PropertyDescriptor)(object)dropDownDescriptor).RemoveValueChanged((object)this, (EventHandler)OnDropDownOpenChanged);
			isDropDownDescriptorAttached = false;
		}
	}

	private void DetachPopupSurface()
	{
		if (popupSurface != null)
		{
			popupSurface.Loaded -= PopupSurface_Loaded;
			popupSurface.PreviewMouseWheel -= PopupDropDown_PreviewMouseWheel;
			popupSurface = null;
		}
	}

	private void PopupSurface_Loaded(object sender, RoutedEventArgs e)
	{
		if (IsPopupOpen)
		{
			SchedulePopupOpenAnimation(popupOpenGeneration);
		}
	}

	private void AttachPopup()
	{
		if (popup != null)
		{
			popup.Opened += Popup_Opened;
			popup.Closed += Popup_Closed;
		}
	}

	private void DetachPopup()
	{
		if (popup != null)
		{
			popup.Opened -= Popup_Opened;
			popup.Closed -= Popup_Closed;
			DetachPopupInputGuard();
			popup = null;
		}
	}

	private void Popup_Opened(object? sender, EventArgs e)
	{
		ResolvePopupContentParts();
		AttachPopupInputGuard();
		popupListBox?.Focus();
	}

	private void Popup_Closed(object? sender, EventArgs e)
	{
		StopPopupScrollAnimation();
		DeactivatePopupWheelIsolation();
		DetachPopupInputGuard();
	}

	private void PopupListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (base.IsDropDownOpen && sender is ListBox itemsControl)
		{
			object originalSource = e.OriginalSource;
			DependencyObject val = (DependencyObject)((originalSource is DependencyObject) ? originalSource : null);
			if (val != null && ItemsControl.ContainerFromElement(itemsControl, val) is ListBoxItem { IsEnabled: not false })
			{
				base.IsDropDownOpen = false;
			}
		}
	}

	private void PopupListBox_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Invalid comparison between Unknown and I4
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Invalid comparison between Unknown and I4
		if (base.IsDropDownOpen)
		{
			Key key = e.Key;
			if (((int)key == 6 || (int)key == 13 || (int)key == 18) ? true : false)
			{
				base.IsDropDownOpen = false;
				e.Handled = true;
			}
		}
	}

	private void PopupDropDown_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		ProcessOpenPopupMouseWheel(e, cursorOverPopup: true);
	}

	private void InputManager_PreProcessInput(object sender, PreProcessInputEventArgs e)
	{
		if (e.StagingItem.Input is MouseWheelEventArgs e2 && e2.RoutedEvent == Mouse.PreviewMouseWheelEvent)
		{
			ProcessOpenPopupMouseWheel(e2, IsCursorOverPopupSurface());
		}
	}

	internal bool ProcessOpenPopupMouseWheel(MouseWheelEventArgs e, bool cursorOverPopup)
	{
		if (!IsPopupOpen)
		{
			return false;
		}
		e.Handled = true;
		if (cursorOverPopup)
		{
			ScrollPopupList(e);
		}
		return true;
	}

	private void ScrollPopupList(MouseWheelEventArgs e)
	{
		ListBox listBox = popupListBox;
		if (listBox != null)
		{
			listBox.ApplyTemplate();
			listBox.UpdateLayout();
			SmoothScrollBehavior.HandleMouseWheelFromDescendant((DependencyObject)(object)listBox, e, handleWhenUnavailable: true);
		}
	}

	internal void AttachPopupInputGuard()
	{
		InputManager current = InputManager.Current;
		if (popupInputManager != current)
		{
			DetachPopupInputGuard();
			popupInputManager = current;
			popupInputManager.PreProcessInput += InputManager_PreProcessInput;
		}
	}

	internal void DetachPopupInputGuard()
	{
		if (popupInputManager != null)
		{
			popupInputManager.PreProcessInput -= InputManager_PreProcessInput;
			popupInputManager = null;
		}
	}

	internal void ActivatePopupWheelIsolation()
	{
		PopupWheelIsolationStates.GetOrCreateValue(((DispatcherObject)this).Dispatcher).ActiveComboBox = new WeakReference<AnimatedComboBox>(this);
	}

	internal void DeactivatePopupWheelIsolation()
	{
		if (PopupWheelIsolationStates.TryGetValue(((DispatcherObject)this).Dispatcher, out PopupWheelIsolationState value))
		{
			WeakReference<AnimatedComboBox> activeComboBox = value.ActiveComboBox;
			if (activeComboBox != null && activeComboBox.TryGetTarget(out var target) && target == this)
			{
				value.ActiveComboBox = null;
			}
		}
	}

	private static void ScrollViewer_PreviewMouseWheelIsolation(object sender, MouseWheelEventArgs e)
	{
		if (!(sender is ScrollViewer scrollViewer) || !PopupWheelIsolationStates.TryGetValue(((DispatcherObject)scrollViewer).Dispatcher, out PopupWheelIsolationState value))
		{
			return;
		}
		WeakReference<AnimatedComboBox> activeComboBox = value.ActiveComboBox;
		if (activeComboBox != null && activeComboBox.TryGetTarget(out var target))
		{
			if (!target.IsPopupOpen)
			{
				value.ActiveComboBox = null;
			}
			else if (!target.IsPopupScrollViewer((DependencyObject)(object)scrollViewer))
			{
				e.Handled = true;
			}
		}
	}

	private bool IsPopupScrollViewer(DependencyObject scrollViewer)
	{
		if (popupListBox == null)
		{
			return false;
		}
		for (DependencyObject val = scrollViewer; val != null; val = VisualTreeHelper.GetParent(val))
		{
			if ((object)val == popupListBox)
			{
				return true;
			}
		}
		return false;
	}

	private bool IsCursorOverPopupSurface()
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		if (popupSurface == null || !GetCursorPos(out var point))
		{
			return false;
		}
		Point val = popupSurface.PointFromScreen(new Point((double)point.X, (double)point.Y));
		if (val.X >= 0.0 && val.Y >= 0.0 && val.X <= popupSurface.ActualWidth)
		{
			return val.Y <= popupSurface.ActualHeight;
		}
		return false;
	}

	private void UpdateSelectionPresenterMode()
	{
		if (selectionTextBlock != null && selectionContentPresenter != null)
		{
			bool flag = SelectionItemTemplate != null || SelectionItemTemplateSelector != null;
			selectionTextBlock.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			selectionContentPresenter.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private void StopPopupScrollAnimation()
	{
		if (popupListBox != null)
		{
			popupListBox.ApplyTemplate();
			popupListBox.UpdateLayout();
			SmoothScrollBehavior.CancelAnimationFromDescendant((DependencyObject)(object)popupListBox);
		}
	}

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out CursorPoint point);
}
