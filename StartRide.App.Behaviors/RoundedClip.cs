using System.Windows;
using System.Windows.Media;

namespace StartRide.App.Behaviors;

public static class RoundedClip
{
	public static readonly DependencyProperty RadiusProperty;

	public static double GetRadius(DependencyObject element)
	{
		return (double)element.GetValue(RadiusProperty);
	}

	public static void SetRadius(DependencyObject element, double value)
	{
		element.SetValue(RadiusProperty, (object)value);
	}

	private static void OnRadiusChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is FrameworkElement frameworkElement)
		{
			frameworkElement.SizeChanged -= Element_SizeChanged;
			if ((double)e.NewValue <= 0.0)
			{
				frameworkElement.Clip = null;
				return;
			}
			frameworkElement.SizeChanged += Element_SizeChanged;
			ApplyClip(frameworkElement);
		}
	}

	private static void Element_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (sender is FrameworkElement element)
		{
			ApplyClip(element);
		}
	}

	private static void ApplyClip(FrameworkElement element)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		double radius = GetRadius((DependencyObject)(object)element);
		if (!(radius <= 0.0) && !(element.ActualWidth <= 0.0) && !(element.ActualHeight <= 0.0))
		{
			element.Clip = new RectangleGeometry(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight), radius, radius);
		}
	}

	static RoundedClip()
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected O, but got Unknown
		RadiusProperty = DependencyProperty.RegisterAttached("Radius", typeof(double), typeof(RoundedClip), new PropertyMetadata((object)0.0, new PropertyChangedCallback(OnRadiusChanged)));
	}
}
