using System;
using Launcher.App.Effects;

namespace Launcher.App.Controls;

internal readonly record struct ProgressiveBlurEffectCreationResult(ProgressiveGaussianBlurEffect? HorizontalEffect, ProgressiveGaussianBlurEffect? VerticalEffect, Exception? Exception)
{
	internal bool IsSuccess
	{
		get
		{
			if (HorizontalEffect != null)
			{
				return VerticalEffect != null;
			}
			return false;
		}
	}

	internal static ProgressiveBlurEffectCreationResult Failed(Exception exception)
	{
		return new ProgressiveBlurEffectCreationResult(null, null, exception);
	}
}
