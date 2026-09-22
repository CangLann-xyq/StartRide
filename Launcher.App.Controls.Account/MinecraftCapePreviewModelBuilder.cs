using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using Launcher.Application.Accounts;

namespace Launcher.App.Controls.Account;

public static class MinecraftCapePreviewModelBuilder
{
	private sealed record BatchedCapeFace(Rect3D Bounds, Int32Rect TextureRect, bool ReverseWinding);

	private const int PixelScale = 8;

	private const string NoneCapeIconResource = "/Assets/Icons/account_page/account_page_forbid.svg";

	public static AmbientLight CreateAmbientLight()
	{
		return new AmbientLight(Color.FromRgb(168, 168, 168));
	}

	public static DirectionalLight CreateDirectionalLight()
	{
		return new DirectionalLight(Color.FromRgb(132, 132, 132), new Vector3D(-0.2, -0.35, -0.9));
	}

	public static Model3DGroup BuildCapeModel(AccountCapeOption cape, double brightness = 1.0, BitmapSource? texture = null)
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0317: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b8: Unknown result type (might be due to invalid IL or missing references)
		ArgumentNullException.ThrowIfNull(cape, "cape");
		if (cape.IsNone)
		{
			return BuildNoneCapeModel(brightness);
		}
		Model3DGroup model3DGroup = new Model3DGroup();
		Material material = CreateSolidMaterial(Color.FromArgb(160, ApplyBrightness(74, brightness), ApplyBrightness(92, brightness), ApplyBrightness(118, brightness)));
		PreviewMeshBuilder mesh = new PreviewMeshBuilder();
		AddBatchedCapeFace(mesh, new Rect3D(-5.15, -0.15, 0.42, 10.3, 16.3, 0.0), new Rect(0.0, 0.0, 1.0, 1.0));
		AddBatchedCapeFace(mesh, new Rect3D(-5.15, -0.15, -0.58, 10.3, 16.3, 0.0), new Rect(0.0, 0.0, 1.0, 1.0));
		AddBatchedCapeMesh(model3DGroup, mesh, material);
		if (!string.IsNullOrWhiteSpace(cape.ImageUrl) && texture != null)
		{
			try
			{
				BatchedCapeFace[] array = new BatchedCapeFace[6]
				{
					new BatchedCapeFace(new Rect3D(-5.0, 0.0, 0.5, 10.0, 16.0, 0.0), new Int32Rect(1, 1, 10, 16), ReverseWinding: true),
					new BatchedCapeFace(new Rect3D(-5.0, 0.0, -0.5, 10.0, 16.0, 0.0), new Int32Rect(12, 1, 10, 16), ReverseWinding: false),
					new BatchedCapeFace(new Rect3D(-5.0, 16.0, -0.5, 10.0, 0.0, 1.0), new Int32Rect(1, 0, 10, 1), ReverseWinding: true),
					new BatchedCapeFace(new Rect3D(-5.0, 0.0, -0.5, 10.0, 0.0, 1.0), new Int32Rect(11, 0, 10, 1), ReverseWinding: false),
					new BatchedCapeFace(new Rect3D(-5.0, 0.0, -0.5, 0.0, 16.0, 1.0), new Int32Rect(0, 1, 1, 16), ReverseWinding: false),
					new BatchedCapeFace(new Rect3D(5.0, 0.0, -0.5, 0.0, 16.0, 1.0), new Int32Rect(11, 1, 1, 16), ReverseWinding: true)
				};
				PreviewTextureAtlas previewTextureAtlas = PreviewTextureAtlasBuilder.Build(texture, array.Select(delegate(BatchedCapeFace item)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					return item.TextureRect;
				}), 8, brightness, 64);
				ImageBrush obj = new ImageBrush(previewTextureAtlas.Bitmap)
				{
					Stretch = Stretch.Fill,
					TileMode = TileMode.None
				};
				RenderOptions.SetBitmapScalingMode((DependencyObject)(object)obj, BitmapScalingMode.NearestNeighbor);
				((Freezable)obj).Freeze();
				DiffuseMaterial diffuseMaterial = new DiffuseMaterial(obj);
				((Freezable)diffuseMaterial).Freeze();
				PreviewMeshBuilder mesh2 = new PreviewMeshBuilder();
				BatchedCapeFace[] array2 = array;
				foreach (BatchedCapeFace batchedCapeFace in array2)
				{
					AddBatchedCapeFace(mesh2, batchedCapeFace.Bounds, previewTextureAtlas.TextureCoordinates[batchedCapeFace.TextureRect], batchedCapeFace.ReverseWinding);
				}
				AddBatchedCapeMesh(model3DGroup, mesh2, diffuseMaterial, doubleSided: false, anchorUnitTextureCoordinates: true);
			}
			catch
			{
			}
		}
		ApplyCapeRotationAndFreeze(model3DGroup);
		return model3DGroup;
	}

	private static void AddBatchedCapeFace(PreviewMeshBuilder mesh, Rect3D bounds, Rect textureCoordinates, bool reverseWinding = false)
	{
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		mesh.AddQuad(new Point3D(bounds.X, bounds.Y + bounds.SizeY, bounds.Z), new Point3D(bounds.X + bounds.SizeX, bounds.Y + bounds.SizeY, bounds.Z + bounds.SizeZ), new Point3D(bounds.X + bounds.SizeX, bounds.Y, bounds.Z + bounds.SizeZ), new Point3D(bounds.X, bounds.Y, bounds.Z), textureCoordinates, reverseWinding);
	}

	private static void AddBatchedCapeMesh(Model3DGroup target, PreviewMeshBuilder mesh, Material material, bool doubleSided = true, bool anchorUnitTextureCoordinates = false)
	{
		if (mesh.QuadCount != 0)
		{
			GeometryModel3D geometryModel3D = new GeometryModel3D
			{
				Geometry = mesh.Build(anchorUnitTextureCoordinates),
				Material = material,
				BackMaterial = (doubleSided ? material : null)
			};
			((Freezable)geometryModel3D).Freeze();
			target.Children.Add(geometryModel3D);
		}
	}

	private static void ApplyCapeRotationAndFreeze(Model3DGroup model)
	{
		AxisAngleRotation3D axisAngleRotation3D = new AxisAngleRotation3D(new Vector3D(1.0, 0.0, 0.0), -6.0);
		((Freezable)axisAngleRotation3D).Freeze();
		RotateTransform3D rotateTransform3D = new RotateTransform3D(axisAngleRotation3D, 0.0, 8.0, 0.0);
		((Freezable)rotateTransform3D).Freeze();
		model.Transform = rotateTransform3D;
		((Freezable)model).Freeze();
	}

	private static Model3DGroup BuildNoneCapeModel(double brightness)
	{
		Model3DGroup model3DGroup = new Model3DGroup();
		model3DGroup.Transform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1.0, 0.0, 0.0), -6.0), 0.0, 8.0, 0.0);
		Material material = CreateSolidMaterial(Color.FromArgb(42, ApplyBrightness(byte.MaxValue, brightness), ApplyBrightness(byte.MaxValue, brightness), ApplyBrightness(byte.MaxValue, brightness)));
		Material material2 = CreateSvgIconMaterial("/Assets/Icons/account_page/account_page_forbid.svg", brightness);
		AddSolidFace(model3DGroup, new Rect3D(-5.0, 0.0, 0.0, 10.0, 16.0, 0.0), material);
		if (material2 != null)
		{
			AddSolidFace(model3DGroup, new Rect3D(-2.2, 5.25, 0.08, 4.4, 4.4, 0.0), material2);
		}
		ApplyCapeRotationAndFreeze(model3DGroup);
		return model3DGroup;
	}

	private static void AddFace(Model3DGroup group, BitmapSource texture, Rect3D bounds, Int32Rect textureRect, double brightness)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		Material material = CreateImageMaterial(texture, textureRect, brightness);
		group.Children.Add(new GeometryModel3D
		{
			Geometry = CreateFaceMesh(bounds),
			Material = material,
			BackMaterial = material
		});
	}

	private static void AddSolidFace(Model3DGroup group, Rect3D bounds, Material material, double rotationDegrees = 0.0)
	{
		GeometryModel3D geometryModel3D = new GeometryModel3D
		{
			Geometry = CreateFaceMesh(bounds),
			Material = material,
			BackMaterial = material
		};
		if (Math.Abs(rotationDegrees) > double.Epsilon)
		{
			geometryModel3D.Transform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0.0, 0.0, 1.0), rotationDegrees));
		}
		group.Children.Add(geometryModel3D);
	}

	private static Material CreateImageMaterial(BitmapSource texture, Int32Rect textureRect, double brightness)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		ImageBrush obj = new ImageBrush(CreatePixelSharpFaceBitmap(texture, textureRect, brightness))
		{
			Stretch = Stretch.Fill,
			TileMode = TileMode.None
		};
		RenderOptions.SetBitmapScalingMode((DependencyObject)(object)obj, BitmapScalingMode.NearestNeighbor);
		((Freezable)obj).Freeze();
		DiffuseMaterial diffuseMaterial = new DiffuseMaterial(obj);
		((Freezable)diffuseMaterial).Freeze();
		return diffuseMaterial;
	}

	private static Material CreateSolidMaterial(Color color)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(color);
		((Freezable)solidColorBrush).Freeze();
		DiffuseMaterial diffuseMaterial = new DiffuseMaterial(solidColorBrush);
		((Freezable)diffuseMaterial).Freeze();
		return diffuseMaterial;
	}

	private static Material? CreateSvgIconMaterial(string resourcePath, double brightness)
	{
		try
		{
			ImageBrush obj = new ImageBrush(RenderSvgIconBitmap(resourcePath, brightness))
			{
				Stretch = Stretch.Fill,
				TileMode = TileMode.None
			};
			RenderOptions.SetBitmapScalingMode((DependencyObject)(object)obj, BitmapScalingMode.NearestNeighbor);
			((Freezable)obj).Freeze();
			DiffuseMaterial diffuseMaterial = new DiffuseMaterial(obj);
			((Freezable)diffuseMaterial).Freeze();
			return diffuseMaterial;
		}
		catch
		{
			return null;
		}
	}

	private static BitmapSource RenderSvgIconBitmap(string resourcePath, double brightness)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		using Stream stream = (System.Windows.Application.GetResourceStream(new Uri(resourcePath, UriKind.Relative)) ?? throw new InvalidOperationException("SVG resource was not found.")).Stream;
		XElement? obj = XDocument.Load(stream).Root ?? throw new InvalidOperationException("SVG root was not found.");
		Rect val = ParseViewBox(obj.Attribute("viewBox")?.Value);
		List<(Geometry, double)> list = (from element in obj.Descendants()
			where element.Name.LocalName == "path"
			select (Geometry: Geometry.Parse(element.Attribute("d")?.Value ?? string.Empty), StrokeWidth: ParseDouble(element.Attribute("stroke-width")?.Value, 4.0))).ToList();
		byte b = ApplyBrightness(byte.MaxValue, brightness);
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromArgb(230, b, b, b));
		((Freezable)solidColorBrush).Freeze();
		DrawingVisual drawingVisual = new DrawingVisual();
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			double num = Math.Min(128.0 / val.Width, 128.0 / val.Height);
			double offsetX = (128.0 - val.Width * num) / 2.0;
			double offsetY = (128.0 - val.Height * num) / 2.0;
			drawingContext.PushTransform(new TranslateTransform(offsetX, offsetY));
			drawingContext.PushTransform(new ScaleTransform(num, num));
			drawingContext.PushTransform(new TranslateTransform(0.0 - val.X, 0.0 - val.Y));
			foreach (var item in list)
			{
				((Freezable)item.Item1).Freeze();
				Pen pen = new Pen(solidColorBrush, item.Item2)
				{
					StartLineCap = PenLineCap.Round,
					EndLineCap = PenLineCap.Round,
					LineJoin = PenLineJoin.Round
				};
				((Freezable)pen).Freeze();
				drawingContext.DrawGeometry(null, pen, item.Item1);
			}
		}
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(128, 128, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		((Freezable)renderTargetBitmap).Freeze();
		return renderTargetBitmap;
	}

	private static Rect ParseViewBox(string? value)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrWhiteSpace(value))
		{
			return new Rect(0.0, 0.0, 48.0, 48.0);
		}
		double[] array = (from part in value.Split(new char[2] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
			select double.Parse(part, CultureInfo.InvariantCulture)).ToArray();
		if (array.Length != 4)
		{
			return new Rect(0.0, 0.0, 48.0, 48.0);
		}
		return new Rect(array[0], array[1], array[2], array[3]);
	}

	private static double ParseDouble(string? value, double fallback)
	{
		if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return fallback;
		}
		return result;
	}

	private static MeshGeometry3D CreateFaceMesh(Rect3D b)
	{
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		Point3D value = new Point3D(b.X, b.Y + b.SizeY, b.Z);
		Point3D value2 = new Point3D(b.X + b.SizeX, b.Y + b.SizeY, b.Z + b.SizeZ);
		Point3D value3 = new Point3D(b.X + b.SizeX, b.Y, b.Z + b.SizeZ);
		Point3D value4 = new Point3D(b.X, b.Y, b.Z);
		MeshGeometry3D obj = new MeshGeometry3D
		{
			Positions = new Point3DCollection { value, value2, value3, value4 },
			TextureCoordinates = new PointCollection
			{
				new Point(0.0, 0.0),
				new Point(1.0, 0.0),
				new Point(1.0, 1.0),
				new Point(0.0, 1.0)
			},
			TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
		};
		((Freezable)obj).Freeze();
		return obj;
	}

	public static BitmapSource CreatePixelSharpFaceBitmap(BitmapSource texture, Int32Rect textureRect, double brightness)
	{
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		brightness = Math.Clamp(brightness, 0.0, 1.0);
		BitmapSource bitmapSource = EnsureBgra32(texture);
		int num = Math.Clamp(textureRect.X, 0, bitmapSource.PixelWidth - 1);
		int num2 = Math.Clamp(textureRect.Y, 0, bitmapSource.PixelHeight - 1);
		Int32Rect sourceRect = new Int32Rect(num, num2, Math.Max(1, Math.Min(textureRect.Width, bitmapSource.PixelWidth - num)), Math.Max(1, Math.Min(textureRect.Height, bitmapSource.PixelHeight - num2)));
		int num3 = sourceRect.Width * 4;
		byte[] array = new byte[num3 * sourceRect.Height];
		bitmapSource.CopyPixels(sourceRect, array, num3, 0);
		int num4 = sourceRect.Width * 8;
		int num5 = sourceRect.Height * 8;
		int num6 = num4 * 4;
		byte[] array2 = new byte[num6 * num5];
		for (int i = 0; i < num5; i++)
		{
			int num7 = i / 8;
			for (int j = 0; j < num4; j++)
			{
				int num8 = j / 8;
				int num9 = num7 * num3 + num8 * 4;
				int num10 = i * num6 + j * 4;
				array2[num10] = ApplyBrightness(array[num9], brightness);
				array2[num10 + 1] = ApplyBrightness(array[num9 + 1], brightness);
				array2[num10 + 2] = ApplyBrightness(array[num9 + 2], brightness);
				array2[num10 + 3] = array[num9 + 3];
			}
		}
		BitmapSource bitmapSource2 = BitmapSource.Create(num4, num5, 96.0, 96.0, PixelFormats.Bgra32, null, array2, num6);
		((Freezable)bitmapSource2).Freeze();
		return bitmapSource2;
	}

	private static byte ApplyBrightness(byte value, double brightness)
	{
		return (byte)Math.Round((double)(int)value * brightness);
	}

	private static BitmapSource EnsureBgra32(BitmapSource source)
	{
		if (source.Format == PixelFormats.Bgra32)
		{
			return source;
		}
		FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0.0);
		((Freezable)formatConvertedBitmap).Freeze();
		return formatConvertedBitmap;
	}
}
