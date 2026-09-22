using System.Windows;
using System.Windows.Input;

namespace Launcher.App.Behaviors;

public static class OptionHoverBehavior
{
	public static readonly DependencyProperty IsEnabledProperty;

	public static readonly DependencyProperty IsExternalActiveProperty;

	public static readonly DependencyProperty IsActiveProperty;

	public static bool GetIsEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsEnabledProperty);
	}

	public static void SetIsEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsEnabledProperty, (object)value);
	}

	public static bool GetIsExternalActive(DependencyObject element)
	{
		return (bool)element.GetValue(IsExternalActiveProperty);
	}

	public static void SetIsExternalActive(DependencyObject element, bool value)
	{
		element.SetValue(IsExternalActiveProperty, (object)value);
	}

	public static bool GetIsActive(DependencyObject element)
	{
		return (bool)element.GetValue(IsActiveProperty);
	}

	public static void SetIsActive(DependencyObject element, bool value)
	{
		element.SetValue(IsActiveProperty, (object)value);
	}

	private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is FrameworkElement frameworkElement)
		{
			if ((bool)e.NewValue)
			{
				frameworkElement.MouseEnter += Element_OnMouseStateChanged;
				frameworkElement.MouseLeave += Element_OnMouseStateChanged;
				UpdateIsActive(frameworkElement);
			}
			else
			{
				frameworkElement.MouseEnter -= Element_OnMouseStateChanged;
				frameworkElement.MouseLeave -= Element_OnMouseStateChanged;
				SetIsActive((DependencyObject)(object)frameworkElement, value: false);
			}
		}
	}

	private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is FrameworkElement element)
		{
			UpdateIsActive(element);
		}
	}

	private static void Element_OnMouseStateChanged(object sender, MouseEventArgs e)
	{
		if (sender is FrameworkElement element)
		{
			UpdateIsActive(element);
		}
	}

	private static void UpdateIsActive(FrameworkElement element)
	{
		SetIsActive((DependencyObject)(object)element, element.IsMouseOver || GetIsExternalActive((DependencyObject)(object)element));
	}

	static OptionHoverBehavior()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected O, but got Unknown
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Expected O, but got Unknown
		IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(OptionHoverBehavior), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsEnabledChanged)));
		IsExternalActiveProperty = DependencyProperty.RegisterAttached("IsExternalActive", typeof(bool), typeof(OptionHoverBehavior), new PropertyMetadata((object)false, new PropertyChangedCallback(OnStateChanged)));
		IsActiveProperty = DependencyProperty.RegisterAttached("IsActive", typeof(bool), typeof(OptionHoverBehavior), new PropertyMetadata((object)false));
	}
}
