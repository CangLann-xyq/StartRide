using System.Windows;

namespace StartRide.App.Behaviors;

public static class TextBoxPlaceholder
{
	public static readonly DependencyProperty TextProperty;

	public static string GetText(DependencyObject element)
	{
		return (string)element.GetValue(TextProperty);
	}

	public static void SetText(DependencyObject element, string value)
	{
		element.SetValue(TextProperty, (object)value);
	}

	static TextBoxPlaceholder()
	{

		TextProperty = DependencyProperty.RegisterAttached("Text", typeof(string), typeof(TextBoxPlaceholder), new PropertyMetadata((object)string.Empty));
	}
}
