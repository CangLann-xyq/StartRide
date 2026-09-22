using System;
using System.Windows;
using System.Windows.Data;

namespace Launcher.App.Services;

internal static class PageContentPrewarm
{
	internal static BindingBase? Begin(FrameworkElement page)
	{
		ArgumentNullException.ThrowIfNull(page, "page");
		BindingBase bindingBase = BindingOperations.GetBindingBase((DependencyObject)(object)page, UIElement.VisibilityProperty);
		page.Visibility = Visibility.Hidden;
		page.UpdateLayout();
		return bindingBase;
	}

	internal static bool End(FrameworkElement page, BindingBase? visibilityBinding)
	{
		ArgumentNullException.ThrowIfNull(page, "page");
		if (visibilityBinding == null)
		{
			page.Visibility = Visibility.Collapsed;
			return true;
		}
		BindingOperations.SetBinding((DependencyObject)(object)page, UIElement.VisibilityProperty, visibilityBinding);
		if (BindingOperations.GetBindingExpressionBase((DependencyObject)(object)page, UIElement.VisibilityProperty) != null)
		{
			return true;
		}
		page.Visibility = Visibility.Collapsed;
		return false;
	}
}
