using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using StartRide.App.Controls;
using StartRide.App.Converters;
using Serilog;

namespace StartRide.App.Behaviors;

public static class BackdropBlurHost
{
	private sealed class BlurClipRadiusConverter : IMultiValueConverter
	{
		internal static BlurClipRadiusConverter Instance { get; } = new BlurClipRadiusConverter();

		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values != null && values.Length == 3 && values[0] is CornerRadius radius && values[1] is Thickness border)
			{
				object obj = values[2];
				if (obj is bool)
				{
					return ((bool)obj) ? InnerCornerRadiusConverter.Deflate(radius, border).TopLeft : 0.0;
				}
			}
			return 0.0;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public static readonly DependencyProperty IsAppliedProperty;

	public static readonly DependencyProperty IsBlurEnabledProperty;

	public static readonly DependencyProperty IsBlurSuppressedProperty;

	public static readonly DependencyProperty FallbackBrushProperty;

	public static readonly DependencyProperty LightweightShadowEffectProperty;

	private static readonly DependencyProperty BackdropProperty;

	private static readonly DependencyProperty ShadowChromeProperty;

	public static bool GetIsApplied(DependencyObject element)
	{
		return (bool)element.GetValue(IsAppliedProperty);
	}

	public static void SetIsApplied(DependencyObject element, bool value)
	{
		element.SetValue(IsAppliedProperty, (object)value);
	}

	public static bool GetIsBlurEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsBlurEnabledProperty);
	}

	public static void SetIsBlurEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsBlurEnabledProperty, (object)value);
	}

	public static bool GetIsBlurSuppressed(DependencyObject element)
	{
		return (bool)element.GetValue(IsBlurSuppressedProperty);
	}

	public static void SetIsBlurSuppressed(DependencyObject element, bool value)
	{
		element.SetValue(IsBlurSuppressedProperty, (object)value);
	}

	public static Brush? GetFallbackBrush(DependencyObject element)
	{
		return (Brush)element.GetValue(FallbackBrushProperty);
	}

	public static void SetFallbackBrush(DependencyObject element, Brush? value)
	{
		element.SetValue(FallbackBrushProperty, (object)value);
	}

	public static DropShadowEffect? GetLightweightShadowEffect(DependencyObject element)
	{
		return (DropShadowEffect)element.GetValue(LightweightShadowEffectProperty);
	}

	public static void SetLightweightShadowEffect(DependencyObject element, DropShadowEffect? value)
	{
		element.SetValue(LightweightShadowEffectProperty, (object)value);
	}

	private static void OnIsAppliedChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (!(dependencyObject is Border border))
		{
			return;
		}
		border.Loaded -= Border_Loaded;
		object newValue = e.NewValue;
		if (newValue is bool && (bool)newValue)
		{
			border.Loaded += Border_Loaded;
			if (border.IsLoaded)
			{
				ApplyBackdrop(border);
			}
		}
		else if (((DependencyObject)border).GetValue(BackdropProperty) is BackdropBlurBorder backdropBlurBorder)
		{
			backdropBlurBorder.IsBlurEnabled = false;
		}
	}

	private static void OnIsBlurSuppressedChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (!(dependencyObject is Border border) || !GetIsApplied((DependencyObject)(object)border))
		{
			return;
		}
		object newValue = e.NewValue;
		if (newValue is bool && (bool)newValue)
		{
			if (((DependencyObject)border).GetValue(BackdropProperty) is BackdropBlurBorder backdropBlurBorder)
			{
				BindingOperations.ClearBinding((DependencyObject)(object)backdropBlurBorder, BackdropBlurBorder.IsBlurEnabledProperty);
				backdropBlurBorder.IsBlurEnabled = false;
			}
		}
		else if (((DependencyObject)border).GetValue(BackdropProperty) is BackdropBlurBorder backdrop)
		{
			BindBlurEnabled(backdrop, border);
		}
		else if (border.IsLoaded)
		{
			ApplyBackdrop(border);
		}
	}

	private static void OnLightweightShadowEffectChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is Border border && ((DependencyObject)border).GetValue(BackdropProperty) is BackdropBlurBorder backdrop)
		{
			UpdateLightweightShadow(border, backdrop);
		}
	}

	private static void Border_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is Border border)
		{
			ApplyBackdrop(border);
		}
	}

	private static void ApplyBackdrop(Border border)
	{
		if (!(((DependencyObject)border).GetValue(BackdropProperty) is BackdropBlurBorder))
		{
			UIElement child = border.Child;
			if (child != null)
			{
				border.Child = null;
			}
			BackdropBlurBorder backdropBlurBorder = new BackdropBlurBorder
			{
				IsHitTestVisible = false
			};
			backdropBlurBorder.SetResourceReference(FrameworkElement.StyleProperty, "SurfaceBackdropBlurStyle");
			BindBlurEnabled(backdropBlurBorder, border);
			BindingOperations.SetBinding((DependencyObject)(object)backdropBlurBorder, BackdropBlurBorder.CornerRadiusProperty, CreateInnerRadiusBinding(border));
			BindingOperations.SetBinding((DependencyObject)(object)backdropBlurBorder, RoundedClip.RadiusProperty, CreateBlurClipRadiusBinding(backdropBlurBorder, border));
			Grid grid = new Grid();
			grid.Children.Add(backdropBlurBorder);
			if (child != null)
			{
				Border element = new Border
				{
					Background = Brushes.Transparent,
					Padding = border.Padding,
					Child = child
				};
				SetIsBlurSuppressed((DependencyObject)(object)element, value: true);
				grid.Children.Add(element);
			}
			border.Padding = default(Thickness);
			border.Background = Brushes.Transparent;
			border.Child = grid;
			((DependencyObject)border).SetValue(BackdropProperty, (object)backdropBlurBorder);
			UpdateLightweightShadow(border, backdropBlurBorder);
		}
	}

	private static void UpdateLightweightShadow(Border border, BackdropBlurBorder backdrop)
	{
		DropShadowEffect? lightweightShadowEffect = GetLightweightShadowEffect((DependencyObject)(object)border);
		CardShadowChrome cardShadowChrome = ((DependencyObject)border).GetValue(ShadowChromeProperty) as CardShadowChrome;
		if (lightweightShadowEffect == null)
		{
			if (cardShadowChrome?.Parent is Panel panel)
			{
				panel.Children.Remove(cardShadowChrome);
			}
			if (cardShadowChrome != null)
			{
				BindingOperations.ClearBinding((DependencyObject)(object)cardShadowChrome, CardShadowChrome.ReferenceEffectProperty);
				BindingOperations.ClearBinding((DependencyObject)(object)cardShadowChrome, CardShadowChrome.CornerRadiusProperty);
				BindingOperations.ClearBinding((DependencyObject)(object)cardShadowChrome, CardShadowChrome.SurfaceBrushProperty);
				BindingOperations.ClearBinding((DependencyObject)(object)cardShadowChrome, CardShadowChrome.SurfaceBorderBrushProperty);
				BindingOperations.ClearBinding((DependencyObject)(object)cardShadowChrome, CardShadowChrome.TintBrushProperty);
				BindingOperations.ClearBinding((DependencyObject)(object)cardShadowChrome, CardShadowChrome.OverlayBrushProperty);
				BindingOperations.ClearBinding((DependencyObject)(object)cardShadowChrome, CardShadowChrome.IsBackdropBlurEnabledProperty);
				BindingOperations.ClearBinding((DependencyObject)(object)cardShadowChrome, CardShadowChrome.BackdropSourceProperty);
				((DependencyObject)border).ClearValue(ShadowChromeProperty);
			}
		}
		else
		{
			if (cardShadowChrome != null)
			{
				return;
			}
			try
			{
				if (!(backdrop.Parent is Grid grid))
				{
					throw new InvalidOperationException("The surface backdrop is not hosted by the expected layer grid.");
				}
				Thickness borderThickness = border.BorderThickness;
				CardShadowChrome cardShadowChrome2 = new CardShadowChrome
				{
					Margin = new Thickness(0.0 - borderThickness.Left, 0.0 - borderThickness.Top, 0.0 - borderThickness.Right, 0.0 - borderThickness.Bottom)
				};
				BindingOperations.SetBinding((DependencyObject)(object)cardShadowChrome2, CardShadowChrome.ReferenceEffectProperty, new Binding
				{
					Source = border,
					Path = new PropertyPath("(0)", LightweightShadowEffectProperty)
				});
				BindingOperations.SetBinding((DependencyObject)(object)cardShadowChrome2, CardShadowChrome.CornerRadiusProperty, CreateOuterRadiusBinding(border));
				BindingOperations.SetBinding((DependencyObject)(object)cardShadowChrome2, CardShadowChrome.SurfaceBrushProperty, new Binding("BaseBrush")
				{
					Source = backdrop
				});
				BindingOperations.SetBinding((DependencyObject)(object)cardShadowChrome2, CardShadowChrome.SurfaceBorderBrushProperty, new Binding("BorderBrush")
				{
					Source = border
				});
				BindingOperations.SetBinding((DependencyObject)(object)cardShadowChrome2, CardShadowChrome.TintBrushProperty, new Binding("TintBrush")
				{
					Source = backdrop
				});
				BindingOperations.SetBinding((DependencyObject)(object)cardShadowChrome2, CardShadowChrome.OverlayBrushProperty, new Binding("OverlayBrush")
				{
					Source = backdrop
				});
				BindingOperations.SetBinding((DependencyObject)(object)cardShadowChrome2, CardShadowChrome.IsBackdropBlurEnabledProperty, new Binding("IsBlurEnabled")
				{
					Source = backdrop
				});
				BindingOperations.SetBinding((DependencyObject)(object)cardShadowChrome2, CardShadowChrome.BackdropSourceProperty, new Binding("SourceElement")
				{
					Source = backdrop
				});
				grid.Children.Insert(0, cardShadowChrome2);
				((DependencyObject)border).SetValue(ShadowChromeProperty, (object)cardShadowChrome2);
				((DependencyObject)border).ClearValue(UIElement.EffectProperty);
			}
			catch (Exception exception)
			{
				Log.Warning(exception, "Failed to initialize the lightweight card shadow. The card shadow will remain disabled.");
				((DependencyObject)border).ClearValue(UIElement.EffectProperty);
			}
		}
	}

	private static MultiBinding CreateInnerRadiusBinding(Border border)
	{
		return CreateRadiusBinding(border, null);
	}

	private static MultiBinding CreateOuterRadiusBinding(Border border)
	{
		return CreateRadiusBinding(border, "Outer");
	}

	private static MultiBinding CreateBlurClipRadiusBinding(BackdropBlurBorder backdrop, Border border)
	{
		return new MultiBinding
		{
			Converter = BlurClipRadiusConverter.Instance,
			Bindings = 
			{
				(BindingBase)new Binding("CornerRadius")
				{
					Source = border
				},
				(BindingBase)new Binding("BorderThickness")
				{
					Source = border
				},
				(BindingBase)new Binding("IsBlurEnabled")
				{
					Source = backdrop
				}
			}
		};
	}

	private static MultiBinding CreateRadiusBinding(Border border, string? parameter)
	{
		return new MultiBinding
		{
			Converter = InnerCornerRadiusConverter.Instance,
			ConverterParameter = parameter,
			Bindings = 
			{
				(BindingBase)new Binding("CornerRadius")
				{
					Source = border
				},
				(BindingBase)new Binding("BorderThickness")
				{
					Source = border
				}
			}
		};
	}

	private static void BindBlurEnabled(BackdropBlurBorder backdrop, Border border)
	{
		if (GetIsBlurSuppressed((DependencyObject)(object)border))
		{
			BindingOperations.ClearBinding((DependencyObject)(object)backdrop, BackdropBlurBorder.IsBlurEnabledProperty);
			backdrop.IsBlurEnabled = false;
			return;
		}
		BindingOperations.SetBinding((DependencyObject)(object)backdrop, BackdropBlurBorder.IsBlurEnabledProperty, new Binding
		{
			Source = border,
			Path = new PropertyPath("(0)", IsBlurEnabledProperty)
		});
	}

	static BackdropBlurHost()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Expected O, but got Unknown
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected O, but got Unknown
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Expected O, but got Unknown
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Expected O, but got Unknown
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Expected O, but got Unknown
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Expected O, but got Unknown
		IsAppliedProperty = DependencyProperty.RegisterAttached("IsApplied", typeof(bool), typeof(BackdropBlurHost), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsAppliedChanged)));
		IsBlurEnabledProperty = DependencyProperty.RegisterAttached("IsBlurEnabled", typeof(bool), typeof(BackdropBlurHost), new PropertyMetadata((object)false));
		IsBlurSuppressedProperty = DependencyProperty.RegisterAttached("IsBlurSuppressed", typeof(bool), typeof(BackdropBlurHost), (PropertyMetadata)(object)new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, new PropertyChangedCallback(OnIsBlurSuppressedChanged)));
		FallbackBrushProperty = DependencyProperty.RegisterAttached("FallbackBrush", typeof(Brush), typeof(BackdropBlurHost), new PropertyMetadata((PropertyChangedCallback)null));
		LightweightShadowEffectProperty = DependencyProperty.RegisterAttached("LightweightShadowEffect", typeof(DropShadowEffect), typeof(BackdropBlurHost), new PropertyMetadata((object)null, new PropertyChangedCallback(OnLightweightShadowEffectChanged)));
		BackdropProperty = DependencyProperty.RegisterAttached("Backdrop", typeof(BackdropBlurBorder), typeof(BackdropBlurHost), new PropertyMetadata((PropertyChangedCallback)null));
		ShadowChromeProperty = DependencyProperty.RegisterAttached("ShadowChrome", typeof(CardShadowChrome), typeof(BackdropBlurHost), new PropertyMetadata((PropertyChangedCallback)null));
	}
}
