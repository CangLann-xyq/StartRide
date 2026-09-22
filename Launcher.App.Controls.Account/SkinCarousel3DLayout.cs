using System;
using Launcher.Domain.Models;

namespace Launcher.App.Controls.Account;

public static class SkinCarousel3DLayout
{
	private static readonly SkinCarouselSlotPlacement LeftPlacement = new SkinCarouselSlotPlacement(-10.0, 0.21);

	private static readonly SkinCarouselSlotPlacement CenterPlacement = new SkinCarouselSlotPlacement(0.0, 0.3);

	private static readonly SkinCarouselSlotPlacement RightPlacement = new SkinCarouselSlotPlacement(10.0, 0.21);

	private static readonly SkinCarouselSlotPlacement LeftEntryPlacement = new SkinCarouselSlotPlacement(-19.0, 0.21);

	private static readonly SkinCarouselSlotPlacement RightEntryPlacement = new SkinCarouselSlotPlacement(19.0, 0.21);

	public static SkinCarouselSlotPlacement GetPlacement(SkinCarouselSlot slot)
	{
		return slot switch
		{
			SkinCarouselSlot.Left => LeftPlacement, 
			SkinCarouselSlot.Right => RightPlacement, 
			_ => CenterPlacement, 
		};
	}

	public static SkinCarouselSlotPlacement GetEntryPlacement(SkinCarouselDirection direction)
	{
		if (direction != SkinCarouselDirection.Previous)
		{
			return RightEntryPlacement;
		}
		return LeftEntryPlacement;
	}

	public static bool CanAnimateTransition(SkinCarouselDirection? direction, LauncherSkinRecord? oldPreviousSkin, LauncherSkinRecord? oldSelectedSkin, LauncherSkinRecord? oldNextSkin, LauncherSkinRecord? newPreviousSkin, LauncherSkinRecord? newSelectedSkin, LauncherSkinRecord? newNextSkin)
	{
		return direction switch
		{
			SkinCarouselDirection.Next => SkinsRepresentSameVisualItem(newPreviousSkin, oldSelectedSkin) && SkinsRepresentSameVisualItem(newSelectedSkin, oldNextSkin), 
			SkinCarouselDirection.Previous => SkinsRepresentSameVisualItem(newSelectedSkin, oldPreviousSkin) && SkinsRepresentSameVisualItem(newNextSkin, oldSelectedSkin), 
			_ => false, 
		};
	}

	public static bool SkinsRepresentSameVisualItem(LauncherSkinRecord? left, LauncherSkinRecord? right)
	{
		if (left == null || right == null)
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(left.Id) && !string.IsNullOrWhiteSpace(right.Id))
		{
			return string.Equals(left.Id, right.Id, StringComparison.Ordinal);
		}
		if (!string.IsNullOrWhiteSpace(left.ContentHash) && !string.IsNullOrWhiteSpace(right.ContentHash))
		{
			if (left.SkinModel == right.SkinModel)
			{
				return string.Equals(left.ContentHash, right.ContentHash, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		if (left.SkinModel == right.SkinModel && !string.IsNullOrWhiteSpace(left.Source))
		{
			return string.Equals(left.Source, right.Source, StringComparison.Ordinal);
		}
		return false;
	}
}
