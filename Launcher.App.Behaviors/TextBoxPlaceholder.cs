using System.Windows;

namespace Launcher.App.Behaviors;

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
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		TextProperty = DependencyProperty.RegisterAttached("Text", typeof(string), typeof(TextBoxPlaceholder), new PropertyMetadata((object)string.Empty));
	}
}
