using System.Windows;
using Launcher.Domain.Models;

namespace StartRide.App.Controls.Account;

public static class MinecraftSkinPreviewGeometry
{
	public static int GetArmWidth(MinecraftSkinModel? skinModel)
	{
		if (!skinModel.HasValue || skinModel != MinecraftSkinModel.Slim)
		{
			return 4;
		}
		return 3;
	}

	public static bool CanUseSecondLayer(int skinPixelHeight)
	{
		return skinPixelHeight >= 64;
	}

	public static bool CanUseHeadOverlay(int skinPixelWidth, int skinPixelHeight)
	{
		if (skinPixelWidth >= 64)
		{
			return skinPixelHeight >= 32;
		}
		return false;
	}

	public static SkinPartFaces GetFaces(SkinPart part, int armWidth = 4)
	{
		return part switch
		{
			SkinPart.Head => Cube(8, 0, 8, 8, 8, 8, 8, 8, 16, 8, 0, 8), 
			SkinPart.HeadOverlay => Cube(40, 0, 8, 8, 8, 8, 8, 8, 16, 8, 32, 8), 
			SkinPart.Body => Cube(20, 16, 8, 4, 8, 12, 4, 12, 4, 12, 16, 20), 
			SkinPart.BodyOverlay => Cube(20, 32, 8, 4, 8, 12, 4, 12, 4, 12, 16, 36), 
			SkinPart.RightArm => Arm(40, 16, armWidth), 
			SkinPart.RightArmOverlay => Arm(40, 32, armWidth), 
			SkinPart.LeftArm => Arm(32, 48, armWidth), 
			SkinPart.LeftArmOverlay => Arm(48, 48, armWidth), 
			SkinPart.RightLeg => Cube(4, 16, 4, 4, 4, 12, 4, 12, 4, 12, 0, 20), 
			SkinPart.RightLegOverlay => Cube(4, 32, 4, 4, 4, 12, 4, 12, 4, 12, 0, 36), 
			SkinPart.LeftLeg => Cube(20, 48, 4, 4, 4, 12, 4, 12, 4, 12, 16, 52), 
			SkinPart.LeftLegOverlay => Cube(4, 48, 4, 4, 4, 12, 4, 12, 4, 12, 0, 52), 
			_ => Cube(8, 0, 8, 8, 8, 8, 8, 8, 16, 8, 0, 8), 
		};
	}

	private static SkinPartFaces Arm(int x, int y, int armWidth)
	{
		return Cube(x + armWidth, y, armWidth, 4, armWidth, 12, 4, 12, armWidth, 12, x, y + 4);
	}

	private static SkinPartFaces Cube(int topX, int topY, int width, int depth, int frontWidth, int height, int sideWidth, int sideHeight, int backWidth, int backHeight, int leftX, int sideY)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		return new SkinPartFaces(new Int32Rect(leftX + sideWidth, sideY, frontWidth, height), new Int32Rect(leftX + sideWidth + frontWidth + sideWidth, sideY, backWidth, backHeight), new Int32Rect(leftX, sideY, sideWidth, sideHeight), new Int32Rect(leftX + sideWidth + frontWidth, sideY, sideWidth, sideHeight), new Int32Rect(topX, topY, width, depth), new Int32Rect(topX + width, topY, width, depth));
	}
}
