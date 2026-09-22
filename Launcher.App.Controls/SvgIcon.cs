using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Resources;
using System.Xml.Linq;

namespace Launcher.App.Controls;

public sealed class SvgIcon : Control
{
	private sealed record SvgIconData(Rect ViewBox, IReadOnlyList<SvgShape> Shapes);

	private sealed record SvgShape(Geometry Geometry, bool HasFill, bool HasStroke, double StrokeThickness, PenLineCap LineCap, PenLineJoin LineJoin);

	private sealed record SvgStyleContext(string? Fill, string? Stroke, string? StrokeWidth, string? StrokeLineCap, string? StrokeLineJoin)
	{
		public static SvgStyleContext Merge(SvgStyleContext? inheritedStyle, XElement element)
		{
			return new SvgStyleContext(element.Attribute("fill")?.Value ?? inheritedStyle?.Fill, element.Attribute("stroke")?.Value ?? inheritedStyle?.Stroke, element.Attribute("stroke-width")?.Value ?? inheritedStyle?.StrokeWidth, element.Attribute("stroke-linecap")?.Value ?? inheritedStyle?.StrokeLineCap, element.Attribute("stroke-linejoin")?.Value ?? inheritedStyle?.StrokeLineJoin);
		}
	}

	public static readonly DependencyProperty IconKeyProperty;

	public static readonly DependencyProperty StrokeProperty;

	public static readonly DependencyProperty ForceFillProperty;

	private static readonly Dictionary<string, SvgIconData?> IconCache;

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

	public Brush? Stroke
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(StrokeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(StrokeProperty, (object)value);
		}
	}

	public bool ForceFill
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(ForceFillProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ForceFillProperty, (object)value);
		}
	}

	static SvgIcon()
	{
		IconKeyProperty = DependencyProperty.Register("IconKey", typeof(string), typeof(SvgIcon), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)null, FrameworkPropertyMetadataOptions.AffectsRender));
		StrokeProperty = DependencyProperty.Register("Stroke", typeof(Brush), typeof(SvgIcon), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)null, FrameworkPropertyMetadataOptions.AffectsRender));
		ForceFillProperty = DependencyProperty.Register("ForceFill", typeof(bool), typeof(SvgIcon), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)false, FrameworkPropertyMetadataOptions.AffectsRender));
		IconCache = new Dictionary<string, SvgIconData>(StringComparer.OrdinalIgnoreCase);
		Control.ForegroundProperty.OverrideMetadata(typeof(SvgIcon), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)null, FrameworkPropertyMetadataOptions.AffectsRender));
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		base.OnRender(drawingContext);
		if (string.IsNullOrWhiteSpace(IconKey) || base.ActualWidth <= 0.0 || base.ActualHeight <= 0.0)
		{
			return;
		}
		try
		{
			SvgIconData iconData = GetIconData(IconKey);
			if ((object)iconData == null)
			{
				return;
			}
			Rect viewBox = iconData.ViewBox;
			if (viewBox.Width <= 0.0)
			{
				return;
			}
			viewBox = iconData.ViewBox;
			if (viewBox.Height <= 0.0)
			{
				return;
			}
			double actualWidth = base.ActualWidth;
			viewBox = iconData.ViewBox;
			double val = actualWidth / viewBox.Width;
			double actualHeight = base.ActualHeight;
			viewBox = iconData.ViewBox;
			double num = Math.Min(val, actualHeight / viewBox.Height);
			viewBox = iconData.ViewBox;
			double num2 = viewBox.Width * num;
			viewBox = iconData.ViewBox;
			double num3 = viewBox.Height * num;
			double num4 = (base.ActualWidth - num2) / 2.0;
			viewBox = iconData.ViewBox;
			double offsetX = num4 - viewBox.X * num;
			double num5 = (base.ActualHeight - num3) / 2.0;
			viewBox = iconData.ViewBox;
			double offsetY = num5 - viewBox.Y * num;
			int num6 = 0;
			try
			{
				drawingContext.PushTransform(new TranslateTransform(offsetX, offsetY));
				num6++;
				drawingContext.PushTransform(new ScaleTransform(num, num));
				num6++;
				Brush foreground = base.Foreground;
				Brush brush = Stroke ?? foreground;
				foreach (SvgShape shape in iconData.Shapes)
				{
					Pen pen = ((shape.HasStroke && brush != null) ? new Pen(brush, shape.StrokeThickness)
					{
						StartLineCap = shape.LineCap,
						EndLineCap = shape.LineCap,
						LineJoin = shape.LineJoin
					} : null);
					drawingContext.DrawGeometry((shape.HasFill || ForceFill) ? foreground : null, pen, shape.Geometry);
				}
			}
			finally
			{
				while (num6 > 0)
				{
					drawingContext.Pop();
					num6--;
				}
			}
		}
		catch
		{
		}
	}

	private static SvgIconData? GetIconData(string iconKey)
	{
		if (IconCache.TryGetValue(iconKey, out SvgIconData value))
		{
			return value;
		}
		SvgIconData svgIconData = LoadIconData(iconKey);
		IconCache[iconKey] = svgIconData;
		return svgIconData;
	}

	private static SvgIconData? LoadIconData(string iconKey)
	{
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			string text = iconKey.Replace("\\", "/", StringComparison.Ordinal).TrimStart('/');
			StreamResourceInfo resourceStream = System.Windows.Application.GetResourceStream(new Uri("/Assets/Icons/" + text + ".svg", UriKind.Relative));
			if (resourceStream == null)
			{
				return null;
			}
			using Stream stream = resourceStream.Stream;
			XElement root = XDocument.Load(stream).Root;
			if (root == null)
			{
				return null;
			}
			Rect viewBox = ParseViewBox(root.Attribute("viewBox")?.Value);
			List<SvgShape> shapes = new List<SvgShape>();
			ReadShapes(root, shapes);
			return new SvgIconData(viewBox, shapes);
		}
		catch
		{
			return null;
		}
	}

	private static void ReadShapes(XElement element, List<SvgShape> shapes, SvgStyleContext? inheritedStyle = null)
	{
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Unknown result type (might be due to invalid IL or missing references)
		SvgStyleContext inheritedStyle2 = SvgStyleContext.Merge(inheritedStyle, element);
		Point center = default(Point);
		foreach (XElement item in element.Elements())
		{
			string localName = item.Name.LocalName;
			bool flag;
			switch (localName)
			{
			case "defs":
			case "clipPath":
			case "title":
			case "desc":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				continue;
			}
			switch (localName)
			{
			case "path":
			{
				string text = item.Attribute("d")?.Value;
				if (!string.IsNullOrWhiteSpace(text))
				{
					shapes.Add(CreateShape(item, Geometry.Parse(text), inheritedStyle2));
				}
				break;
			}
			case "circle":
			{
				center = new Point(ParseDouble(item.Attribute("cx")?.Value), ParseDouble(item.Attribute("cy")?.Value));
				double num5 = ParseDouble(item.Attribute("r")?.Value);
				shapes.Add(CreateShape(item, new EllipseGeometry(center, num5, num5), inheritedStyle2));
				break;
			}
			case "rect":
			{
				double num = ParseDouble(item.Attribute("x")?.Value);
				double num2 = ParseDouble(item.Attribute("y")?.Value);
				double num3 = ParseDouble(item.Attribute("width")?.Value);
				double num4 = ParseDouble(item.Attribute("height")?.Value);
				double radiusX = ParseDouble(item.Attribute("rx")?.Value);
				double radiusY = ParseDouble(item.Attribute("ry")?.Value);
				shapes.Add(CreateShape(item, new RectangleGeometry(new Rect(num, num2, num3, num4), radiusX, radiusY), inheritedStyle2));
				break;
			}
			default:
				ReadShapes(item, shapes, inheritedStyle2);
				break;
			}
		}
	}

	private static SvgShape CreateShape(XElement element, Geometry geometry, SvgStyleContext inheritedStyle)
	{
		Transform transform = ParseTransform(element.Attribute("transform")?.Value);
		if (transform != null)
		{
			geometry.Transform = transform;
		}
		SvgStyleContext svgStyleContext = SvgStyleContext.Merge(inheritedStyle, element);
		((Freezable)geometry).Freeze();
		return new SvgShape(geometry, HasPaint(svgStyleContext.Fill), HasPaint(svgStyleContext.Stroke), ParseDouble(svgStyleContext.StrokeWidth, 1.0), ParseLineCap(svgStyleContext.StrokeLineCap), ParseLineJoin(svgStyleContext.StrokeLineJoin));
	}

	private static Rect ParseViewBox(string? value)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrWhiteSpace(value))
		{
			return new Rect(0.0, 0.0, 24.0, 24.0);
		}
		double[] array = (from part in value.Split(new char[2] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
			select double.Parse(part, CultureInfo.InvariantCulture)).ToArray();
		if (array.Length != 4)
		{
			return new Rect(0.0, 0.0, 24.0, 24.0);
		}
		return new Rect(array[0], array[1], array[2], array[3]);
	}

	private static bool HasPaint(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static double ParseDouble(string? value, double fallback = 0.0)
	{
		if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return fallback;
		}
		return result;
	}

	private static PenLineCap ParseLineCap(string? value)
	{
		if (!string.Equals(value, "round", StringComparison.OrdinalIgnoreCase))
		{
			return PenLineCap.Flat;
		}
		return PenLineCap.Round;
	}

	private static PenLineJoin ParseLineJoin(string? value)
	{
		if (!string.Equals(value, "round", StringComparison.OrdinalIgnoreCase))
		{
			return PenLineJoin.Miter;
		}
		return PenLineJoin.Round;
	}

	private static Transform? ParseTransform(string? value)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		Matrix identity = Matrix.Identity;
		foreach (Match item in Regex.Matches(value, "([a-zA-Z]+)\\(([^)]*)\\)"))
		{
			string value2 = item.Groups[1].Value;
			double[] array = (from part in item.Groups[2].Value.Split(new char[2] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
				select double.Parse(part, CultureInfo.InvariantCulture)).ToArray();
			if (value2.Equals("matrix", StringComparison.OrdinalIgnoreCase) && array.Length == 6)
			{
				identity.Append(new Matrix(array[0], array[1], array[2], array[3], array[4], array[5]));
			}
			else if (value2.Equals("translate", StringComparison.OrdinalIgnoreCase) && array.Length >= 1)
			{
				identity.Translate(array[0], (array.Length > 1) ? array[1] : 0.0);
			}
			else if (value2.Equals("scale", StringComparison.OrdinalIgnoreCase) && array.Length >= 1)
			{
				identity.Scale(array[0], (array.Length > 1) ? array[1] : array[0]);
			}
		}
		if (identity.IsIdentity)
		{
			return null;
		}
		MatrixTransform matrixTransform = new MatrixTransform(identity);
		((Freezable)matrixTransform).Freeze();
		return matrixTransform;
	}
}
