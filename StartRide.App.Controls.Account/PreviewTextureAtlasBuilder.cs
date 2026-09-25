using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StartRide.App.Controls.Account;

internal static class PreviewTextureAtlasBuilder
{
	private sealed record Placement(Int32Rect RequestedRegion, Int32Rect ClampedRegion, Int32Rect OuterRect);

	private const int GutterPixels = 1;

	internal static PreviewTextureAtlas Build(BitmapSource source, IEnumerable<Int32Rect> requestedRegions, int pixelScale, double brightness, int maximumSourceRowWidth, bool opaqueUnusedPixels = false)
	{
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		ArgumentNullException.ThrowIfNull(source, "source");
		if (pixelScale <= 0)
		{
			throw new ArgumentOutOfRangeException("pixelScale");
		}
		brightness = Math.Clamp(brightness, 0.0, 1.0);
		BitmapSource bitmapSource = EnsureBgra32(source);
		Int32Rect[] array = requestedRegions.Distinct().ToArray();
		if (array.Length == 0)
		{
			throw new ArgumentException("At least one texture region is required.", "requestedRegions");
		}
		IReadOnlyList<Placement> readOnlyList = Pack(array, bitmapSource.PixelWidth, bitmapSource.PixelHeight, maximumSourceRowWidth);
		int sourceWidth = Math.Max(1, readOnlyList.Max(delegate(Placement item)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			Int32Rect outerRect = item.OuterRect;
			int x2 = outerRect.X;
			outerRect = item.OuterRect;
			return x2 + outerRect.Width;
		}));
		int sourceHeight = Math.Max(1, readOnlyList.Max(delegate(Placement item)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			Int32Rect outerRect = item.OuterRect;
			int y2 = outerRect.Y;
			outerRect = item.OuterRect;
			return y2 + outerRect.Height;
		}));
		int num = bitmapSource.PixelWidth * 4;
		byte[] array2 = new byte[num * bitmapSource.PixelHeight];
		bitmapSource.CopyPixels(array2, num, 0);
		int num2 = sourceWidth * 4;
		byte[] array3 = new byte[num2 * sourceHeight];
		if (opaqueUnusedPixels)
		{
			for (int num3 = 3; num3 < array3.Length; num3 += 4)
			{
				array3[num3] = byte.MaxValue;
			}
		}
		foreach (Placement item in readOnlyList)
		{
			int num4 = 0;
			while (true)
			{
				int num5 = num4;
				Int32Rect val = item.OuterRect;
				if (num5 >= val.Height)
				{
					break;
				}
				val = item.ClampedRegion;
				int y = val.Y;
				int value = num4 - 1;
				val = item.ClampedRegion;
				int num6 = y + Math.Clamp(value, 0, val.Height - 1);
				int num7 = 0;
				while (true)
				{
					int num8 = num7;
					val = item.OuterRect;
					if (num8 >= val.Width)
					{
						break;
					}
					val = item.ClampedRegion;
					int x = val.X;
					int value2 = num7 - 1;
					val = item.ClampedRegion;
					int num9 = x + Math.Clamp(value2, 0, val.Width - 1);
					int num10 = num6 * num + num9 * 4;
					val = item.OuterRect;
					int num11 = (val.Y + num4) * num2;
					val = item.OuterRect;
					int num12 = num11 + (val.X + num7) * 4;
					array3[num12] = ApplyBrightness(array2[num10], brightness);
					array3[num12 + 1] = ApplyBrightness(array2[num10 + 1], brightness);
					array3[num12 + 2] = ApplyBrightness(array2[num10 + 2], brightness);
					array3[num12 + 3] = array2[num10 + 3];
					num7++;
				}
				num4++;
			}
		}
		int num13 = sourceWidth * pixelScale;
		int num14 = sourceHeight * pixelScale;
		int num15 = num13 * 4;
		byte[] array4 = new byte[num15 * num14];
		for (int num16 = 0; num16 < num14; num16++)
		{
			int num17 = num16 / pixelScale;
			for (int num18 = 0; num18 < num13; num18++)
			{
				int num19 = num17 * num2 + num18 / pixelScale * 4;
				int num20 = num16 * num15 + num18 * 4;
				array4[num20] = array3[num19];
				array4[num20 + 1] = array3[num19 + 1];
				array4[num20 + 2] = array3[num19 + 2];
				array4[num20 + 3] = array3[num19 + 3];
			}
		}
		BitmapSource bitmapSource2 = BitmapSource.Create(num13, num14, 96.0, 96.0, PixelFormats.Bgra32, null, array4, num15);
		((Freezable)bitmapSource2).Freeze();
		Dictionary<Int32Rect, Rect> textureCoordinates = ((IEnumerable<Placement>)readOnlyList).ToDictionary((Func<Placement, Int32Rect>)delegate(Placement item)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return item.RequestedRegion;
		}, (Func<Placement, Rect>)delegate(Placement item)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			Int32Rect val2 = item.OuterRect;
			double num21 = (double)(val2.X + 1) / (double)sourceWidth;
			val2 = item.OuterRect;
			double num22 = (double)(val2.Y + 1) / (double)sourceHeight;
			val2 = item.ClampedRegion;
			double num23 = (double)val2.Width / (double)sourceWidth;
			val2 = item.ClampedRegion;
			return new Rect(num21, num22, num23, (double)val2.Height / (double)sourceHeight);
		});
		return new PreviewTextureAtlas(bitmapSource2, textureCoordinates, sourceWidth, sourceHeight);
	}

	private static IReadOnlyList<Placement> Pack(IReadOnlyList<Int32Rect> requestedRegions, int sourceWidth, int sourceHeight, int maximumSourceRowWidth)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		int num = Math.Max(8, maximumSourceRowWidth);
		List<Placement> list = new List<Placement>(requestedRegions.Count);
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		Int32Rect outerRect = default(Int32Rect);
		foreach (Int32Rect requestedRegion in requestedRegions)
		{
			Int32Rect clampedRegion = ClampRegion(requestedRegion, sourceWidth, sourceHeight);
			int num5 = clampedRegion.Width + 2;
			int num6 = clampedRegion.Height + 2;
			if (num2 > 0 && num2 + num5 > num)
			{
				num2 = 0;
				num3 += num4;
				num4 = 0;
			}
			outerRect = new Int32Rect(num2, num3, num5, num6);
			list.Add(new Placement(requestedRegion, clampedRegion, outerRect));
			num2 += num5;
			num4 = Math.Max(num4, num6);
		}
		return list;
	}

	private static Int32Rect ClampRegion(Int32Rect region, int width, int height)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		int num = Math.Clamp(region.X, 0, width - 1);
		int num2 = Math.Clamp(region.Y, 0, height - 1);
		return new Int32Rect(num, num2, Math.Max(1, Math.Min(region.Width, width - num)), Math.Max(1, Math.Min(region.Height, height - num2)));
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

	private static byte ApplyBrightness(byte value, double brightness)
	{
		return (byte)Math.Round((double)(int)value * brightness);
	}
}
