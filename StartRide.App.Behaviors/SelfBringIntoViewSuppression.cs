using System.Windows;

namespace StartRide.App.Behaviors;

public static class SelfBringIntoViewSuppression
{
	public static readonly DependencyProperty IsEnabledProperty;

	public static bool GetIsEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsEnabledProperty);
	}

	public static void SetIsEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsEnabledProperty, (object)value);
	}

	private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is FrameworkElement frameworkElement)
		{
			if ((bool)e.NewValue)
			{
				frameworkElement.RequestBringIntoView += Element_OnRequestBringIntoView;
			}
			else
			{
				frameworkElement.RequestBringIntoView -= Element_OnRequestBringIntoView;
			}
		}
	}

	private static void Element_OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
	{
		if (e.OriginalSource == sender)
		{
			e.Handled = true;
		}
	}

	static SelfBringIntoViewSuppression()
	{

		IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(SelfBringIntoViewSuppression), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsEnabledChanged)));
	}
}
