using System;
using Launcher.Application.Accounts;

namespace Launcher.App.Controls.Account;

public static class CapeCarousel3DLayout
{
	private static readonly CapeCarouselSlotPlacement LeftPlacement = new CapeCarouselSlotPlacement(-7.5, 0.25);

	private static readonly CapeCarouselSlotPlacement CenterPlacement = new CapeCarouselSlotPlacement(0.0, 0.36);

	private static readonly CapeCarouselSlotPlacement RightPlacement = new CapeCarouselSlotPlacement(7.5, 0.25);

	private static readonly CapeCarouselSlotPlacement LeftEntryPlacement = new CapeCarouselSlotPlacement(-14.5, 0.25);

	private static readonly CapeCarouselSlotPlacement RightEntryPlacement = new CapeCarouselSlotPlacement(14.5, 0.25);

	public static CapeCarouselSlotPlacement GetPlacement(CapeCarouselSlot slot)
	{
		return slot switch
		{
			CapeCarouselSlot.Left => LeftPlacement, 
			CapeCarouselSlot.Right => RightPlacement, 
			_ => CenterPlacement, 
		};
	}

	public static CapeCarouselSlotPlacement GetEntryPlacement(CapeCarouselDirection direction)
	{
		if (direction != CapeCarouselDirection.Previous)
		{
			return RightEntryPlacement;
		}
		return LeftEntryPlacement;
	}

	public static bool CanAnimateTransition(CapeCarouselDirection? direction, AccountCapeOption? oldPreviousCape, AccountCapeOption? oldSelectedCape, AccountCapeOption? oldNextCape, AccountCapeOption? newPreviousCape, AccountCapeOption? newSelectedCape, AccountCapeOption? newNextCape)
	{
		return direction switch
		{
			CapeCarouselDirection.Next => CapesRepresentSameVisualItem(newPreviousCape, oldSelectedCape) && CapesRepresentSameVisualItem(newSelectedCape, oldNextCape), 
			CapeCarouselDirection.Previous => CapesRepresentSameVisualItem(newSelectedCape, oldPreviousCape) && CapesRepresentSameVisualItem(newNextCape, oldSelectedCape), 
			_ => false, 
		};
	}

	public static bool CapesRepresentSameVisualItem(AccountCapeOption? left, AccountCapeOption? right)
	{
		if (left == null || right == null)
		{
			return false;
		}
		if (left.IsNone || right.IsNone)
		{
			if (left.IsNone)
			{
				return right.IsNone;
			}
			return false;
		}
		if (!string.IsNullOrWhiteSpace(left.Id) && !string.IsNullOrWhiteSpace(right.Id))
		{
			return string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
		}
		if (!string.IsNullOrWhiteSpace(left.ImageUrl))
		{
			return string.Equals(left.ImageUrl, right.ImageUrl, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}
}
