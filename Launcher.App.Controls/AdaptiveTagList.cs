using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Launcher.App.Controls;

public sealed class AdaptiveTagList : Panel
{
	private static readonly Rect HiddenArrangeRect;

	public static readonly DependencyProperty ItemsSourceProperty;

	public static readonly DependencyProperty TagBackgroundProperty;

	public static readonly DependencyProperty TagForegroundProperty;

	private readonly List<Border> itemTags = new List<Border>();

	private readonly TextBlock overflowText;

	private readonly Border overflowTag;

	private int visibleItemCount;

	private bool showsOverflow;

	public IEnumerable? ItemsSource
	{
		get
		{
			return (IEnumerable)((DependencyObject)this).GetValue(ItemsSourceProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ItemsSourceProperty, (object)value);
		}
	}

	public Brush? TagBackground
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(TagBackgroundProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TagBackgroundProperty, (object)value);
		}
	}

	public Brush? TagForeground
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(TagForegroundProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TagForegroundProperty, (object)value);
		}
	}

	public AdaptiveTagList()
	{
		base.IsHitTestVisible = false;
		base.ClipToBounds = true;
		overflowText = CreateTagText(string.Empty);
		overflowTag = CreateTag(overflowText);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		Size infinite = new Size(double.PositiveInfinity, availableSize.Height);
		foreach (Border itemTag in itemTags)
		{
			itemTag.Measure(infinite);
		}
		double num = (double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0.0, availableSize.Width));
		List<double> list = itemTags.Select(delegate(Border tag)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			Size desiredSize2 = tag.DesiredSize;
			return desiredSize2.Width;
		}).ToList();
		double num2 = list.Sum();
		if (num2 <= num)
		{
			visibleItemCount = itemTags.Count;
			showsOverflow = false;
			return new Size(num2, ResolveHeight());
		}
		visibleItemCount = CalculateVisibleItemCount(list, num, delegate(int hiddenCount)
		{
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			overflowText.Text = $"+{hiddenCount}";
			overflowTag.Measure(infinite);
			Size desiredSize2 = overflowTag.DesiredSize;
			return desiredSize2.Width;
		});
		showsOverflow = visibleItemCount < itemTags.Count;
		int value = itemTags.Count - visibleItemCount;
		overflowText.Text = $"+{value}";
		overflowTag.Measure(infinite);
		double num3 = list.Take(visibleItemCount).Sum();
		Size desiredSize = overflowTag.DesiredSize;
		return new Size(Math.Min(num3 + desiredSize.Width, num), ResolveHeight());
	}

	internal static int CalculateVisibleItemCount(IReadOnlyList<double> itemWidths, double availableWidth, Func<int, double> getOverflowWidth)
	{
		if (itemWidths.Sum() <= availableWidth)
		{
			return itemWidths.Count;
		}
		for (int num = itemWidths.Count - 1; num >= 0; num--)
		{
			int arg = itemWidths.Count - num;
			if (itemWidths.Take(num).Sum() + getOverflowWidth(arg) <= availableWidth || num == 0)
			{
				return num;
			}
		}
		return 0;
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		double num = 0.0;
		Size desiredSize;
		for (int i = 0; i < itemTags.Count; i++)
		{
			Border border = itemTags[i];
			if (i >= visibleItemCount)
			{
				border.Arrange(HiddenArrangeRect);
				continue;
			}
			double num2 = num;
			desiredSize = border.DesiredSize;
			border.Arrange(new Rect(num2, 0.0, desiredSize.Width, finalSize.Height));
			double num3 = num;
			desiredSize = border.DesiredSize;
			num = num3 + desiredSize.Width;
		}
		if (showsOverflow)
		{
			Border border2 = overflowTag;
			double num4 = num;
			desiredSize = overflowTag.DesiredSize;
			border2.Arrange(new Rect(num4, 0.0, desiredSize.Width, finalSize.Height));
		}
		else
		{
			overflowTag.Arrange(HiddenArrangeRect);
		}
		return finalSize;
	}

	private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
	{
		if (dependencyObject is AdaptiveTagList adaptiveTagList)
		{
			adaptiveTagList.RebuildItems(args.NewValue as IEnumerable);
		}
	}

	private static void OnTagAppearanceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
	{
		if (dependencyObject is AdaptiveTagList adaptiveTagList)
		{
			adaptiveTagList.RefreshTagAppearance();
		}
	}

	private void RebuildItems(IEnumerable? items)
	{
		base.Children.Clear();
		itemTags.Clear();
		foreach (string item in items?.Cast<object>().Select((object value) => value?.ToString()).Where((string value) => !string.IsNullOrWhiteSpace(value))
			.Distinct<string>(StringComparer.CurrentCulture) ?? Array.Empty<string>())
		{
			Border border = CreateTag(CreateTagText(item));
			itemTags.Add(border);
			base.Children.Add(border);
		}
		base.Children.Add(overflowTag);
		InvalidateMeasure();
	}

	private Border CreateTag(TextBlock text)
	{
		Border border = new Border
		{
			MinWidth = 24.0,
			Margin = new Thickness(0.0, 0.0, 4.0, 0.0),
			Padding = new Thickness(6.0, 1.0, 6.0, 1.0),
			Child = text
		};
		ApplyTagAppearance(border);
		border.SetResourceReference(Border.CornerRadiusProperty, "LauncherCornerRadiusSmall");
		return border;
	}

	private void RefreshTagAppearance()
	{
		foreach (Border itemTag in itemTags)
		{
			ApplyTagAppearance(itemTag);
		}
		ApplyTagAppearance(overflowTag);
	}

	private void ApplyTagAppearance(Border tag)
	{
		if (TagBackground == null)
		{
			tag.SetResourceReference(Border.BackgroundProperty, "Brush.14FFFFFF");
		}
		else
		{
			tag.Background = TagBackground;
		}
		if (tag.Child is TextBlock textBlock)
		{
			if (TagForeground == null)
			{
				textBlock.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Text.Secondary");
			}
			else
			{
				textBlock.Foreground = TagForeground;
			}
		}
	}

	private static TextBlock CreateTagText(string text)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontSize = 11.0;
		textBlock.FontWeight = FontWeights.Normal;
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Text.Secondary");
		return textBlock;
	}

	private double ResolveHeight()
	{
		return base.Children.Cast<UIElement>().Select(delegate(UIElement child)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			Size desiredSize = child.DesiredSize;
			return desiredSize.Height;
		}).DefaultIfEmpty(0.0)
			.Max();
	}

	static AdaptiveTagList()
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Expected O, but got Unknown
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected O, but got Unknown
		HiddenArrangeRect = new Rect(0.0, 0.0, 0.0, 0.0);
		ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(AdaptiveTagList), (PropertyMetadata)(object)new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, new PropertyChangedCallback(OnItemsSourceChanged)));
		TagBackgroundProperty = DependencyProperty.Register("TagBackground", typeof(Brush), typeof(AdaptiveTagList), (PropertyMetadata)(object)new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnTagAppearanceChanged)));
		TagForegroundProperty = DependencyProperty.Register("TagForeground", typeof(Brush), typeof(AdaptiveTagList), (PropertyMetadata)(object)new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, new PropertyChangedCallback(OnTagAppearanceChanged)));
	}
}
