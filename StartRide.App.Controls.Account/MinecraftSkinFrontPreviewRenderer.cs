using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Launcher.Domain.Models;

namespace StartRide.App.Controls.Account;

public static class MinecraftSkinFrontPreviewRenderer
{
	private const int PreviewHeight = 32;

	public static BitmapSource BuildFrontBitmap(BitmapSource skin, MinecraftSkinModel? skinModel)
	{
		BitmapSource bitmapSource = EnsureBgra32(skin);
		int armWidth = MinecraftSkinPreviewGeometry.GetArmWidth(skinModel);
		int num = armWidth * 2 + 8;
		byte[] array = new byte[num * 32 * 4];
		bool flag = MinecraftSkinPreviewGeometry.CanUseHeadOverlay(bitmapSource.PixelWidth, bitmapSource.PixelHeight);
		bool num2 = MinecraftSkinPreviewGeometry.CanUseSecondLayer(bitmapSource.PixelHeight);
		SkinPart part = (num2 ? SkinPart.LeftArm : SkinPart.RightArm);
		SkinPart part2 = (num2 ? SkinPart.LeftLeg : SkinPart.RightLeg);
		DrawPart(bitmapSource, array, num, SkinPart.RightArm, 0, 8, armWidth);
		DrawPart(bitmapSource, array, num, SkinPart.Body, armWidth, 8, armWidth);
		DrawPart(bitmapSource, array, num, part, armWidth + 8, 8, armWidth);
		DrawPart(bitmapSource, array, num, SkinPart.Head, armWidth, 0, armWidth);
		DrawPart(bitmapSource, array, num, SkinPart.RightLeg, armWidth, 20, armWidth);
		DrawPart(bitmapSource, array, num, part2, armWidth + 4, 20, armWidth);
		if (flag)
		{
			DrawPart(bitmapSource, array, num, SkinPart.HeadOverlay, armWidth, 0, armWidth);
		}
		if (num2)
		{
			DrawPart(bitmapSource, array, num, SkinPart.RightArmOverlay, 0, 8, armWidth);
			DrawPart(bitmapSource, array, num, SkinPart.BodyOverlay, armWidth, 8, armWidth);
			DrawPart(bitmapSource, array, num, SkinPart.LeftArmOverlay, armWidth + 8, 8, armWidth);
			DrawPart(bitmapSource, array, num, SkinPart.RightLegOverlay, armWidth, 20, armWidth);
			DrawPart(bitmapSource, array, num, SkinPart.LeftLegOverlay, armWidth + 4, 20, armWidth);
		}
		BitmapSource bitmapSource2 = BitmapSource.Create(num, 32, 96.0, 96.0, PixelFormats.Bgra32, null, array, num * 4);
		((Freezable)bitmapSource2).Freeze();
		return bitmapSource2;
	}

	private static void DrawPart(BitmapSource source, byte[] output, int outputWidth, SkinPart part, int destinationX, int destinationY, int armWidth)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		Int32Rect front = MinecraftSkinPreviewGeometry.GetFaces(part, armWidth).Front;
		Int32Rect sourceRect = ClampRect(source, front);
		int num = sourceRect.Width * 4;
		byte[] array = new byte[num * sourceRect.Height];
		source.CopyPixels(sourceRect, array, num, 0);
		for (int i = 0; i < sourceRect.Height; i++)
		{
			for (int j = 0; j < sourceRect.Width; j++)
			{
				int num2 = destinationX + j;
				int num3 = destinationY + i;
				if (num2 >= 0 && num2 < outputWidth && num3 >= 0 && num3 < 32)
				{
					int sourceIndex = i * num + j * 4;
					int destinationIndex = (num3 * outputWidth + num2) * 4;
					AlphaBlend(output, destinationIndex, array, sourceIndex);
				}
			}
		}
	}

	private static Int32Rect ClampRect(BitmapSource source, Int32Rect rect)
	{
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		int num = Math.Clamp(rect.X, 0, source.PixelWidth - 1);
		int num2 = Math.Clamp(rect.Y, 0, source.PixelHeight - 1);
		return new Int32Rect(num, num2, Math.Max(1, Math.Min(rect.Width, source.PixelWidth - num)), Math.Max(1, Math.Min(rect.Height, source.PixelHeight - num2)));
	}

	private static void AlphaBlend(byte[] destination, int destinationIndex, byte[] source, int sourceIndex)
	{
		double num = (double)(int)source[sourceIndex + 3] / 255.0;
		if (num <= 0.0)
		{
			return;
		}
		if (num >= 1.0)
		{
			destination[destinationIndex] = source[sourceIndex];
			destination[destinationIndex + 1] = source[sourceIndex + 1];
			destination[destinationIndex + 2] = source[sourceIndex + 2];
			destination[destinationIndex + 3] = source[sourceIndex + 3];
			return;
		}
		double num2 = (double)(int)destination[destinationIndex + 3] / 255.0;
		double num3 = num + num2 * (1.0 - num);
		if (!(num3 <= 0.0))
		{
			for (int i = 0; i < 3; i++)
			{
				double num4 = (double)(int)source[sourceIndex + i] / 255.0;
				double num5 = (double)(int)destination[destinationIndex + i] / 255.0;
				double num6 = (num4 * num + num5 * num2 * (1.0 - num)) / num3;
				destination[destinationIndex + i] = (byte)Math.Round(num6 * 255.0);
			}
			destination[destinationIndex + 3] = (byte)Math.Round(num3 * 255.0);
		}
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
