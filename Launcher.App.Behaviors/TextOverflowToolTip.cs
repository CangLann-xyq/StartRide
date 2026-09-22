using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Launcher.App.Behaviors;

public static class TextOverflowToolTip
{
	public static readonly DependencyProperty IsEnabledProperty;

	public static bool GetIsEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsEnabledProperty);
	}

	public static void SetIsEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsEnabledProperty, (object)value);
	}

	private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is TextBlock textBlock)
		{
			if ((bool)e.NewValue)
			{
				textBlock.ToolTipOpening += OnToolTipOpening;
			}
			else
			{
				textBlock.ToolTipOpening -= OnToolTipOpening;
			}
		}
	}

	private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
	{
		if (sender is TextBlock textBlock && !HasOverflow(textBlock))
		{
			e.Handled = true;
		}
	}

	internal static bool HasOverflow(TextBlock textBlock)
	{
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrEmpty(textBlock.Text) || !textBlock.IsArrangeValid)
		{
			return false;
		}
		Thickness padding = textBlock.Padding;
		double num = Math.Max(0.0, textBlock.ActualWidth - padding.Left - padding.Right);
		double num2 = Math.Max(0.0, textBlock.ActualHeight - padding.Top - padding.Bottom);
		if (num <= 0.0 || num2 <= 0.0)
		{
			return false;
		}
		DpiScale dpi = VisualTreeHelper.GetDpi(textBlock);
		FormattedText formattedText = new FormattedText(textBlock.Text, textBlock.Language.GetSpecificCulture(), textBlock.FlowDirection, new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch), textBlock.FontSize, Brushes.Black, null, TextOptions.GetTextFormattingMode((DependencyObject)(object)textBlock), dpi.PixelsPerDip);
		if (textBlock.TextWrapping != TextWrapping.NoWrap)
		{
			formattedText.MaxTextWidth = num;
		}
		if (!double.IsNaN(textBlock.LineHeight))
		{
			formattedText.LineHeight = textBlock.LineHeight;
		}
		if (!(formattedText.Width > num + 0.5 / dpi.DpiScaleX))
		{
			return formattedText.Height > num2 + 0.5 / dpi.DpiScaleY;
		}
		return true;
	}

	static TextOverflowToolTip()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(TextOverflowToolTip), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsEnabledChanged)));
	}
}
