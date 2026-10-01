using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using StartRide.App.Converters;

namespace StartRide.App.Behaviors;

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

		SourceProperty = DependencyProperty.RegisterAttached("Source", typeof(Border), typeof(InnerCornerRadius), new PropertyMetadata((object)null, new PropertyChangedCallback(OnSourceChanged)));
	}
}
