using System.Windows.Media.Media3D;

namespace StartRide.App.Controls.Account;

internal static class CarouselAnimationLifecycle
{
	internal static void CompleteAndRemoveClocks(ScaleTransform3D scale, TranslateTransform3D translate, double targetX, double targetScale)
	{
		ApplyBaseValues(scale, translate, targetX, targetScale);
		RemoveClocks(scale, translate);
	}

	internal static (double X, double Scale) CaptureCurrentAndRemoveClocks(ScaleTransform3D scale, TranslateTransform3D translate)
	{
		double offsetX = translate.OffsetX;
		double scaleX = scale.ScaleX;
		ApplyBaseValues(scale, translate, offsetX, scaleX);
		RemoveClocks(scale, translate);
		return (X: offsetX, Scale: scaleX);
	}

	private static void ApplyBaseValues(ScaleTransform3D scale, TranslateTransform3D translate, double x, double scaleValue)
	{
		translate.OffsetX = x;
		scale.ScaleX = scaleValue;
		scale.ScaleY = scaleValue;
		scale.ScaleZ = scaleValue;
	}

	private static void RemoveClocks(ScaleTransform3D scale, TranslateTransform3D translate)
	{
		translate.BeginAnimation(TranslateTransform3D.OffsetXProperty, null);
		scale.BeginAnimation(ScaleTransform3D.ScaleXProperty, null);
		scale.BeginAnimation(ScaleTransform3D.ScaleYProperty, null);
		scale.BeginAnimation(ScaleTransform3D.ScaleZProperty, null);
	}
}
