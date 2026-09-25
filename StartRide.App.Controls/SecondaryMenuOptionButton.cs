using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using StartRide.App.Behaviors;

namespace StartRide.App.Controls;

public partial class SecondaryMenuOptionButton : UserControl, IComponentConnector
{
	public static readonly DependencyProperty TextProperty;

	public static readonly DependencyProperty TextMarginProperty;

	public static readonly DependencyProperty IconModeProperty;

	public static readonly DependencyProperty IconKeyProperty;

	public static readonly DependencyProperty GlyphProperty;

	public static readonly DependencyProperty AvatarSourceProperty;

	public static readonly DependencyProperty CommandProperty;

	public static readonly DependencyProperty CommandParameterProperty;

	public static readonly DependencyProperty IsSelectedProperty;

	public static readonly DependencyProperty IsExternalMouseOverProperty;

	public static readonly DependencyProperty IsPointerOverOptionProperty;

	public static readonly DependencyProperty IconWidthProperty;

	public static readonly DependencyProperty IconHeightProperty;

	public static readonly DependencyProperty GlyphFontFamilyProperty;

	public static readonly DependencyProperty GlyphFontSizeProperty;

	public static readonly DependencyProperty GlyphFontWeightProperty;

	public static readonly DependencyProperty SvgIconVisibilityProperty;

	public static readonly DependencyProperty AvatarIconVisibilityProperty;

	public static readonly DependencyProperty GlyphIconVisibilityProperty;

	public static readonly RoutedEvent RefreshRequestedEvent;

	public string Text
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(TextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TextProperty, (object)value);
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

	public string IconMode
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(IconModeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IconModeProperty, (object)value);
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

	public string Glyph
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(GlyphProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(GlyphProperty, (object)value);
		}
	}

	public object? AvatarSource
	{
		get
		{
			return ((DependencyObject)this).GetValue(AvatarSourceProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(AvatarSourceProperty, value);
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

	public bool IsExternalMouseOver
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsExternalMouseOverProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsExternalMouseOverProperty, (object)value);
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

	public FontFamily GlyphFontFamily
	{
		get
		{
			return (FontFamily)((DependencyObject)this).GetValue(GlyphFontFamilyProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(GlyphFontFamilyProperty, (object)value);
		}
	}

	public double GlyphFontSize
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(GlyphFontSizeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(GlyphFontSizeProperty, (object)value);
		}
	}

	public FontWeight GlyphFontWeight
	{
		get
		{
			return (FontWeight)((DependencyObject)this).GetValue(GlyphFontWeightProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(GlyphFontWeightProperty, (object)value);
		}
	}

	public Visibility SvgIconVisibility
	{
		get
		{
			return (Visibility)((DependencyObject)this).GetValue(SvgIconVisibilityProperty);
		}
		private set
		{
			((DependencyObject)this).SetValue(SvgIconVisibilityProperty, (object)value);
		}
	}

	public Visibility AvatarIconVisibility
	{
		get
		{
			return (Visibility)((DependencyObject)this).GetValue(AvatarIconVisibilityProperty);
		}
		private set
		{
			((DependencyObject)this).SetValue(AvatarIconVisibilityProperty, (object)value);
		}
	}

	public Visibility GlyphIconVisibility
	{
		get
		{
			return (Visibility)((DependencyObject)this).GetValue(GlyphIconVisibilityProperty);
		}
		private set
		{
			((DependencyObject)this).SetValue(GlyphIconVisibilityProperty, (object)value);
		}
	}

	public event RoutedEventHandler RefreshRequested
	{
		add
		{
			AddHandler(RefreshRequestedEvent, value);
		}
		remove
		{
			RemoveHandler(RefreshRequestedEvent, value);
		}
	}

	public SecondaryMenuOptionButton()
	{
		InitializeComponent();
		UpdateIconVisibility();
		base.MouseEnter += delegate
		{
			UpdatePointerOverOption();
		};
		base.MouseLeave += delegate
		{
			UpdatePointerOverOption();
		};
	}

	private void Button_OnClick(object sender, RoutedEventArgs e)
	{
		if (IsSelected)
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				RaiseEvent(new RoutedEventArgs(RefreshRequestedEvent, this));
			}, (DispatcherPriority)4, Array.Empty<object>());
		}
	}

	private static void OnPointerStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is SecondaryMenuOptionButton secondaryMenuOptionButton)
		{
			secondaryMenuOptionButton.UpdatePointerOverOption();
		}
	}

	private static void OnIconModeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is SecondaryMenuOptionButton secondaryMenuOptionButton)
		{
			secondaryMenuOptionButton.UpdateIconVisibility();
		}
	}

	private void UpdatePointerOverOption()
	{
		OptionHoverBehavior.SetIsExternalActive(value: IsPointerOverOption = base.IsMouseOver || IsExternalMouseOver, element: (DependencyObject)(object)PART_Button);
	}

	private void UpdateIconVisibility()
	{
		SvgIconVisibility = ((!string.Equals(IconMode, "Svg", StringComparison.OrdinalIgnoreCase)) ? Visibility.Collapsed : Visibility.Visible);
		AvatarIconVisibility = ((!string.Equals(IconMode, "Avatar", StringComparison.OrdinalIgnoreCase)) ? Visibility.Collapsed : Visibility.Visible);
		GlyphIconVisibility = ((!string.Equals(IconMode, "Glyph", StringComparison.OrdinalIgnoreCase)) ? Visibility.Collapsed : Visibility.Visible);
	}


	static SecondaryMenuOptionButton()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Expected O, but got Unknown
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Expected O, but got Unknown
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Expected O, but got Unknown
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Expected O, but got Unknown
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Expected O, but got Unknown
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Expected O, but got Unknown
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Expected O, but got Unknown
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Expected O, but got Unknown
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Expected O, but got Unknown
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Expected O, but got Unknown
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Expected O, but got Unknown
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_028a: Expected O, but got Unknown
		//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bc: Expected O, but got Unknown
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Expected O, but got Unknown
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0324: Expected O, but got Unknown
		//IL_0348: Unknown result type (might be due to invalid IL or missing references)
		//IL_0352: Expected O, but got Unknown
		//IL_0376: Unknown result type (might be due to invalid IL or missing references)
		//IL_0380: Expected O, but got Unknown
		//IL_03a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ae: Expected O, but got Unknown
		TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)string.Empty));
		TextMarginProperty = DependencyProperty.Register("TextMargin", typeof(Thickness), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)new Thickness(4.0, 0.0, 8.0, 0.0)));
		IconModeProperty = DependencyProperty.Register("IconMode", typeof(string), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)"Svg", new PropertyChangedCallback(OnIconModeChanged)));
		IconKeyProperty = DependencyProperty.Register("IconKey", typeof(string), typeof(SecondaryMenuOptionButton), new PropertyMetadata((PropertyChangedCallback)null));
		GlyphProperty = DependencyProperty.Register("Glyph", typeof(string), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)string.Empty));
		AvatarSourceProperty = DependencyProperty.Register("AvatarSource", typeof(object), typeof(SecondaryMenuOptionButton), new PropertyMetadata((PropertyChangedCallback)null));
		CommandProperty = DependencyProperty.Register("Command", typeof(ICommand), typeof(SecondaryMenuOptionButton), new PropertyMetadata((PropertyChangedCallback)null));
		CommandParameterProperty = DependencyProperty.Register("CommandParameter", typeof(object), typeof(SecondaryMenuOptionButton), new PropertyMetadata((PropertyChangedCallback)null));
		IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)false));
		IsExternalMouseOverProperty = DependencyProperty.Register("IsExternalMouseOver", typeof(bool), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)false, new PropertyChangedCallback(OnPointerStateChanged)));
		IsPointerOverOptionProperty = DependencyProperty.Register("IsPointerOverOption", typeof(bool), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)false));
		IconWidthProperty = DependencyProperty.Register("IconWidth", typeof(double), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)22.0));
		IconHeightProperty = DependencyProperty.Register("IconHeight", typeof(double), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)22.0));
		GlyphFontFamilyProperty = DependencyProperty.Register("GlyphFontFamily", typeof(FontFamily), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)new FontFamily("Microsoft YaHei UI")));
		GlyphFontSizeProperty = DependencyProperty.Register("GlyphFontSize", typeof(double), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)18.0));
		GlyphFontWeightProperty = DependencyProperty.Register("GlyphFontWeight", typeof(FontWeight), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)FontWeights.Medium));
		SvgIconVisibilityProperty = DependencyProperty.Register("SvgIconVisibility", typeof(Visibility), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)Visibility.Visible));
		AvatarIconVisibilityProperty = DependencyProperty.Register("AvatarIconVisibility", typeof(Visibility), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)Visibility.Collapsed));
		GlyphIconVisibilityProperty = DependencyProperty.Register("GlyphIconVisibility", typeof(Visibility), typeof(SecondaryMenuOptionButton), new PropertyMetadata((object)Visibility.Collapsed));
		RefreshRequestedEvent = EventManager.RegisterRoutedEvent("RefreshRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SecondaryMenuOptionButton));
	}
}
