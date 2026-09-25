using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Serilog;

namespace StartRide.App.Controls;

[TemplatePart(Name = "PART_BlurLayer", Type = typeof(Border))]
public sealed class BackdropBlurBorder : ContentControl
{
	private readonly record struct BackdropGeometrySnapshot(FrameworkElement? Source, Rect Viewbox, Rect Viewport, bool IsActive, bool HasRecursiveSource)
	{
		internal static BackdropGeometrySnapshot Inactive(FrameworkElement source, bool hasRecursiveSource = false)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return new BackdropGeometrySnapshot(source, Rect.Empty, Rect.Empty, IsActive: false, hasRecursiveSource);
		}
	}

	internal const string BlurLayerPartName = "PART_BlurLayer";

	private const double BlurOverscanFactor = 1.5;

	private const double LocalBlurFixedRenderScale = 0.2;

	private const double RenderScaleComparisonTolerance = 0.001;

	public static readonly DependencyProperty SourceElementProperty;

	public static readonly DependencyProperty BlurRadiusProperty;

	public static readonly DependencyProperty IsBlurEnabledProperty;

	public static readonly DependencyProperty IsSourcePreblurredProperty;

	public static readonly DependencyProperty IsTintEnabledProperty;

	public static readonly DependencyProperty BaseBrushProperty;

	public static readonly DependencyProperty TintBrushProperty;

	public static readonly DependencyProperty OverlayBrushProperty;

	public static readonly DependencyProperty BlurRenderingBiasProperty;

	public static readonly DependencyProperty CornerRadiusProperty;

	private Border? blurLayer;

	private TileBrush? backdropBrush;

	private BitmapCache? localBlurCache;

	private Rect lastViewbox;

	private Rect lastViewport;

	private bool isLoaded;

	private bool isRefreshTrackingActive;

	private bool recursiveSourceWarningLogged;

	private bool hasPreparedGeometry;

	private BackdropGeometrySnapshot preparedGeometry;

	private BackdropGeometrySnapshot lastAppliedGeometry;

	private BackdropBlurRefreshCoordinator? refreshCoordinator;

	private ScrollViewer? trackedScrollViewer;

	public FrameworkElement? SourceElement
	{
		get
		{
			return (FrameworkElement)((DependencyObject)this).GetValue(SourceElementProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SourceElementProperty, (object)value);
		}
	}

	public double BlurRadius
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(BlurRadiusProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(BlurRadiusProperty, (object)value);
		}
	}

	public bool IsBlurEnabled
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsBlurEnabledProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsBlurEnabledProperty, (object)value);
		}
	}

	public bool IsSourcePreblurred
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsSourcePreblurredProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsSourcePreblurredProperty, (object)value);
		}
	}

	public bool IsTintEnabled
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsTintEnabledProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsTintEnabledProperty, (object)value);
		}
	}

	public Brush? BaseBrush
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(BaseBrushProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(BaseBrushProperty, (object)value);
		}
	}

	public Brush? TintBrush
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(TintBrushProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TintBrushProperty, (object)value);
		}
	}

	public Brush? OverlayBrush
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(OverlayBrushProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(OverlayBrushProperty, (object)value);
		}
	}

	public RenderingBias BlurRenderingBias
	{
		get
		{
			return (RenderingBias)((DependencyObject)this).GetValue(BlurRenderingBiasProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(BlurRenderingBiasProperty, (object)value);
		}
	}

	public CornerRadius CornerRadius
	{
		get
		{
			return (CornerRadius)((DependencyObject)this).GetValue(CornerRadiusProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(CornerRadiusProperty, (object)value);
		}
	}

	internal VisualBrush? BackdropBrush => backdropBrush as VisualBrush;

	internal DrawingBrush? BackdropDrawingBrush => backdropBrush as DrawingBrush;

	internal BlurEffect? BackdropEffect => blurLayer?.Effect as BlurEffect;

	internal bool IsBackdropActive
	{
		get
		{
			Border? border = blurLayer;
			if (border == null)
			{
				return false;
			}
			return border.Visibility == Visibility.Visible;
		}
	}

	internal bool IsRenderTrackingActive => isRefreshTrackingActive;

	internal bool IsRefreshEligible
	{
		get
		{
			if (isLoaded && base.IsVisible && IsBlurEnabled)
			{
				return SourceElement != null;
			}
			return false;
		}
	}

	internal double BlurOverscan
	{
		get
		{
			if (!IsSourcePreblurred)
			{
				return CalculateBlurOverscan(BlurRadius);
			}
			return 0.0;
		}
	}

	internal double LocalBlurRenderScale => localBlurCache?.RenderAtScale ?? 1.0;

	internal bool IsUsingDrawingSource
	{
		get
		{
			if (backdropBrush is DrawingBrush drawingBrush)
			{
				return drawingBrush.Drawing != null;
			}
			return false;
		}
	}

	public BackdropBlurBorder()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		lastViewbox = Rect.Empty;
		lastViewport = Rect.Empty;
		base.Focusable = false;
		base.IsTabStop = false;
		base.Loaded += BackdropBlurBorder_Loaded;
		base.Unloaded += BackdropBlurBorder_Unloaded;
		base.IsVisibleChanged += new DependencyPropertyChangedEventHandler(BackdropBlurBorder_IsVisibleChanged);
		base.SizeChanged += BackdropBlurBorder_SizeChanged;
	}

	public override void OnApplyTemplate()
	{
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		ClearBackdropSource();
		base.OnApplyTemplate();
		blurLayer = GetTemplateChild("PART_BlurLayer") as Border;
		BitmapCache bitmapCache = blurLayer?.CacheMode as BitmapCache;
		localBlurCache = ((bitmapCache != null && ((Freezable)bitmapCache).IsFrozen) ? bitmapCache.CloneCurrentValue() : bitmapCache);
		if (blurLayer != null && localBlurCache != null && localBlurCache != bitmapCache)
		{
			blurLayer.CacheMode = localBlurCache;
		}
		VisualBrush visualBrush = blurLayer?.Background as VisualBrush;
		if (blurLayer != null && visualBrush != null)
		{
			backdropBrush = (((Freezable)visualBrush).IsFrozen ? visualBrush.CloneCurrentValue() : visualBrush);
			if (backdropBrush != visualBrush)
			{
				blurLayer.Background = backdropBrush;
			}
			backdropBrush.ViewboxUnits = BrushMappingMode.Absolute;
			backdropBrush.ViewportUnits = BrushMappingMode.Absolute;
			backdropBrush.TileMode = TileMode.FlipXY;
		}
		else
		{
			backdropBrush = null;
		}
		UpdateBlurLayerOverscan();
		lastViewbox = Rect.Empty;
		lastViewport = Rect.Empty;
		lastAppliedGeometry = default(BackdropGeometrySnapshot);
		InvalidatePreparedGeometry();
		RequestRefresh(BackdropBlurRefreshReason.Lifecycle);
	}

	protected override void OnVisualParentChanged(DependencyObject oldParent)
	{
		base.OnVisualParentChanged(oldParent);
		InvalidatePreparedGeometry();
		UpdateRefreshTracking();
		RequestRefresh(BackdropBlurRefreshReason.Layout);
	}

	internal bool RefreshBackdrop()
	{
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		if (blurLayer == null || backdropBrush == null)
		{
			return false;
		}
		BackdropGeometrySnapshot geometry = (hasPreparedGeometry ? preparedGeometry : BuildGeometrySnapshot());
		hasPreparedGeometry = false;
		if (!geometry.IsActive)
		{
			if (geometry.HasRecursiveSource)
			{
				FrameworkElement sourceElement = SourceElement;
				if (sourceElement != null)
				{
					LogRecursiveSourceOnce(sourceElement);
				}
			}
			DeactivateBackdrop(geometry);
			return false;
		}
		UpdateLocalBlurRenderScale();
		EnsureBackdropBrushForSource(geometry.Source);
		bool result = false;
		if (lastViewbox != geometry.Viewbox)
		{
			backdropBrush.Viewbox = geometry.Viewbox;
			lastViewbox = geometry.Viewbox;
			result = true;
		}
		if (lastViewport != geometry.Viewport)
		{
			backdropBrush.Viewport = geometry.Viewport;
			lastViewport = geometry.Viewport;
		}
		lastAppliedGeometry = geometry;
		blurLayer.Visibility = Visibility.Visible;
		return result;
	}

	internal bool PrepareLayoutGeometryRefresh()
	{
		BackdropGeometrySnapshot backdropGeometrySnapshot = BuildGeometrySnapshot();
		if (hasPreparedGeometry && preparedGeometry == backdropGeometrySnapshot)
		{
			return false;
		}
		preparedGeometry = backdropGeometrySnapshot;
		hasPreparedGeometry = true;
		return backdropGeometrySnapshot != lastAppliedGeometry;
	}

	internal void InvalidatePreparedGeometry()
	{
		hasPreparedGeometry = false;
		preparedGeometry = default(BackdropGeometrySnapshot);
	}

	private static void OnBackdropSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (dependencyObject is BackdropBlurBorder backdropBlurBorder)
		{
			backdropBlurBorder.recursiveSourceWarningLogged = false;
			backdropBlurBorder.lastViewbox = Rect.Empty;
			backdropBlurBorder.lastViewport = Rect.Empty;
			backdropBlurBorder.lastAppliedGeometry = default(BackdropGeometrySnapshot);
			backdropBlurBorder.InvalidatePreparedGeometry();
			backdropBlurBorder.UpdateRefreshTracking();
			backdropBlurBorder.RequestRefresh(BackdropBlurRefreshReason.Source);
		}
	}

	private static void OnBackdropPresentationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is BackdropBlurBorder backdropBlurBorder)
		{
			backdropBlurBorder.InvalidatePreparedGeometry();
			backdropBlurBorder.UpdateRefreshTracking();
			backdropBlurBorder.RequestRefresh(BackdropBlurRefreshReason.Lifecycle);
		}
	}

	private static void OnBlurRadiusChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		if (dependencyObject is BackdropBlurBorder backdropBlurBorder)
		{
			backdropBlurBorder.UpdateBlurLayerOverscan();
			backdropBlurBorder.lastViewbox = Rect.Empty;
			backdropBlurBorder.lastViewport = Rect.Empty;
			backdropBlurBorder.lastAppliedGeometry = default(BackdropGeometrySnapshot);
			backdropBlurBorder.InvalidatePreparedGeometry();
			backdropBlurBorder.RequestRefresh(BackdropBlurRefreshReason.Source);
		}
	}

	private static void OnIsSourcePreblurredChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		if (dependencyObject is BackdropBlurBorder backdropBlurBorder)
		{
			backdropBlurBorder.UpdateBlurLayerOverscan();
			backdropBlurBorder.lastViewbox = Rect.Empty;
			backdropBlurBorder.lastViewport = Rect.Empty;
			backdropBlurBorder.lastAppliedGeometry = default(BackdropGeometrySnapshot);
			backdropBlurBorder.InvalidatePreparedGeometry();
			backdropBlurBorder.UpdateRefreshTracking();
			backdropBlurBorder.RequestRefresh(BackdropBlurRefreshReason.Source);
		}
	}

	private void BackdropBlurBorder_Loaded(object sender, RoutedEventArgs e)
	{
		isLoaded = true;
		InvalidatePreparedGeometry();
		UpdateRefreshTracking();
		RequestRefresh(BackdropBlurRefreshReason.Lifecycle);
	}

	private void BackdropBlurBorder_Unloaded(object sender, RoutedEventArgs e)
	{
		isLoaded = false;
		InvalidatePreparedGeometry();
		StopRefreshTracking();
		DeactivateBackdrop();
	}

	private void BackdropBlurBorder_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		InvalidatePreparedGeometry();
		UpdateRefreshTracking();
		RequestRefresh(BackdropBlurRefreshReason.Lifecycle);
	}

	private void BackdropBlurBorder_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		InvalidatePreparedGeometry();
		RequestRefresh(BackdropBlurRefreshReason.Size);
	}

	private void UpdateRefreshTracking()
	{
		if (!IsRefreshEligible)
		{
			StopRefreshTracking();
			DeactivateBackdrop();
			return;
		}
		BackdropBlurRefreshCoordinator backdropBlurRefreshCoordinator = BackdropBlurRefreshCoordinator.TryGet(this);
		if (backdropBlurRefreshCoordinator == null)
		{
			StopRefreshTracking();
			return;
		}
		if (refreshCoordinator != backdropBlurRefreshCoordinator)
		{
			refreshCoordinator?.Unregister(this);
			refreshCoordinator = backdropBlurRefreshCoordinator;
		}
		trackedScrollViewer = FindNearestScrollViewer();
		refreshCoordinator.Register(this, SourceElement, trackedScrollViewer);
		isRefreshTrackingActive = true;
	}

	private void StopRefreshTracking()
	{
		refreshCoordinator?.Unregister(this);
		refreshCoordinator = null;
		trackedScrollViewer = null;
		isRefreshTrackingActive = false;
	}

	private ScrollViewer? FindNearestScrollViewer()
	{
		DependencyObject val = (DependencyObject)(object)this;
		while ((val = VisualTreeHelper.GetParent(val)) != null)
		{
			if (val is ScrollViewer result)
			{
				return result;
			}
		}
		return null;
	}

	private BackdropGeometrySnapshot BuildGeometrySnapshot()
	{
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		if (blurLayer != null && backdropBrush != null && IsBlurEnabled && base.IsVisible && !(base.ActualWidth <= 0.0) && !(base.ActualHeight <= 0.0))
		{
			FrameworkElement sourceElement = SourceElement;
			if (sourceElement != null && !(sourceElement.ActualWidth <= 0.0) && !(sourceElement.ActualHeight <= 0.0))
			{
				if (sourceElement == this || sourceElement.IsAncestorOf((DependencyObject)(object)this))
				{
					return BackdropGeometrySnapshot.Inactive(sourceElement, hasRecursiveSource: true);
				}
				Rect val;
				try
				{
					double blurOverscan = BlurOverscan;
					val = TransformToVisual(sourceElement).TransformBounds(new Rect(0.0 - blurOverscan, 0.0 - blurOverscan, base.ActualWidth + blurOverscan * 2.0, base.ActualHeight + blurOverscan * 2.0));
				}
				catch (InvalidOperationException)
				{
					return BackdropGeometrySnapshot.Inactive(sourceElement);
				}
				if (!IsValidViewbox(val))
				{
					return BackdropGeometrySnapshot.Inactive(sourceElement);
				}
				Rect viewbox = Rect.Intersect(val, new Rect(0.0, 0.0, sourceElement.ActualWidth, sourceElement.ActualHeight));
				viewbox = ClipToScrollViewport(viewbox, sourceElement);
				if (!IsValidViewbox(viewbox))
				{
					return BackdropGeometrySnapshot.Inactive(sourceElement);
				}
				double num = BlurOverscan * 2.0;
				Rect val2 = CalculateMirroredViewport(val, viewbox, base.ActualWidth + num, base.ActualHeight + num);
				if (!IsValidViewbox(val2))
				{
					return BackdropGeometrySnapshot.Inactive(sourceElement);
				}
				return new BackdropGeometrySnapshot(sourceElement, viewbox, val2, IsActive: true, HasRecursiveSource: false);
			}
		}
		return default(BackdropGeometrySnapshot);
	}

	private Rect ClipToScrollViewport(Rect viewbox, FrameworkElement source)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		ScrollViewer scrollViewer = trackedScrollViewer;
		if (scrollViewer == null || scrollViewer.ActualWidth <= 0.0 || scrollViewer.ActualHeight <= 0.0)
		{
			return viewbox;
		}
		try
		{
			Rect val = scrollViewer.TransformToVisual(source).TransformBounds(new Rect(0.0, 0.0, scrollViewer.ActualWidth, scrollViewer.ActualHeight));
			return Rect.Intersect(viewbox, val);
		}
		catch (InvalidOperationException)
		{
			return Rect.Empty;
		}
	}

	private void RequestRefresh(BackdropBlurRefreshReason reason)
	{
		if (isRefreshTrackingActive && refreshCoordinator != null)
		{
			refreshCoordinator.RequestRefresh(this, reason);
		}
	}

	private void UpdateBlurLayerOverscan()
	{
		if (blurLayer != null)
		{
			double blurOverscan = BlurOverscan;
			blurLayer.Margin = new Thickness(0.0 - blurOverscan);
		}
	}

	private void UpdateLocalBlurRenderScale()
	{
		if (!IsSourcePreblurred)
		{
			BitmapCache bitmapCache = localBlurCache;
			if (bitmapCache != null && Math.Abs(bitmapCache.RenderAtScale - 0.2) > 0.001)
			{
				bitmapCache.RenderAtScale = 0.2;
			}
		}
	}

	private void EnsureBackdropBrushForSource(FrameworkElement source)
	{
		if (blurLayer != null && (!(backdropBrush is VisualBrush visualBrush) || visualBrush.Visual != source))
		{
			VisualBrush visualBrush2 = CreateBackdropBrush<VisualBrush>();
			visualBrush2.Visual = source;
			ReplaceBackdropBrush(visualBrush2);
		}
	}

	private void ReplaceBackdropBrush(TileBrush replacement)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		ClearBackdropSource();
		backdropBrush = replacement;
		if (blurLayer != null)
		{
			blurLayer.Background = replacement;
		}
		lastViewbox = Rect.Empty;
		lastViewport = Rect.Empty;
	}

	private static TBrush CreateBackdropBrush<TBrush>() where TBrush : TileBrush, new()
	{
		TBrush obj = new TBrush
		{
			AlignmentX = AlignmentX.Left,
			AlignmentY = AlignmentY.Top,
			Stretch = Stretch.Fill,
			TileMode = TileMode.FlipXY,
			ViewboxUnits = BrushMappingMode.Absolute,
			ViewportUnits = BrushMappingMode.Absolute
		};
		RenderOptions.SetCachingHint((DependencyObject)(object)obj, CachingHint.Cache);
		RenderOptions.SetCacheInvalidationThresholdMinimum((DependencyObject)(object)obj, 0.5);
		RenderOptions.SetCacheInvalidationThresholdMaximum((DependencyObject)(object)obj, 2.0);
		return obj;
	}

	private void ClearBackdropSource()
	{
		TileBrush tileBrush = backdropBrush;
		if (!(tileBrush is VisualBrush visualBrush))
		{
			if (tileBrush is DrawingBrush drawingBrush)
			{
				drawingBrush.Drawing = null;
			}
		}
		else
		{
			visualBrush.Visual = null;
		}
	}

	private void DeactivateBackdrop(BackdropGeometrySnapshot geometry = default(BackdropGeometrySnapshot))
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		ClearBackdropSource();
		InvalidatePreparedGeometry();
		lastAppliedGeometry = geometry;
		lastViewbox = Rect.Empty;
		lastViewport = Rect.Empty;
		if (blurLayer != null)
		{
			blurLayer.Visibility = Visibility.Collapsed;
		}
	}

	private void LogRecursiveSourceOnce(FrameworkElement source)
	{
		if (!recursiveSourceWarningLogged)
		{
			recursiveSourceWarningLogged = true;
			Log.Warning("Backdrop blur source contains the blur control and cannot be sampled safely. SourceType={SourceType}", ((object)source).GetType().FullName);
		}
	}

	private static bool IsValidViewbox(Rect viewbox)
	{
		if (!viewbox.IsEmpty && double.IsFinite(viewbox.X) && double.IsFinite(viewbox.Y) && double.IsFinite(viewbox.Width) && double.IsFinite(viewbox.Height) && viewbox.Width > 0.0)
		{
			return viewbox.Height > 0.0;
		}
		return false;
	}

	private static Rect CalculateMirroredViewport(Rect desiredViewbox, Rect clippedViewbox, double destinationWidth, double destinationHeight)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		double num = destinationWidth / desiredViewbox.Width;
		double num2 = destinationHeight / desiredViewbox.Height;
		return new Rect(Math.Max(0.0, (clippedViewbox.Left - desiredViewbox.Left) * num), Math.Max(0.0, (clippedViewbox.Top - desiredViewbox.Top) * num2), Math.Min(destinationWidth, clippedViewbox.Width * num), Math.Min(destinationHeight, clippedViewbox.Height * num2));
	}

	private static bool IsNonNegativeFiniteDouble(object value)
	{
		double num = (double)value;
		if (double.IsFinite(num))
		{
			return num >= 0.0;
		}
		return false;
	}

	private static bool IsRenderingBiasValid(object value)
	{
		if (value is RenderingBias renderingBias && (uint)renderingBias <= 1u)
		{
			return true;
		}
		return false;
	}

	private static bool IsCornerRadiusValid(object value)
	{
		CornerRadius cornerRadius = (CornerRadius)value;
		if (IsNonNegativeFinite(cornerRadius.TopLeft) && IsNonNegativeFinite(cornerRadius.TopRight) && IsNonNegativeFinite(cornerRadius.BottomRight))
		{
			return IsNonNegativeFinite(cornerRadius.BottomLeft);
		}
		return false;
	}

	private static bool IsNonNegativeFinite(double value)
	{
		if (double.IsFinite(value))
		{
			return value >= 0.0;
		}
		return false;
	}

	private static double CalculateBlurOverscan(double blurRadius)
	{
		return Math.Ceiling(blurRadius * 1.5);
	}

	static BackdropBlurBorder()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected O, but got Unknown
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Expected O, but got Unknown
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Expected O, but got Unknown
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Expected O, but got Unknown
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Expected O, but got Unknown
		SourceElementProperty = DependencyProperty.Register("SourceElement", typeof(FrameworkElement), typeof(BackdropBlurBorder), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)null, new PropertyChangedCallback(OnBackdropSourceChanged)));
		BlurRadiusProperty = DependencyProperty.Register("BlurRadius", typeof(double), typeof(BackdropBlurBorder), (PropertyMetadata)(object)new FrameworkPropertyMetadata(42.0, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnBlurRadiusChanged)), new ValidateValueCallback(IsNonNegativeFiniteDouble));
		IsBlurEnabledProperty = DependencyProperty.Register("IsBlurEnabled", typeof(bool), typeof(BackdropBlurBorder), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)true, new PropertyChangedCallback(OnBackdropPresentationChanged)));
		IsSourcePreblurredProperty = DependencyProperty.Register("IsSourcePreblurred", typeof(bool), typeof(BackdropBlurBorder), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)false, new PropertyChangedCallback(OnIsSourcePreblurredChanged)));
		IsTintEnabledProperty = DependencyProperty.Register("IsTintEnabled", typeof(bool), typeof(BackdropBlurBorder), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)false));
		BaseBrushProperty = DependencyProperty.Register("BaseBrush", typeof(Brush), typeof(BackdropBlurBorder), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)null, FrameworkPropertyMetadataOptions.AffectsRender));
		TintBrushProperty = DependencyProperty.Register("TintBrush", typeof(Brush), typeof(BackdropBlurBorder), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)null, FrameworkPropertyMetadataOptions.AffectsRender));
		OverlayBrushProperty = DependencyProperty.Register("OverlayBrush", typeof(Brush), typeof(BackdropBlurBorder), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)null, FrameworkPropertyMetadataOptions.AffectsRender));
		BlurRenderingBiasProperty = DependencyProperty.Register("BlurRenderingBias", typeof(RenderingBias), typeof(BackdropBlurBorder), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)RenderingBias.Performance, FrameworkPropertyMetadataOptions.AffectsRender), new ValidateValueCallback(IsRenderingBiasValid));
		CornerRadiusProperty = DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(BackdropBlurBorder), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)default(CornerRadius), FrameworkPropertyMetadataOptions.AffectsRender), new ValidateValueCallback(IsCornerRadiusValid));
	}
}
