using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Launcher.App.Behaviors;

namespace Launcher.App.Controls;

public partial class ListPageItemButton : UserControl, IComponentConnector
{
	public static readonly DependencyProperty TitleProperty;

	public static readonly DependencyProperty SubtitleProperty;

	public static readonly DependencyProperty TrailingTextProperty;

	public static readonly DependencyProperty TrailingContentProperty;

	public static readonly DependencyProperty TitleTrailingContentProperty;

	public static readonly DependencyProperty IconSourceProperty;

	private static readonly DependencyPropertyKey ResolvedIconSourcePropertyKey;

	public static readonly DependencyProperty ResolvedIconSourceProperty;

	public static readonly DependencyProperty IconKeyProperty;

	public static readonly DependencyProperty CommandProperty;

	public static readonly DependencyProperty CommandParameterProperty;

	public static readonly DependencyProperty IsSelectedProperty;

	public static readonly DependencyProperty IsFirstVisibleProperty;

	public static readonly DependencyProperty IsLastVisibleProperty;

	public static readonly DependencyProperty IsPreviousItemHighlightedProperty;

	public static readonly DependencyProperty IsPointerOverOptionProperty;

	public static readonly DependencyProperty ShouldPlayEnterAnimationProperty;

	public static readonly DependencyProperty IsEnterAnimationPendingProperty;

	public static readonly DependencyProperty EnterAnimationIndexProperty;

	public static readonly DependencyProperty ItemMarginProperty;

	public static readonly DependencyProperty IconColumnWidthProperty;

	public static readonly DependencyProperty IconWidthProperty;

	public static readonly DependencyProperty IconHeightProperty;

	public static readonly DependencyProperty IconMarginProperty;

	public static readonly DependencyProperty IconScalingModeProperty;

	public static readonly DependencyProperty TextMarginProperty;

	public static readonly DependencyProperty TrailingMarginProperty;

	public static readonly DependencyProperty TitleFontSizeProperty;

	public static readonly DependencyProperty TitleFontWeightProperty;

	public static readonly DependencyProperty TitleForegroundProperty;

	public static readonly DependencyProperty SubtitleFontSizeProperty;

	public static readonly DependencyProperty SubtitleForegroundProperty;

	public static readonly DependencyProperty IconOpacityProperty;

	public static readonly DependencyProperty IconOverlayKeyProperty;

	public static readonly DependencyProperty IconOverlayForegroundProperty;

	public static readonly DependencyProperty TrailingFontSizeProperty;

	public static readonly DependencyProperty TrailingForegroundProperty;

	// StartRide：图标底板。原版图标是"浮"在行里的，视觉上很单薄；
	// 加上一层圆角底板 + 一像素内描边之后，列表才有真正的"控件感"。
	public static readonly DependencyProperty IconTileBackgroundProperty;

	public static readonly DependencyProperty IconTileBorderBrushProperty;

	public static readonly DependencyProperty IconTileCornerRadiusProperty;

	public static readonly DependencyProperty IconTilePaddingProperty;

	public static readonly DependencyProperty IconTileBorderThicknessProperty;

	private bool isPreparingEnterAnimation;

	public Button InnerButton => PART_Button;

	public string Title
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(TitleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TitleProperty, (object)value);
		}
	}

	public string Subtitle
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(SubtitleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SubtitleProperty, (object)value);
		}
	}

	public string TrailingText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(TrailingTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TrailingTextProperty, (object)value);
		}
	}

	public object? TrailingContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(TrailingContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TrailingContentProperty, value);
		}
	}

	public object? TitleTrailingContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(TitleTrailingContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TitleTrailingContentProperty, value);
		}
	}

	public object? IconSource
	{
		get
		{
			return ((DependencyObject)this).GetValue(IconSourceProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconSourceProperty, value);
		}
	}

	public ImageSource? ResolvedIconSource
	{
		get
		{
			return (ImageSource)((DependencyObject)this).GetValue(ResolvedIconSourceProperty);
		}
		private set
		{
			((DependencyObject)this).SetValue(ResolvedIconSourcePropertyKey, (object)value);
		}
	}

	public string? IconKey
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(IconKeyProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconKeyProperty, (object)value);
		}
	}

	public ICommand? Command
	{
		get
		{
			return (ICommand)((DependencyObject)this).GetValue(CommandProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(CommandProperty, (object)value);
		}
	}

	public object? CommandParameter
	{
		get
		{
			return ((DependencyObject)this).GetValue(CommandParameterProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(CommandParameterProperty, value);
		}
	}

	public bool IsSelected
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsSelectedProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsSelectedProperty, (object)value);
		}
	}

	public bool IsFirstVisible
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsFirstVisibleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsFirstVisibleProperty, (object)value);
		}
	}

	public bool IsLastVisible
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsLastVisibleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsLastVisibleProperty, (object)value);
		}
	}

	public bool IsPreviousItemHighlighted
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsPreviousItemHighlightedProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsPreviousItemHighlightedProperty, (object)value);
		}
	}

	public bool IsPointerOverOption
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsPointerOverOptionProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsPointerOverOptionProperty, (object)value);
		}
	}

	public bool ShouldPlayEnterAnimation
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(ShouldPlayEnterAnimationProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ShouldPlayEnterAnimationProperty, (object)value);
		}
	}

	public bool IsEnterAnimationPending
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsEnterAnimationPendingProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsEnterAnimationPendingProperty, (object)value);
		}
	}

	public int EnterAnimationIndex
	{
		get
		{
			return (int)((DependencyObject)this).GetValue(EnterAnimationIndexProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(EnterAnimationIndexProperty, (object)value);
		}
	}

	public Thickness ItemMargin
	{
		get
		{
			return (Thickness)((DependencyObject)this).GetValue(ItemMarginProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ItemMarginProperty, (object)value);
		}
	}

	public GridLength IconColumnWidth
	{
		get
		{
			return (GridLength)((DependencyObject)this).GetValue(IconColumnWidthProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconColumnWidthProperty, (object)value);
		}
	}

	public double IconWidth
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(IconWidthProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconWidthProperty, (object)value);
		}
	}

	public double IconHeight
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(IconHeightProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconHeightProperty, (object)value);
		}
	}

	public Thickness IconMargin
	{
		get
		{
			return (Thickness)((DependencyObject)this).GetValue(IconMarginProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconMarginProperty, (object)value);
		}
	}

	public BitmapScalingMode IconScalingMode
	{
		get
		{
			return (BitmapScalingMode)((DependencyObject)this).GetValue(IconScalingModeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconScalingModeProperty, (object)value);
		}
	}

	public Thickness TextMargin
	{
		get
		{
			return (Thickness)((DependencyObject)this).GetValue(TextMarginProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TextMarginProperty, (object)value);
		}
	}

	public Thickness TrailingMargin
	{
		get
		{
			return (Thickness)((DependencyObject)this).GetValue(TrailingMarginProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TrailingMarginProperty, (object)value);
		}
	}

	public double TitleFontSize
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(TitleFontSizeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TitleFontSizeProperty, (object)value);
		}
	}

	public FontWeight TitleFontWeight
	{
		get
		{
			return (FontWeight)((DependencyObject)this).GetValue(TitleFontWeightProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TitleFontWeightProperty, (object)value);
		}
	}

	public Brush TitleForeground
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(TitleForegroundProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TitleForegroundProperty, (object)value);
		}
	}

	public double SubtitleFontSize
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(SubtitleFontSizeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SubtitleFontSizeProperty, (object)value);
		}
	}

	public Brush SubtitleForeground
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(SubtitleForegroundProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SubtitleForegroundProperty, (object)value);
		}
	}

	public double IconOpacity
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(IconOpacityProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconOpacityProperty, (object)value);
		}
	}

	public string? IconOverlayKey
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(IconOverlayKeyProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconOverlayKeyProperty, (object)value);
		}
	}

	public Brush IconOverlayForeground
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(IconOverlayForegroundProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconOverlayForegroundProperty, (object)value);
		}
	}

	public double TrailingFontSize
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(TrailingFontSizeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TrailingFontSizeProperty, (object)value);
		}
	}

	public Brush TrailingForeground
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(TrailingForegroundProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TrailingForegroundProperty, (object)value);
		}
	}

	/// <summary>图标底板填充（传 null 则是原来的"无底板"效果）。</summary>
	public Brush IconTileBackground
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(IconTileBackgroundProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconTileBackgroundProperty, (object)value);
		}
	}

	/// <summary>图标底板一像素内描边。</summary>
	public Brush IconTileBorderBrush
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(IconTileBorderBrushProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconTileBorderBrushProperty, (object)value);
		}
	}

	/// <summary>图标底板圆角。</summary>
	public CornerRadius IconTileCornerRadius
	{
		get
		{
			return (CornerRadius)((DependencyObject)this).GetValue(IconTileCornerRadiusProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconTileCornerRadiusProperty, (object)value);
		}
	}

	/// <summary>图标底板内边距（glyph 与底板之间的呼吸空间）。默认 0 = 不加底板。</summary>
	public Thickness IconTilePadding
	{
		get
		{
			return (Thickness)((DependencyObject)this).GetValue(IconTilePaddingProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconTilePaddingProperty, (object)value);
		}
	}

	/// <summary>图标底板描边粗细。默认 0 = 不描边（保持旧版观感）。</summary>
	public Thickness IconTileBorderThickness
	{
		get
		{
			return (Thickness)((DependencyObject)this).GetValue(IconTileBorderThicknessProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconTileBorderThicknessProperty, (object)value);
		}
	}

	public ListPageItemButton()
	{
		InitializeComponent();
		base.Loaded += delegate
		{
			PlayEnterAnimationIfNeeded();
		};
	}

	private void Root_MouseEnter(object sender, MouseEventArgs e)
	{
		IsPointerOverOption = true;
		OptionHoverBehavior.SetIsExternalActive((DependencyObject)(object)PART_Button, value: true);
		if (TrailingContent != null)
		{
			AnimateTrailingVisibility(0.0, 1.0, TimeSpan.FromMilliseconds(140.0));
		}
	}

	private void Root_MouseLeave(object sender, MouseEventArgs e)
	{
		IsPointerOverOption = false;
		OptionHoverBehavior.SetIsExternalActive((DependencyObject)(object)PART_Button, value: false);
		if (TrailingContent != null)
		{
			AnimateTrailingVisibility(1.0, 0.0, TimeSpan.FromMilliseconds(180.0));
		}
	}

	private void PlayEnterAnimationIfNeeded()
	{
		if (!ShouldPlayEnterAnimation)
		{
			if (IsEnterAnimationPending)
			{
				HoldEntrancePendingVisual();
			}
			else
			{
				ResetVisual();
			}
			return;
		}
		TimeSpan delay = TimeSpan.FromMilliseconds(Math.Min(EnterAnimationIndex, 12) * 30);
		TimeSpan duration = TimeSpan.FromMilliseconds(330.0);
		CubicEase easing = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		ScaleTransform scaleTransform = new ScaleTransform(0.96, 0.96);
		TranslateTransform translateTransform = new TranslateTransform(0.0, 14.0);
		AnimatedRoot.BeginAnimation(UIElement.OpacityProperty, null);
		AnimatedRoot.RenderTransform = new TransformGroup
		{
			Children = 
			{
				(Transform)scaleTransform,
				(Transform)translateTransform
			}
		};
		AnimatedRoot.Opacity = 0.0;
		isPreparingEnterAnimation = true;
		try
		{
			BindingExpression bindingExpression = BindingOperations.GetBindingExpression((DependencyObject)(object)this, ShouldPlayEnterAnimationProperty);
			if (bindingExpression?.ResolvedSource is VirtualizedListItemState virtualizedListItemState && string.Equals(bindingExpression.ResolvedSourcePropertyName, "ShouldPlayEnterAnimation", StringComparison.Ordinal))
			{
				virtualizedListItemState.ShouldPlayEnterAnimation = false;
			}
			else
			{
				((DependencyObject)this).SetCurrentValue(ShouldPlayEnterAnimationProperty, (object)false);
				bindingExpression?.UpdateSource();
			}
			((DependencyObject)this).ClearValue(IsEnterAnimationPendingProperty);
		}
		finally
		{
			isPreparingEnterAnimation = false;
		}
		((DispatcherObject)this).Dispatcher.BeginInvoke((DispatcherPriority)4, (Delegate)(Action)delegate
		{
			BeginEnterAnimation(scaleTransform, translateTransform, delay, duration, easing);
		});
	}

	private static void OnIconSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ListPageItemButton listPageItemButton)
		{
			listPageItemButton.ResolvedIconSource = IconSourceImageLoader.TryLoad(e.NewValue);
		}
	}

	private static void OnShouldPlayEnterAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ListPageItemButton { IsLoaded: not false } listPageItemButton)
		{
			object newValue = e.NewValue;
			if (newValue is bool && (bool)newValue)
			{
				listPageItemButton.PlayEnterAnimationIfNeeded();
			}
		}
	}

	private static void OnIsEnterAnimationPendingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ListPageItemButton listPageItemButton)
		{
			object newValue = e.NewValue;
			if (newValue is bool && (bool)newValue)
			{
				listPageItemButton.HoldEntrancePendingVisual();
			}
			else if (!listPageItemButton.isPreparingEnterAnimation && !listPageItemButton.ShouldPlayEnterAnimation && listPageItemButton.IsLoaded)
			{
				listPageItemButton.ResetVisual();
			}
		}
	}

	private void HoldEntrancePendingVisual()
	{
		AnimatedRoot.BeginAnimation(UIElement.OpacityProperty, null);
		AnimatedRoot.Opacity = 0.0;
		AnimatedRoot.RenderTransform = null;
		TrailingContentPresenter.BeginAnimation(UIElement.OpacityProperty, null);
	}

	private void ResetVisual()
	{
		AnimatedRoot.BeginAnimation(UIElement.OpacityProperty, null);
		AnimatedRoot.Opacity = 1.0;
		AnimatedRoot.RenderTransform = null;
		TrailingContentPresenter.BeginAnimation(UIElement.OpacityProperty, null);
	}

	private void BeginEnterAnimation(ScaleTransform scaleTransform, TranslateTransform translateTransform, TimeSpan delay, TimeSpan duration, IEasingFunction easing)
	{
		if (AnimatedRoot.IsLoaded)
		{
			AnimatedRoot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, duration)
			{
				BeginTime = delay,
				EasingFunction = easing
			});
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1.0, duration)
			{
				BeginTime = delay,
				EasingFunction = easing
			});
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1.0, duration)
			{
				BeginTime = delay,
				EasingFunction = easing
			});
			translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14.0, 0.0, duration)
			{
				BeginTime = delay,
				EasingFunction = easing
			});
		}
	}

	private void AnimateTrailingVisibility(double trailingTextOpacity, double trailingContentOpacity, TimeSpan duration)
	{
		TrailingTextBlock.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(trailingTextOpacity, duration));
		TrailingContentPresenter.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(trailingContentOpacity, duration));
	}


	static ListPageItemButton()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Expected O, but got Unknown
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Expected O, but got Unknown
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Expected O, but got Unknown
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Expected O, but got Unknown
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Expected O, but got Unknown
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Expected O, but got Unknown
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Expected O, but got Unknown
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Expected O, but got Unknown
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Expected O, but got Unknown
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Expected O, but got Unknown
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Expected O, but got Unknown
		//IL_023c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Expected O, but got Unknown
		//IL_026a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0274: Expected O, but got Unknown
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Expected O, but got Unknown
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dc: Expected O, but got Unknown
		//IL_030c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0316: Expected O, but got Unknown
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_031b: Expected O, but got Unknown
		//IL_033f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Expected O, but got Unknown
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		//IL_039f: Expected O, but got Unknown
		//IL_03d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03da: Expected O, but got Unknown
		//IL_0406: Unknown result type (might be due to invalid IL or missing references)
		//IL_0410: Expected O, but got Unknown
		//IL_043c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0446: Expected O, but got Unknown
		//IL_0492: Unknown result type (might be due to invalid IL or missing references)
		//IL_049c: Expected O, but got Unknown
		//IL_04c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ca: Expected O, but got Unknown
		//IL_0516: Unknown result type (might be due to invalid IL or missing references)
		//IL_0520: Expected O, but got Unknown
		//IL_056c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0576: Expected O, but got Unknown
		//IL_05a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ac: Expected O, but got Unknown
		//IL_05d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_05de: Expected O, but got Unknown
		//IL_05fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0607: Expected O, but got Unknown
		//IL_0633: Unknown result type (might be due to invalid IL or missing references)
		//IL_063d: Expected O, but got Unknown
		//IL_065c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0666: Expected O, but got Unknown
		//IL_0692: Unknown result type (might be due to invalid IL or missing references)
		//IL_069c: Expected O, but got Unknown
		//IL_06bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c5: Expected O, but got Unknown
		//IL_06e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ee: Expected O, but got Unknown
		//IL_071a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0724: Expected O, but got Unknown
		//IL_0743: Unknown result type (might be due to invalid IL or missing references)
		//IL_074d: Expected O, but got Unknown
		TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(ListPageItemButton), new PropertyMetadata((object)string.Empty));
		SubtitleProperty = DependencyProperty.Register("Subtitle", typeof(string), typeof(ListPageItemButton), new PropertyMetadata((object)string.Empty));
		TrailingTextProperty = DependencyProperty.Register("TrailingText", typeof(string), typeof(ListPageItemButton), new PropertyMetadata((object)string.Empty));
		TrailingContentProperty = DependencyProperty.Register("TrailingContent", typeof(object), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		TitleTrailingContentProperty = DependencyProperty.Register("TitleTrailingContent", typeof(object), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		IconSourceProperty = DependencyProperty.Register("IconSource", typeof(object), typeof(ListPageItemButton), new PropertyMetadata((object)null, new PropertyChangedCallback(OnIconSourceChanged)));
		ResolvedIconSourcePropertyKey = DependencyProperty.RegisterReadOnly("ResolvedIconSource", typeof(ImageSource), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		ResolvedIconSourceProperty = ResolvedIconSourcePropertyKey.DependencyProperty;
		IconKeyProperty = DependencyProperty.Register("IconKey", typeof(string), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		CommandProperty = DependencyProperty.Register("Command", typeof(ICommand), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		CommandParameterProperty = DependencyProperty.Register("CommandParameter", typeof(object), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(ListPageItemButton), new PropertyMetadata((object)false));
		IsFirstVisibleProperty = DependencyProperty.Register("IsFirstVisible", typeof(bool), typeof(ListPageItemButton), new PropertyMetadata((object)false));
		IsLastVisibleProperty = DependencyProperty.Register("IsLastVisible", typeof(bool), typeof(ListPageItemButton), new PropertyMetadata((object)false));
		IsPreviousItemHighlightedProperty = DependencyProperty.Register("IsPreviousItemHighlighted", typeof(bool), typeof(ListPageItemButton), new PropertyMetadata((object)false));
		IsPointerOverOptionProperty = DependencyProperty.Register("IsPointerOverOption", typeof(bool), typeof(ListPageItemButton), new PropertyMetadata((object)false));
		ShouldPlayEnterAnimationProperty = DependencyProperty.Register("ShouldPlayEnterAnimation", typeof(bool), typeof(ListPageItemButton), (PropertyMetadata)(object)new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(OnShouldPlayEnterAnimationChanged)));
		IsEnterAnimationPendingProperty = DependencyProperty.Register("IsEnterAnimationPending", typeof(bool), typeof(ListPageItemButton), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsEnterAnimationPendingChanged)));
		EnterAnimationIndexProperty = DependencyProperty.Register("EnterAnimationIndex", typeof(int), typeof(ListPageItemButton), new PropertyMetadata((object)0));
		ItemMarginProperty = DependencyProperty.Register("ItemMargin", typeof(Thickness), typeof(ListPageItemButton), new PropertyMetadata((object)new Thickness(0.0, 0.0, 12.0, 0.0)));
		IconColumnWidthProperty = DependencyProperty.Register("IconColumnWidth", typeof(GridLength), typeof(ListPageItemButton), new PropertyMetadata((object)new GridLength(50.0)));
		IconWidthProperty = DependencyProperty.Register("IconWidth", typeof(double), typeof(ListPageItemButton), new PropertyMetadata((object)32.0));
		IconHeightProperty = DependencyProperty.Register("IconHeight", typeof(double), typeof(ListPageItemButton), new PropertyMetadata((object)32.0));
		IconMarginProperty = DependencyProperty.Register("IconMargin", typeof(Thickness), typeof(ListPageItemButton), new PropertyMetadata((object)new Thickness(10.0, 0.0, 0.0, 0.0)));
		// StartRide：原版默认 NearestNeighbor 是为 Minecraft 像素风贴图准备的，
		// 用在 BeamNG 车辆图 / 游戏图标这类平滑图像上会让边缘出现明显锯齿。
		// 默认改为 HighQuality（真正需要像素风的调用方仍可显式传 NearestNeighbor）。
		IconScalingModeProperty = DependencyProperty.Register("IconScalingMode", typeof(BitmapScalingMode), typeof(ListPageItemButton), new PropertyMetadata((object)BitmapScalingMode.HighQuality));
		TextMarginProperty = DependencyProperty.Register("TextMargin", typeof(Thickness), typeof(ListPageItemButton), new PropertyMetadata((object)new Thickness(5.0, 0.0, 0.0, 0.0)));
		TrailingMarginProperty = DependencyProperty.Register("TrailingMargin", typeof(Thickness), typeof(ListPageItemButton), new PropertyMetadata((object)new Thickness(12.0, 0.0, 24.0, 0.0)));
		TitleFontSizeProperty = DependencyProperty.Register("TitleFontSize", typeof(double), typeof(ListPageItemButton), new PropertyMetadata((object)15.0));
		TitleFontWeightProperty = DependencyProperty.Register("TitleFontWeight", typeof(FontWeight), typeof(ListPageItemButton), new PropertyMetadata((object)FontWeights.SemiBold));
		TitleForegroundProperty = DependencyProperty.Register("TitleForeground", typeof(Brush), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		SubtitleFontSizeProperty = DependencyProperty.Register("SubtitleFontSize", typeof(double), typeof(ListPageItemButton), new PropertyMetadata((object)11.0));
		SubtitleForegroundProperty = DependencyProperty.Register("SubtitleForeground", typeof(Brush), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		IconOpacityProperty = DependencyProperty.Register("IconOpacity", typeof(double), typeof(ListPageItemButton), new PropertyMetadata((object)1.0));
		IconOverlayKeyProperty = DependencyProperty.Register("IconOverlayKey", typeof(string), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		IconOverlayForegroundProperty = DependencyProperty.Register("IconOverlayForeground", typeof(Brush), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		TrailingFontSizeProperty = DependencyProperty.Register("TrailingFontSize", typeof(double), typeof(ListPageItemButton), new PropertyMetadata((object)12.0));
		TrailingForegroundProperty = DependencyProperty.Register("TrailingForeground", typeof(Brush), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		IconTileBackgroundProperty = DependencyProperty.Register("IconTileBackground", typeof(Brush), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		IconTileBorderBrushProperty = DependencyProperty.Register("IconTileBorderBrush", typeof(Brush), typeof(ListPageItemButton), new PropertyMetadata((PropertyChangedCallback)null));
		IconTileCornerRadiusProperty = DependencyProperty.Register("IconTileCornerRadius", typeof(CornerRadius), typeof(ListPageItemButton), new PropertyMetadata((object)new CornerRadius(10.0)));
		IconTilePaddingProperty = DependencyProperty.Register("IconTilePadding", typeof(Thickness), typeof(ListPageItemButton), new PropertyMetadata((object)new Thickness(0.0)));
		IconTileBorderThicknessProperty = DependencyProperty.Register("IconTileBorderThickness", typeof(Thickness), typeof(ListPageItemButton), new PropertyMetadata((object)new Thickness(0.0)));
	}
}
