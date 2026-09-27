using System.Windows;

namespace StartRide.App.Behaviors;

public static class SurfaceShadow
{
	public static readonly DependencyProperty SuppressChildShadowsProperty = DependencyProperty.RegisterAttached("SuppressChildShadows", typeof(bool), typeof(SurfaceShadow), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)false, FrameworkPropertyMetadataOptions.Inherits));

	public static bool GetSuppressChildShadows(DependencyObject element)
	{
		return (bool)element.GetValue(SuppressChildShadowsProperty);
	}

	public static void SetSuppressChildShadows(DependencyObject element, bool value)
	{
		element.SetValue(SuppressChildShadowsProperty, (object)value);
	}
}
