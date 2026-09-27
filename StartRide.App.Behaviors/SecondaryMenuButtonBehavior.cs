using System.Windows;

namespace StartRide.App.Behaviors;

public static class SecondaryMenuButtonBehavior
{
	public static readonly DependencyProperty IsSelectedProperty;

	public static readonly DependencyProperty SuppressSelectedBackgroundProperty;

	public static bool GetIsSelected(DependencyObject element)
	{
		return (bool)element.GetValue(IsSelectedProperty);
	}

	public static void SetIsSelected(DependencyObject element, bool value)
	{
		element.SetValue(IsSelectedProperty, (object)value);
	}

	public static bool GetSuppressSelectedBackground(DependencyObject element)
	{
		return (bool)element.GetValue(SuppressSelectedBackgroundProperty);
	}

	public static void SetSuppressSelectedBackground(DependencyObject element, bool value)
	{
		element.SetValue(SuppressSelectedBackgroundProperty, (object)value);
	}

	static SecondaryMenuButtonBehavior()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Expected O, but got Unknown
		IsSelectedProperty = DependencyProperty.RegisterAttached("IsSelected", typeof(bool), typeof(SecondaryMenuButtonBehavior), new PropertyMetadata((object)false));
		SuppressSelectedBackgroundProperty = DependencyProperty.RegisterAttached("SuppressSelectedBackground", typeof(bool), typeof(SecondaryMenuButtonBehavior), new PropertyMetadata((object)false));
	}
}
