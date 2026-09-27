using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace StartRide.App.Controls;

[ContentProperty("ListContent")]
public sealed class DeferredListContentHost : ContentControl
{
	public static readonly DependencyProperty ListContentProperty;

	public static readonly DependencyProperty IsListVisibleProperty;

	public object? ListContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(ListContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ListContentProperty, value);
		}
	}

	public bool IsListVisible
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsListVisibleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsListVisibleProperty, (object)value);
		}
	}

	public DeferredListContentHost()
	{
		base.HorizontalContentAlignment = HorizontalAlignment.Stretch;
		base.VerticalContentAlignment = VerticalAlignment.Stretch;
		base.Focusable = false;
		base.IsTabStop = false;
		UpdatePresentation();
	}

	private static void OnPresentationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is DeferredListContentHost deferredListContentHost)
		{
			deferredListContentHost.UpdatePresentation();
		}
	}

	private void UpdatePresentation()
	{
		base.Content = (IsListVisible ? ListContent : null);
		base.Visibility = ((!IsListVisible) ? Visibility.Collapsed : Visibility.Visible);
	}

	static DeferredListContentHost()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		ListContentProperty = DependencyProperty.Register("ListContent", typeof(object), typeof(DeferredListContentHost), new PropertyMetadata((object)null, new PropertyChangedCallback(OnPresentationChanged)));
		IsListVisibleProperty = DependencyProperty.Register("IsListVisible", typeof(bool), typeof(DeferredListContentHost), new PropertyMetadata((object)false, new PropertyChangedCallback(OnPresentationChanged)));
	}
}
