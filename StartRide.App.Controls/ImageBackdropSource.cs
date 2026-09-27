using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StartRide.App.Controls;

public sealed class ImageBackdropSource : Border
{
	public static readonly DependencyProperty ImageSourceProperty;

	public static readonly DependencyProperty OverlayBrushProperty;

	public static readonly DependencyProperty OverlayOpacityProperty;

	public ImageSource? ImageSource
	{
		get
		{
			return (ImageSource)((DependencyObject)this).GetValue(ImageSourceProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ImageSourceProperty, (object)value);
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

	public double OverlayOpacity
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(OverlayOpacityProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(OverlayOpacityProperty, (object)value);
		}
	}

	public ImageBackdropSource()
	{
		base.IsHitTestVisible = false;
		base.ClipToBounds = true;
		base.Loaded += ImageBackdropSource_Loaded;
		base.Background = CreateImageBrush(null);
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		base.OnRender(drawingContext);
		Rect rectangle = new Rect(base.RenderSize);
		if (!(rectangle.Width <= 0.0) && !(rectangle.Height <= 0.0))
		{
			Brush overlayBrush = OverlayBrush;
			if (overlayBrush != null && !(OverlayOpacity <= 0.0))
			{
				drawingContext.PushOpacity(OverlayOpacity);
				drawingContext.DrawRectangle(overlayBrush, null, rectangle);
				drawingContext.Pop();
			}
		}
	}

	private static void OnImageSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is ImageBackdropSource imageBackdropSource)
		{
			imageBackdropSource.RefreshImageDrawing((ImageSource)e.NewValue);
		}
	}

	private void ImageBackdropSource_Loaded(object sender, RoutedEventArgs e)
	{
		RefreshImageDrawing(ImageSource);
	}

	private void RefreshImageDrawing(ImageSource? imageSource)
	{
		base.Background = CreateImageBrush(imageSource);
		InvalidateVisual();
	}

	private static ImageBrush CreateImageBrush(ImageSource? imageSource)
	{
		return new ImageBrush(imageSource)
		{
			AlignmentX = AlignmentX.Center,
			AlignmentY = AlignmentY.Center,
			Stretch = Stretch.UniformToFill
		};
	}

	private static bool IsValidOpacity(object value)
	{
		double num = (double)value;
		if (double.IsFinite(num))
		{
			if (num >= 0.0)
			{
				return num <= 1.0;
			}
			return false;
		}
		return false;
	}

	static ImageBackdropSource()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Expected O, but got Unknown
		ImageSourceProperty = DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(ImageBackdropSource), (PropertyMetadata)(object)new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnImageSourceChanged)));
		OverlayBrushProperty = DependencyProperty.Register("OverlayBrush", typeof(Brush), typeof(ImageBackdropSource), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)null, FrameworkPropertyMetadataOptions.AffectsRender));
		OverlayOpacityProperty = DependencyProperty.Register("OverlayOpacity", typeof(double), typeof(ImageBackdropSource), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)0.0, FrameworkPropertyMetadataOptions.AffectsRender), new ValidateValueCallback(IsValidOpacity));
	}
}
