using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Launcher.App.Converters;

namespace Launcher.App.Behaviors;

public static class InnerCornerRadius
{
	public static readonly DependencyProperty SourceProperty;

	public static Border? GetSource(DependencyObject element)
	{
		return (Border)element.GetValue(SourceProperty);
	}

	public static void SetSource(DependencyObject element, Border? value)
	{
		element.SetValue(SourceProperty, (object)value);
	}

	private static void OnSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is Border target)
		{
			if (!(e.NewValue is Border source))
			{
				BindingOperations.ClearBinding((DependencyObject)(object)target, Border.CornerRadiusProperty);
				return;
			}
			MultiBinding multiBinding = new MultiBinding
			{
				Converter = InnerCornerRadiusConverter.Instance
			};
			multiBinding.Bindings.Add(new Binding("CornerRadius")
			{
				Source = source
			});
			multiBinding.Bindings.Add(new Binding("BorderThickness")
			{
				Source = source
			});
			BindingOperations.SetBinding((DependencyObject)(object)target, Border.CornerRadiusProperty, multiBinding);
		}
	}

	static InnerCornerRadius()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		SourceProperty = DependencyProperty.RegisterAttached("Source", typeof(Border), typeof(InnerCornerRadius), new PropertyMetadata((object)null, new PropertyChangedCallback(OnSourceChanged)));
	}
}
