using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Launcher.Domain.Models;

namespace Launcher.App.Controls.Account;

internal static class MinecraftSkinPreviewModelBuilder
{
	private sealed record BatchedSkinFace(Rect3D Bounds, CubeFace Face, Int32Rect TextureRect, bool IsOverlay);

	private enum CubeFace
	{
		Front,
		Back,
		Left,
		Right,
		Top,
		Bottom
	}

	private const int AtlasPixelScale = 8;

	public static AmbientLight CreateAmbientLight()
	{
		return new AmbientLight(Color.FromRgb(160, 160, 160));
	}

	public static DirectionalLight CreateDirectionalLight()
	{
		return new DirectionalLight(Color.FromRgb(130, 130, 130), new Vector3D(-0.2, -0.35, -0.9));
	}

	public static BitmapImage LoadSkinBitmap(string source)
	{
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
		bitmapImage.UriSource = new Uri(source, UriKind.RelativeOrAbsolute);
		bitmapImage.EndInit();
		((Freezable)bitmapImage).Freeze();
		return bitmapImage;
	}

	public static Model3DGroup BuildPlayerModel(BitmapSource skin, MinecraftSkinModel? skinModel, double brightness = 1.0)
	{
		ArgumentNullException.ThrowIfNull(skin, "skin");
		int skinPixelHeight = Math.Max(skin.PixelHeight, 32);
		int armWidth = MinecraftSkinPreviewGeometry.GetArmWidth(skinModel);
		bool num = MinecraftSkinPreviewGeometry.CanUseHeadOverlay(skin.PixelWidth, skinPixelHeight);
		bool flag = MinecraftSkinPreviewGeometry.CanUseSecondLayer(skinPixelHeight);
		List<BatchedSkinFace> list = new List<BatchedSkinFace>(72);
		AddBatchedCuboid(list, new Rect3D(-4.0, 12.0, -4.0, 8.0, 8.0, 8.0), SkinPart.Head);
		AddBatchedCuboid(list, new Rect3D(-4.0, 0.0, -2.0, 8.0, 12.0, 4.0), SkinPart.Body);
		AddBatchedCuboid(list, new Rect3D(-4.0, -12.0, -2.0, 4.0, 12.0, 4.0), SkinPart.RightLeg);
		AddBatchedCuboid(list, new Rect3D(0.0, -12.0, -2.0, 4.0, 12.0, 4.0), flag ? SkinPart.LeftLeg : SkinPart.RightLeg);
		AddBatchedCuboid(list, new Rect3D(-4 - armWidth, 0.0, -2.0, armWidth, 12.0, 4.0), SkinPart.RightArm, armWidth);
		AddBatchedCuboid(list, new Rect3D(4.0, 0.0, -2.0, armWidth, 12.0, 4.0), flag ? SkinPart.LeftArm : SkinPart.RightArm, armWidth);
		if (num)
		{
			AddBatchedCuboid(list, new Rect3D(-4.35, 11.65, -4.35, 8.7, 8.7, 8.7), SkinPart.HeadOverlay, 4, isOverlay: true);
		}
		if (flag)
		{
			AddBatchedCuboid(list, new Rect3D(-4.25, -0.25, -2.25, 8.5, 12.5, 4.5), SkinPart.BodyOverlay, 4, isOverlay: true);
			AddBatchedCuboid(list, new Rect3D(-4.25, -12.25, -2.25, 4.5, 12.5, 4.5), SkinPart.RightLegOverlay, 4, isOverlay: true);
			AddBatchedCuboid(list, new Rect3D(-0.25, -12.25, -2.25, 4.5, 12.5, 4.5), SkinPart.LeftLegOverlay, 4, isOverlay: true);
			AddBatchedCuboid(list, new Rect3D(-4.25 - (double)armWidth, -0.25, -2.25, (double)armWidth + 0.5, 12.5, 4.5), SkinPart.RightArmOverlay, armWidth, isOverlay: true);
			AddBatchedCuboid(list, new Rect3D(3.75, -0.25, -2.25, (double)armWidth + 0.5, 12.5, 4.5), SkinPart.LeftArmOverlay, armWidth, isOverlay: true);
		}
		Model3DGroup model3DGroup = new Model3DGroup();
		AddBatchedLayer(model3DGroup, skin, list.Where((BatchedSkinFace item) => !item.IsOverlay), brightness, opaqueUnusedPixels: true);
		AddBatchedLayer(model3DGroup, skin, list.Where((BatchedSkinFace item) => item.IsOverlay), brightness, opaqueUnusedPixels: false);
		AxisAngleRotation3D axisAngleRotation3D = new AxisAngleRotation3D(new Vector3D(1.0, 0.0, 0.0), 2.0);
		((Freezable)axisAngleRotation3D).Freeze();
		RotateTransform3D rotateTransform3D = new RotateTransform3D(axisAngleRotation3D);
		((Freezable)rotateTransform3D).Freeze();
		model3DGroup.Transform = rotateTransform3D;
		((Freezable)model3DGroup).Freeze();
		return model3DGroup;
	}

	private static void AddBatchedLayer(Model3DGroup model, BitmapSource skin, IEnumerable<BatchedSkinFace> layerFaces, double brightness, bool opaqueUnusedPixels)
	{
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		BatchedSkinFace[] array = layerFaces.ToArray();
		if (array.Length != 0)
		{
			PreviewTextureAtlas previewTextureAtlas = PreviewTextureAtlasBuilder.Build(skin, array.Select(delegate(BatchedSkinFace item)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				return item.TextureRect;
			}), 8, brightness, 128, opaqueUnusedPixels);
			ImageBrush obj = new ImageBrush(previewTextureAtlas.Bitmap)
			{
				Stretch = Stretch.Fill,
				TileMode = TileMode.None
			};
			RenderOptions.SetBitmapScalingMode((DependencyObject)(object)obj, BitmapScalingMode.NearestNeighbor);
			((Freezable)obj).Freeze();
			DiffuseMaterial diffuseMaterial = new DiffuseMaterial(obj);
			((Freezable)diffuseMaterial).Freeze();
			PreviewMeshBuilder mesh = new PreviewMeshBuilder();
			BatchedSkinFace[] array2 = array;
			foreach (BatchedSkinFace batchedSkinFace in array2)
			{
				AddBatchedFace(mesh, batchedSkinFace.Bounds, batchedSkinFace.Face, previewTextureAtlas.TextureCoordinates[batchedSkinFace.TextureRect]);
			}
			AddBatchedMeshModel(model, mesh, diffuseMaterial);
		}
	}

	private static void AddBatchedCuboid(ICollection<BatchedSkinFace> target, Rect3D bounds, SkinPart part, int armWidth = 4, bool isOverlay = false)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		SkinPartFaces faces = MinecraftSkinPreviewGeometry.GetFaces(part, armWidth);
		target.Add(new BatchedSkinFace(bounds, CubeFace.Front, faces.Front, isOverlay));
		target.Add(new BatchedSkinFace(bounds, CubeFace.Back, faces.Back, isOverlay));
		target.Add(new BatchedSkinFace(bounds, CubeFace.Left, faces.Left, isOverlay));
		target.Add(new BatchedSkinFace(bounds, CubeFace.Right, faces.Right, isOverlay));
		target.Add(new BatchedSkinFace(bounds, CubeFace.Top, faces.Top, isOverlay));
		target.Add(new BatchedSkinFace(bounds, CubeFace.Bottom, faces.Bottom, isOverlay));
	}

	private static void AddBatchedFace(PreviewMeshBuilder mesh, Rect3D bounds, CubeFace face, Rect textureCoordinates)
	{
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		double x = bounds.X;
		double x2 = bounds.X + bounds.SizeX;
		double y = bounds.Y;
		double y2 = bounds.Y + bounds.SizeY;
		double z = bounds.Z;
		double z2 = bounds.Z + bounds.SizeZ;
		Point3D p;
		Point3D p2;
		Point3D p3;
		Point3D p4;
		switch (face)
		{
		case CubeFace.Front:
			p = new Point3D(x, y2, z2);
			p2 = new Point3D(x2, y2, z2);
			p3 = new Point3D(x2, y, z2);
			p4 = new Point3D(x, y, z2);
			break;
		case CubeFace.Back:
			p = new Point3D(x2, y2, z);
			p2 = new Point3D(x, y2, z);
			p3 = new Point3D(x, y, z);
			p4 = new Point3D(x2, y, z);
			break;
		case CubeFace.Left:
			p = new Point3D(x, y2, z);
			p2 = new Point3D(x, y2, z2);
			p3 = new Point3D(x, y, z2);
			p4 = new Point3D(x, y, z);
			break;
		case CubeFace.Right:
			p = new Point3D(x2, y2, z2);
			p2 = new Point3D(x2, y2, z);
			p3 = new Point3D(x2, y, z);
			p4 = new Point3D(x2, y, z2);
			break;
		case CubeFace.Top:
			p = new Point3D(x, y2, z);
			p2 = new Point3D(x2, y2, z);
			p3 = new Point3D(x2, y2, z2);
			p4 = new Point3D(x, y2, z2);
			break;
		default:
			p = new Point3D(x, y, z2);
			p2 = new Point3D(x2, y, z2);
			p3 = new Point3D(x2, y, z);
			p4 = new Point3D(x, y, z);
			break;
		}
		mesh.AddQuad(p, p2, p3, p4, textureCoordinates);
	}

	private static void AddBatchedMeshModel(Model3DGroup target, PreviewMeshBuilder mesh, Material material)
	{
		if (mesh.QuadCount != 0)
		{
			GeometryModel3D geometryModel3D = new GeometryModel3D
			{
				Geometry = mesh.Build(anchorUnitTextureCoordinates: true),
				Material = material,
				BackMaterial = material
			};
			((Freezable)geometryModel3D).Freeze();
			target.Children.Add(geometryModel3D);
		}
	}
}
