using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Launcher.App.Controls;

public partial class ListPageFrame : UserControl, IComponentConnector
{
	public static readonly DependencyProperty TitleProperty;

	public static readonly DependencyProperty TitleIconSourceProperty;

	private static readonly DependencyPropertyKey ResolvedTitleIconSourcePropertyKey;

	public static readonly DependencyProperty ResolvedTitleIconSourceProperty;

	public static readonly DependencyProperty IsHeaderBackButtonVisibleProperty;

	public static readonly DependencyProperty HeaderBackCommandProperty;

	public static readonly DependencyProperty SearchTextProperty;

	public static readonly DependencyProperty IsSearchVisibleProperty;

	public static readonly DependencyProperty SearchLeadingContentProperty;

	public static readonly DependencyProperty SearchLeadingContentTemplateProperty;

	public static readonly DependencyProperty IsSearchLeadingContentVisibleProperty;

	public static readonly DependencyProperty SearchTrailingContentProperty;

	public static readonly DependencyProperty SearchToolbarContentProperty;

	public static readonly DependencyProperty IsSearchToolbarVisibleProperty;

	public static readonly DependencyProperty SearchToolbarContentTemplateProperty;

	public static readonly DependencyProperty SearchFilterContentProperty;

	public static readonly DependencyProperty IsSearchFilterVisibleProperty;

	public static readonly DependencyProperty SearchFilterContentTemplateProperty;

	public static readonly DependencyProperty IsListVisibleProperty;

	public static readonly DependencyProperty IsProgressiveBlurEnabledProperty;

	public static readonly DependencyProperty UseFrameScrollViewerProperty;

	public static readonly DependencyProperty OverlayContentProperty;

	public static readonly DependencyProperty ListContentProperty;

	public static readonly DependencyProperty FloatingContentProperty;

	private readonly ProgressiveBlurBandController? progressiveBlurController;

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

	public object? TitleIconSource
	{
		get
		{
			return ((DependencyObject)this).GetValue(TitleIconSourceProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TitleIconSourceProperty, value);
		}
	}

	public ImageSource? ResolvedTitleIconSource
	{
		get
		{
			return (ImageSource)((DependencyObject)this).GetValue(ResolvedTitleIconSourceProperty);
		}
		private set
		{
			((DependencyObject)this).SetValue(ResolvedTitleIconSourcePropertyKey, (object)value);
		}
	}

	public bool IsHeaderBackButtonVisible
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsHeaderBackButtonVisibleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsHeaderBackButtonVisibleProperty, (object)value);
		}
	}

	public ICommand? HeaderBackCommand
	{
		get
		{
			return (ICommand)((DependencyObject)this).GetValue(HeaderBackCommandProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(HeaderBackCommandProperty, (object)value);
		}
	}

	public string SearchText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(SearchTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SearchTextProperty, (object)value);
		}
	}

	public bool IsSearchVisible
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsSearchVisibleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsSearchVisibleProperty, (object)value);
		}
	}

	public object? SearchLeadingContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(SearchLeadingContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SearchLeadingContentProperty, value);
		}
	}

	public DataTemplate? SearchLeadingContentTemplate
	{
		get
		{
			return (DataTemplate)((DependencyObject)this).GetValue(SearchLeadingContentTemplateProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SearchLeadingContentTemplateProperty, (object)value);
		}
	}

	public bool IsSearchLeadingContentVisible
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsSearchLeadingContentVisibleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsSearchLeadingContentVisibleProperty, (object)value);
		}
	}

	public object? SearchTrailingContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(SearchTrailingContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SearchTrailingContentProperty, value);
		}
	}

	public object? SearchToolbarContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(SearchToolbarContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SearchToolbarContentProperty, value);
		}
	}

	public bool IsSearchToolbarVisible
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsSearchToolbarVisibleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsSearchToolbarVisibleProperty, (object)value);
		}
	}

	public DataTemplate? SearchToolbarContentTemplate
	{
		get
		{
			return (DataTemplate)((DependencyObject)this).GetValue(SearchToolbarContentTemplateProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SearchToolbarContentTemplateProperty, (object)value);
		}
	}

	public object? SearchFilterContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(SearchFilterContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SearchFilterContentProperty, value);
		}
	}

	public bool IsSearchFilterVisible
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsSearchFilterVisibleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsSearchFilterVisibleProperty, (object)value);
		}
	}

	public DataTemplate? SearchFilterContentTemplate
	{
		get
		{
			return (DataTemplate)((DependencyObject)this).GetValue(SearchFilterContentTemplateProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SearchFilterContentTemplateProperty, (object)value);
		}
	}

	public bool IsListVisible
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsListVisibleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsListVisibleProperty, (object)value);
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

	public bool UseFrameScrollViewer
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(UseFrameScrollViewerProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(UseFrameScrollViewerProperty, (object)value);
		}
	}

	public object? OverlayContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(OverlayContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(OverlayContentProperty, value);
		}
	}

	public object? ListContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(ListContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ListContentProperty, value);
		}
	}

	public object? FloatingContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(FloatingContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(FloatingContentProperty, value);
		}
	}

	public ScrollViewer ScrollViewer => PART_ScrollViewer;

	internal FrameworkElement ListLayerElement => PART_ListLayer;

	internal FrameworkElement ListVisualSourceElement => PART_ListVisualSource;

	internal FrameworkElement DirectListHostElement => PART_DirectListHost;

	internal FrameworkElement BlurBandViewportElement => PART_BlurBandViewport;

	internal FrameworkElement BlurBandUpscaleHostElement => PART_BlurBandUpscaleHost;

	internal ScaleTransform BlurBandUpscaleTransform => PART_BlurBandUpscaleTransform;

	internal FrameworkElement BlurBandHorizontalHostElement => PART_BlurBandHorizontalHost;

	internal FrameworkElement BlurBandVerticalHostElement => PART_BlurBandVerticalHost;

	internal VisualBrush BlurBandBrush => PART_BlurBandBrush;

	internal FrameworkElement HeaderOverlayElement => PART_HeaderOverlay;

	internal FrameworkElement HeaderTitleRowElement => PART_HeaderTitleRow;

	public ListPageFrame()
		: this(null, null)
	{
	}

	internal ListPageFrame(ProgressiveBlurEffectFactory? effectFactory, ProgressiveBlurEffectAttacher? effectAttacher)
	{
		InitializeComponent();
		progressiveBlurController = new ProgressiveBlurBandController(new ProgressiveBlurVisualParts(this, PART_ListLayer, PART_ListVisualSource, PART_DirectListHost, PART_BlurBandViewport, PART_BlurBandUpscaleHost, PART_BlurBandUpscaleTransform, PART_BlurBandHorizontalHost, PART_BlurBandVerticalHost, PART_BlurBandBrush), () => base.IsVisible && IsListVisible && IsProgressiveBlurEnabled, effectFactory, effectAttacher);
		base.Loaded += ListPageFrame_Loaded;
		base.Unloaded += ListPageFrame_Unloaded;
	}

	private static void OnTitleIconSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
	{
		if (dependencyObject is ListPageFrame listPageFrame)
		{
			listPageFrame.ResolvedTitleIconSource = IconSourceImageLoader.TryLoad(args.NewValue);
		}
	}

	private static void OnListVisibilityChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is ListPageFrame listPageFrame)
		{
			listPageFrame.progressiveBlurController?.Update();
		}
	}

	private static void OnProgressiveBlurEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is ListPageFrame listPageFrame)
		{
			bool becameEnabled = !(bool)e.OldValue && (bool)e.NewValue;
			listPageFrame.progressiveBlurController?.OnEnabledChanged(becameEnabled);
		}
	}

	private void ListPageFrame_Loaded(object sender, RoutedEventArgs e)
	{
		progressiveBlurController?.OnLoaded();
	}

	private void ListPageFrame_Unloaded(object sender, RoutedEventArgs e)
	{
		progressiveBlurController?.OnUnloaded();
	}


	static ListPageFrame()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Expected O, but got Unknown
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Expected O, but got Unknown
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected O, but got Unknown
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Expected O, but got Unknown
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Expected O, but got Unknown
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Expected O, but got Unknown
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Expected O, but got Unknown
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Expected O, but got Unknown
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Expected O, but got Unknown
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Expected O, but got Unknown
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Expected O, but got Unknown
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Expected O, but got Unknown
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_029e: Expected O, but got Unknown
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cc: Expected O, but got Unknown
		//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f5: Expected O, but got Unknown
		//IL_0320: Unknown result type (might be due to invalid IL or missing references)
		//IL_032a: Expected O, but got Unknown
		//IL_0325: Unknown result type (might be due to invalid IL or missing references)
		//IL_032f: Expected O, but got Unknown
		//IL_035a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0364: Expected O, but got Unknown
		//IL_035f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0369: Expected O, but got Unknown
		//IL_038d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0397: Expected O, but got Unknown
		//IL_03b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c0: Expected O, but got Unknown
		//IL_03df: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e9: Expected O, but got Unknown
		//IL_0408: Unknown result type (might be due to invalid IL or missing references)
		//IL_0412: Expected O, but got Unknown
		TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(ListPageFrame), new PropertyMetadata((object)string.Empty));
		TitleIconSourceProperty = DependencyProperty.Register("TitleIconSource", typeof(object), typeof(ListPageFrame), new PropertyMetadata((object)null, new PropertyChangedCallback(OnTitleIconSourceChanged)));
		ResolvedTitleIconSourcePropertyKey = DependencyProperty.RegisterReadOnly("ResolvedTitleIconSource", typeof(ImageSource), typeof(ListPageFrame), new PropertyMetadata((PropertyChangedCallback)null));
		ResolvedTitleIconSourceProperty = ResolvedTitleIconSourcePropertyKey.DependencyProperty;
		IsHeaderBackButtonVisibleProperty = DependencyProperty.Register("IsHeaderBackButtonVisible", typeof(bool), typeof(ListPageFrame), new PropertyMetadata((object)false));
		HeaderBackCommandProperty = DependencyProperty.Register("HeaderBackCommand", typeof(ICommand), typeof(ListPageFrame), new PropertyMetadata((PropertyChangedCallback)null));
		SearchTextProperty = DependencyProperty.Register("SearchText", typeof(string), typeof(ListPageFrame), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		IsSearchVisibleProperty = DependencyProperty.Register("IsSearchVisible", typeof(bool), typeof(ListPageFrame), new PropertyMetadata((object)true));
		SearchLeadingContentProperty = DependencyProperty.Register("SearchLeadingContent", typeof(object), typeof(ListPageFrame), new PropertyMetadata((PropertyChangedCallback)null));
		SearchLeadingContentTemplateProperty = DependencyProperty.Register("SearchLeadingContentTemplate", typeof(DataTemplate), typeof(ListPageFrame), new PropertyMetadata((PropertyChangedCallback)null));
		IsSearchLeadingContentVisibleProperty = DependencyProperty.Register("IsSearchLeadingContentVisible", typeof(bool), typeof(ListPageFrame), new PropertyMetadata((object)true));
		SearchTrailingContentProperty = DependencyProperty.Register("SearchTrailingContent", typeof(object), typeof(ListPageFrame), new PropertyMetadata((PropertyChangedCallback)null));
		SearchToolbarContentProperty = DependencyProperty.Register("SearchToolbarContent", typeof(object), typeof(ListPageFrame), new PropertyMetadata((PropertyChangedCallback)null));
		IsSearchToolbarVisibleProperty = DependencyProperty.Register("IsSearchToolbarVisible", typeof(bool), typeof(ListPageFrame), new PropertyMetadata((object)false));
		SearchToolbarContentTemplateProperty = DependencyProperty.Register("SearchToolbarContentTemplate", typeof(DataTemplate), typeof(ListPageFrame), new PropertyMetadata((PropertyChangedCallback)null));
		SearchFilterContentProperty = DependencyProperty.Register("SearchFilterContent", typeof(object), typeof(ListPageFrame), new PropertyMetadata((PropertyChangedCallback)null));
		IsSearchFilterVisibleProperty = DependencyProperty.Register("IsSearchFilterVisible", typeof(bool), typeof(ListPageFrame), new PropertyMetadata((object)false));
		SearchFilterContentTemplateProperty = DependencyProperty.Register("SearchFilterContentTemplate", typeof(DataTemplate), typeof(ListPageFrame), new PropertyMetadata((PropertyChangedCallback)null));
		IsListVisibleProperty = DependencyProperty.Register("IsListVisible", typeof(bool), typeof(ListPageFrame), new PropertyMetadata((object)true, new PropertyChangedCallback(OnListVisibilityChanged)));
		IsProgressiveBlurEnabledProperty = DependencyProperty.Register("IsProgressiveBlurEnabled", typeof(bool), typeof(ListPageFrame), new PropertyMetadata((object)false, new PropertyChangedCallback(OnProgressiveBlurEnabledChanged)));
		UseFrameScrollViewerProperty = DependencyProperty.Register("UseFrameScrollViewer", typeof(bool), typeof(ListPageFrame), new PropertyMetadata((object)true));
		OverlayContentProperty = DependencyProperty.Register("OverlayContent", typeof(object), typeof(ListPageFrame), new PropertyMetadata((PropertyChangedCallback)null));
		ListContentProperty = DependencyProperty.Register("ListContent", typeof(object), typeof(ListPageFrame), new PropertyMetadata((PropertyChangedCallback)null));
		FloatingContentProperty = DependencyProperty.Register("FloatingContent", typeof(object), typeof(ListPageFrame), new PropertyMetadata((PropertyChangedCallback)null));
	}
}
