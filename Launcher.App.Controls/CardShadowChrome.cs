using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Launcher.App.Controls;

internal sealed class CardShadowChrome : FrameworkElement
{
	private const int MaximumBandCount = 24;

	private const double MinimumBandCount = 4.0;

	private const int MinimumRenderCacheTier = 2;

	private const long MaximumRenderCacheBytes = 8388608L;

	internal static readonly DependencyProperty ReferenceEffectProperty;

	internal static readonly DependencyProperty CornerRadiusProperty;

	internal static readonly DependencyProperty SurfaceBrushProperty;

	internal static readonly DependencyProperty SurfaceBorderBrushProperty;

	internal static readonly DependencyProperty TintBrushProperty;

	internal static readonly DependencyProperty OverlayBrushProperty;

	internal static readonly DependencyProperty IsBackdropBlurEnabledProperty;

	internal static readonly DependencyProperty BackdropSourceProperty;

	private DrawingGroup? drawing;

	private DropShadowEffect? subscribedEffect;

	internal DropShadowEffect? ReferenceEffect
	{
		get
		{
			return (DropShadowEffect)((DependencyObject)this).GetValue(ReferenceEffectProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ReferenceEffectProperty, (object)value);
		}
	}

	internal CornerRadius CornerRadius
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

	internal Brush? SurfaceBrush
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(SurfaceBrushProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SurfaceBrushProperty, (object)value);
		}
	}

	internal Brush? SurfaceBorderBrush
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(SurfaceBorderBrushProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SurfaceBorderBrushProperty, (object)value);
		}
	}

	internal Brush? TintBrush
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

	internal Brush? OverlayBrush
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

	internal bool IsBackdropBlurEnabled
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsBackdropBlurEnabledProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsBackdropBlurEnabledProperty, (object)value);
		}
	}

	internal FrameworkElement? BackdropSource
	{
		get
		{
			return (FrameworkElement)((DependencyObject)this).GetValue(BackdropSourceProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(BackdropSourceProperty, (object)value);
		}
	}

	internal int DrawingBuildCount { get; private set; }

	internal int DrawingPrimitiveCount => drawing?.Children.Count ?? 0;

	internal CardShadowChrome()
	{
		base.Focusable = false;
		base.IsHitTestVisible = false;
	}

	private void ApplyRenderCache()
	{
		double dpiScaleX = VisualTreeHelper.GetDpi(this).DpiScaleX;
		bool flag = ShouldUseRenderCache(RenderCapability.Tier >> 16, base.ActualWidth, base.ActualHeight, dpiScaleX);
		if (flag != base.CacheMode is BitmapCache)
		{
			base.CacheMode = (flag ? new BitmapCache
			{
				EnableClearType = false,
				RenderAtScale = 1.0,
				SnapsToDevicePixels = true
			} : null);
		}
	}

	internal static bool ShouldUseRenderCache(int renderingTier, double width, double height, double dpiScale)
	{
		if (renderingTier < 2)
		{
			return false;
		}
		if (width <= 0.0 || height <= 0.0 || dpiScale <= 0.0)
		{
			return false;
		}
		long num = (long)Math.Ceiling(width * dpiScale);
		long num2 = (long)Math.Ceiling(height * dpiScale);
		if (num > 2097152 / Math.Max(num2, 1L))
		{
			return false;
		}
		return num * num2 * 4 <= 8388608;
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		base.OnRender(drawingContext);
		if (drawing == null)
		{
			drawing = BuildDrawing();
		}
		if (drawing != null)
		{
			drawingContext.DrawDrawing(drawing);
		}
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		base.OnRenderSizeChanged(sizeInfo);
		InvalidateDrawing();
		ApplyRenderCache();
	}

	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
	{
		base.OnDpiChanged(oldDpi, newDpi);
		InvalidateDrawing();
		ApplyRenderCache();
	}

	private DrawingGroup? BuildDrawing()
	{
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		DrawingBuildCount++;
		DropShadowEffect referenceEffect = ReferenceEffect;
		if (referenceEffect == null || base.ActualWidth <= 0.0 || base.ActualHeight <= 0.0 || referenceEffect.Opacity <= 0.0)
		{
			return null;
		}
		double backgroundAlpha = CompositeAlpha(CompositeAlpha((IsBackdropBlurEnabled && BackdropSource != null) ? 1.0 : ResolveBrushAlpha(SurfaceBrush), ResolveBrushAlpha(TintBrush)), ResolveBrushAlpha(OverlayBrush));
		double foregroundAlpha = ResolveBrushAlpha(SurfaceBorderBrush);
		double num = CompositeAlpha(backgroundAlpha, foregroundAlpha);
		double num2 = Math.Clamp(referenceEffect.Opacity * ((double)(int)referenceEffect.Color.A / 255.0) * num, 0.0, 1.0);
		if (num2 <= 0.0)
		{
			return null;
		}
		double num3 = referenceEffect.Direction * Math.PI / 180.0;
		double num4 = referenceEffect.ShadowDepth * Math.Cos(num3);
		double num5 = (0.0 - referenceEffect.ShadowDepth) * Math.Sin(num3);
		Rect rect = new Rect(num4, num5, base.ActualWidth, base.ActualHeight);
		double num6 = ResolveUniformCornerRadius(CornerRadius);
		DrawingGroup drawingGroup = new DrawingGroup();
		AddGeometryDrawing(drawingGroup, CreateRoundedRectangleGeometry(rect, num6), CreateFrozenBrush(referenceEffect.Color, num2));
		double num7 = Math.Max(0.0, referenceEffect.BlurRadius);
		if (num7 > 0.0)
		{
			int num8 = Math.Clamp((int)Math.Ceiling(num7), 4, 24);
			double num9 = num7 / (double)num8;
			double num10 = Math.Max(num7 / 3.0, 0.01);
			for (int i = 0; i < num8; i++)
			{
				double num11 = (double)i * num9;
				double num12 = (double)(i + 1) * num9;
				double num13 = (num11 + num12) / 2.0;
				double num14 = num2 * EvaluateNormalTail(num13 / num10);
				if (!(num14 < 0.0009765625))
				{
					RectangleGeometry geometry = CreateRoundedRectangleGeometry(Inflate(rect, num12), num6 + num12);
					RectangleGeometry geometry2 = CreateRoundedRectangleGeometry(Inflate(rect, num11), num6 + num11);
					PathGeometry pathGeometry = Geometry.Combine(geometry, geometry2, GeometryCombineMode.Exclude, null);
					if (((Freezable)pathGeometry).CanFreeze)
					{
						((Freezable)pathGeometry).Freeze();
					}
					AddGeometryDrawing(drawingGroup, pathGeometry, CreateFrozenBrush(referenceEffect.Color, num14));
				}
			}
		}
		if (((Freezable)drawingGroup).CanFreeze)
		{
			((Freezable)drawingGroup).Freeze();
		}
		return drawingGroup;
	}

	private static void AddGeometryDrawing(DrawingGroup group, Geometry geometry, Brush brush)
	{
		GeometryDrawing geometryDrawing = new GeometryDrawing(brush, null, geometry);
		if (((Freezable)geometryDrawing).CanFreeze)
		{
			((Freezable)geometryDrawing).Freeze();
		}
		group.Children.Add(geometryDrawing);
	}

	private static SolidColorBrush CreateFrozenBrush(Color color, double opacity)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp((int)Math.Round(opacity * 255.0), 0, 255), color.R, color.G, color.B));
		((Freezable)solidColorBrush).Freeze();
		return solidColorBrush;
	}

	private static RectangleGeometry CreateRoundedRectangleGeometry(Rect rect, double radius)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		RectangleGeometry rectangleGeometry = new RectangleGeometry(rect, radius, radius);
		if (((Freezable)rectangleGeometry).CanFreeze)
		{
			((Freezable)rectangleGeometry).Freeze();
		}
		return rectangleGeometry;
	}

	private static Rect Inflate(Rect rect, double amount)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		rect.Inflate(amount, amount);
		return rect;
	}

	private static double ResolveUniformCornerRadius(CornerRadius radius)
	{
		return Math.Max(0.0, Math.Max(Math.Max(radius.TopLeft, radius.TopRight), Math.Max(radius.BottomRight, radius.BottomLeft)));
	}

	private static double ResolveBrushAlpha(Brush? brush)
	{
		if (brush == null)
		{
			return 0.0;
		}
		return Math.Clamp(((brush is SolidColorBrush { Color: var color }) ? ((double)(int)color.A / 255.0) : 1.0) * brush.Opacity, 0.0, 1.0);
	}

	private static double CompositeAlpha(double backgroundAlpha, double foregroundAlpha)
	{
		return 1.0 - (1.0 - backgroundAlpha) * (1.0 - foregroundAlpha);
	}

	private static double EvaluateNormalTail(double value)
	{
		double num = 1.0 / (1.0 + 0.2316419 * Math.Max(0.0, value));
		double num2 = num * (0.31938153 + num * (-0.356563782 + num * (1.781477937 + num * (-1.821255978 + num * 1.330274429))));
		return Math.Clamp(0.3989422804014327 * Math.Exp(-0.5 * value * value) * num2, 0.0, 0.5);
	}

	private static void OnReferenceEffectChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
	{
		CardShadowChrome obj = (CardShadowChrome)(object)dependencyObject;
		obj.ReplaceEffectSubscription(args.NewValue as DropShadowEffect);
		obj.InvalidateDrawing();
	}

	private static void OnDrawingPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
	{
		((CardShadowChrome)(object)dependencyObject).InvalidateDrawing();
	}

	private void ReplaceEffectSubscription(DropShadowEffect? effect)
	{
		DropShadowEffect dropShadowEffect = subscribedEffect;
		if (dropShadowEffect != null && !((Freezable)dropShadowEffect).IsFrozen)
		{
			((Freezable)subscribedEffect).Changed -= ReferenceEffect_Changed;
		}
		subscribedEffect = effect;
		dropShadowEffect = subscribedEffect;
		if (dropShadowEffect != null && !((Freezable)dropShadowEffect).IsFrozen)
		{
			((Freezable)subscribedEffect).Changed += ReferenceEffect_Changed;
		}
	}

	private void ReferenceEffect_Changed(object? sender, EventArgs e)
	{
		InvalidateDrawing();
	}

	private void InvalidateDrawing()
	{
		drawing = null;
		InvalidateVisual();
	}

	static CardShadowChrome()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected O, but got Unknown
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Expected O, but got Unknown
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Expected O, but got Unknown
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Expected O, but got Unknown
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Expected O, but got Unknown
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Expected O, but got Unknown
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Expected O, but got Unknown
		ReferenceEffectProperty = DependencyProperty.Register("ReferenceEffect", typeof(DropShadowEffect), typeof(CardShadowChrome), (PropertyMetadata)(object)new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnReferenceEffectChanged)));
		CornerRadiusProperty = DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(CardShadowChrome), (PropertyMetadata)(object)new FrameworkPropertyMetadata(default(CornerRadius), FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnDrawingPropertyChanged)));
		SurfaceBrushProperty = DependencyProperty.Register("SurfaceBrush", typeof(Brush), typeof(CardShadowChrome), (PropertyMetadata)(object)new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnDrawingPropertyChanged)));
		SurfaceBorderBrushProperty = DependencyProperty.Register("SurfaceBorderBrush", typeof(Brush), typeof(CardShadowChrome), (PropertyMetadata)(object)new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnDrawingPropertyChanged)));
		TintBrushProperty = DependencyProperty.Register("TintBrush", typeof(Brush), typeof(CardShadowChrome), (PropertyMetadata)(object)new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnDrawingPropertyChanged)));
		OverlayBrushProperty = DependencyProperty.Register("OverlayBrush", typeof(Brush), typeof(CardShadowChrome), (PropertyMetadata)(object)new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnDrawingPropertyChanged)));
		IsBackdropBlurEnabledProperty = DependencyProperty.Register("IsBackdropBlurEnabled", typeof(bool), typeof(CardShadowChrome), (PropertyMetadata)(object)new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnDrawingPropertyChanged)));
		BackdropSourceProperty = DependencyProperty.Register("BackdropSource", typeof(FrameworkElement), typeof(CardShadowChrome), (PropertyMetadata)(object)new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnDrawingPropertyChanged)));
	}
}
